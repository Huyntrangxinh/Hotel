using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HotelBooking.Data;
using HotelBooking.Models;
using HotelBooking.ViewModels;
using Microsoft.AspNetCore.Identity;
using HotelBooking.Services;
using Microsoft.AspNetCore.Authorization;

namespace HotelBooking.Controllers
{
    public class BookingController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IBookingEmailService _bookingEmailService;

        public BookingController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IBookingEmailService bookingEmailService)
        {
            _db = db;
            _userManager = userManager;
            _bookingEmailService = bookingEmailService;
        }

        [HttpGet]
        public async Task<IActionResult> Book(int propertyId, int roomId, DateTime? checkIn, DateTime? checkOut, int? guests, int? roomPriceId, int? pricePackageId)
        {
            var property = await _db.Properties.FirstOrDefaultAsync(p => p.Id == propertyId);
            if (property == null) return NotFound();

            var room = await _db.Rooms
                .Include(r => r.Photos)
                .Include(r => r.Amenities)
                .FirstOrDefaultAsync(r => r.Id == roomId && r.PropertyId == propertyId);
            if (room == null) return NotFound();

            // Load RoomPrice theo roomPriceId nếu có, nếu không thì lấy đầu tiên
            var roomPrice = roomPriceId.HasValue && roomPriceId.Value > 0
                ? await _db.RoomPrices
                    .Include(rp => rp.PricePackage)
                    .FirstOrDefaultAsync(rp => rp.Id == roomPriceId.Value && rp.RoomId == roomId && rp.PropertyId == propertyId)
                : await _db.RoomPrices
                    .Include(rp => rp.PricePackage)
                    .FirstOrDefaultAsync(rp => rp.RoomId == roomId && rp.PropertyId == propertyId);

            var ci = checkIn ?? DateTime.Today.AddDays(1);
            var co = checkOut ?? ci.AddDays(1);
            var nights = Math.Max(1, (co - ci).Days);

            var viewModel = new BookingViewModel
            {
                PropertyId = propertyId,
                PropertyName = property.Name,
                RoomId = roomId,
                RoomName = room.Name,
                RoomSize = room.Size,
                RoomSizeUnit = room.SizeUnit,
                CapacityAdults = room.CapacityAdults,
                CapacityChildren = room.CapacityChildren,
                RoomPhotos = room.Photos?.Select(p => p.Url).ToList() ?? new List<string>(),
                RoomAmenities = room.Amenities?.Select(a => a.Name).ToList() ?? new List<string>(),
                CheckIn = ci,
                CheckOut = co,
                Guests = guests ?? 2,
                PricePerNight = roomPrice?.Amount ?? 0,
                TotalNights = nights,
                TotalPrice = (roomPrice?.Amount ?? 0) * nights,
                RoomPriceId = roomPriceId,
                PricePackageId = pricePackageId ?? roomPrice?.PricePackageId
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Book(BookingViewModel model)
        {
            bool essentialsOk =
                model.PropertyId > 0 &&
                model.RoomId > 0 &&
                model.CheckIn != default &&
                model.CheckOut != default &&
                !string.IsNullOrWhiteSpace(model.FullName) &&
                !string.IsNullOrWhiteSpace(model.Email);

            if (!ModelState.IsValid && !essentialsOk)
            {
                var property = await _db.Properties.FirstOrDefaultAsync(p => p.Id == model.PropertyId);
                var room = await _db.Rooms
                    .Include(r => r.Photos)
                    .Include(r => r.Amenities)
                    .FirstOrDefaultAsync(r => r.Id == model.RoomId && r.PropertyId == model.PropertyId);
                // Try to load RoomPrice by ID if provided, otherwise load first available
                var roomPrice = model.RoomPriceId.HasValue && model.RoomPriceId.Value > 0
                    ? await _db.RoomPrices
                        .Include(rp => rp.PricePackage)
                        .FirstOrDefaultAsync(rp => rp.Id == model.RoomPriceId.Value && rp.RoomId == model.RoomId && rp.PropertyId == model.PropertyId)
                    : await _db.RoomPrices
                        .Include(rp => rp.PricePackage)
                        .FirstOrDefaultAsync(rp => rp.RoomId == model.RoomId && rp.PropertyId == model.PropertyId);

                model.PropertyName = property?.Name;
                model.RoomName = room?.Name;
                model.RoomSize = room?.Size;
                model.RoomSizeUnit = room?.SizeUnit;
                model.CapacityAdults = room?.CapacityAdults ?? 0;
                model.CapacityChildren = room?.CapacityChildren ?? 0;
                model.RoomPhotos = room?.Photos?.Select(p => p.Url).ToList() ?? new List<string>();
                model.RoomAmenities = room?.Amenities?.Select(a => a.Name).ToList() ?? new List<string>();
                model.TotalNights = Math.Max(1, (model.CheckOut - model.CheckIn).Days);
                model.TotalPrice = model.PricePerNight * model.TotalNights * 1.1m; // Include taxes and fees
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            
            var booking = new Booking
            {
                PropertyId = model.PropertyId,
                RoomId = model.RoomId,
                UserId = user?.Id ?? "guest", // Sử dụng "guest" nếu user chưa đăng nhập
                BookingCode = GenerateBookingCode(),
                FullName = model.FullName,
                PhoneNumber = model.PhoneNumber,
                Email = model.Email,
                GuestName = model.GuestName,
                CheckIn = model.CheckIn,
                CheckOut = model.CheckOut,
                Guests = model.Guests,
                NonSmokingRoom = model.NonSmokingRoom,
                ConnectingRoom = model.ConnectingRoom,
                HighFloor = model.HighFloor,
                SpecialRequests = model.SpecialRequests,
                PricePerNight = model.PricePerNight,
                TotalNights = model.TotalNights,
                TotalPrice = model.TotalPrice,
                DiscountCode = model.DiscountCode ?? string.Empty,
                DiscountAmount = model.DiscountAmount,
                DiscountPercentage = model.DiscountPercentage,
                Status = BookingStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            _db.Bookings.Add(booking);
            await _db.SaveChangesAsync();

            // Trừ số lượng phòng trong bảng Rooms
            var roomToUpdate = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == model.RoomId);
            if (roomToUpdate != null && roomToUpdate.Quantity > 0)
            {
                roomToUpdate.Quantity -= 1; // Trừ đi 1 phòng
                _db.Rooms.Update(roomToUpdate);
                await _db.SaveChangesAsync();
            }

            return RedirectToAction("Payment", new { bookingId = booking.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmPayment(int bookingId)
        {
            var booking = await _db.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId);
            if (booking == null)
            {
                return RedirectToAction("Payment", new { bookingId });
            }

            if (booking.Status != BookingStatus.Confirmed)
            {
                booking.Status = BookingStatus.Confirmed;
                _db.Bookings.Update(booking);
                await _db.SaveChangesAsync();
            }

            if (!string.IsNullOrWhiteSpace(booking.Email))
            {
                var property = await _db.Properties.FirstOrDefaultAsync(p => p.Id == booking.PropertyId);
                var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == booking.RoomId);

                await _bookingEmailService.SendBookingConfirmationAsync(new BookingConfirmationEmailModel
                {
                    Email = booking.Email,
                    FullName = booking.FullName,
                    GuestName = booking.GuestName,
                    PhoneNumber = booking.PhoneNumber,
                    BookingCode = booking.BookingCode,
                    PropertyName = property?.Name ?? "Khách sạn",
                    RoomName = room?.Name ?? "Phòng",
                    CheckIn = booking.CheckIn,
                    CheckOut = booking.CheckOut,
                    TotalNights = booking.TotalNights,
                    Guests = booking.Guests,
                    TotalPrice = booking.TotalPrice
                });
            }

            return RedirectToAction("Success", new { bookingId = booking.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Success(string bookingId)
        {
            if (int.TryParse(bookingId, out int id))
            {
                var booking = await _db.Bookings.FirstOrDefaultAsync(b => b.Id == id);
                if (booking != null)
                {
                    ViewBag.BookingId = booking.BookingCode;
                    return View();
                }
            }
            return NotFound();
        }

        [HttpGet]
        public async Task<IActionResult> Payment(string bookingId)
        {
            if (!int.TryParse(bookingId, out var idInt)) return RedirectToAction("Index", "Home");
            var booking = await _db.Bookings.FirstOrDefaultAsync(b => b.Id == idInt);
            if (booking == null) return RedirectToAction("Index", "Home");

            var property = await _db.Properties.FirstOrDefaultAsync(p => p.Id == booking.PropertyId);
            var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == booking.RoomId);
            var pricePackage = await _db.PricePackages.FirstOrDefaultAsync(pp => pp.PropertyId == booking.PropertyId);

            ViewBag.BookingId = booking.Id;
            ViewBag.PropertyName = property?.Name ?? "Khách sạn";
            ViewBag.RoomName = room?.Name ?? "Phòng";
            ViewBag.CheckIn = booking.CheckIn;
            ViewBag.CheckOut = booking.CheckOut;
            ViewBag.TotalNights = booking.TotalNights;
            ViewBag.Guests = booking.Guests;
            
            // Calculate final price with discount
            var finalPrice = booking.TotalPrice;
            var originalPrice = booking.TotalPrice;
            
            // If there's a discount, calculate original price and final price
            if (!string.IsNullOrEmpty(booking.DiscountCode) && booking.DiscountAmount > 0)
            {
                originalPrice = booking.TotalPrice + booking.DiscountAmount;
                finalPrice = booking.TotalPrice; // TotalPrice already contains the discounted amount
            }
            
            ViewBag.TotalPrice = finalPrice;
            ViewBag.OriginalPrice = originalPrice;
            ViewBag.DiscountCode = booking.DiscountCode;
            ViewBag.DiscountAmount = booking.DiscountAmount;
            ViewBag.DiscountPercentage = booking.DiscountPercentage;

            // Contact details
            ViewBag.ContactFullName = booking.FullName;
            ViewBag.ContactPhone = booking.PhoneNumber;
            ViewBag.ContactEmail = booking.Email;
            ViewBag.GuestName = booking.GuestName;

            // Room details extras
            ViewBag.RoomChildrenCapacity = room?.CapacityChildren ?? 0;
            ViewBag.BedSummary = "";
            if (room != null)
            {
                var beds = await _db.RoomBeds.Where(b => b.RoomId == room.Id).ToListAsync();
                if (beds.Any())
                {
                    ViewBag.BedSummary = string.Join(" hoặc ", beds.Select(b => $"{(int)b.Count} {b.Type}"));
                }
            }
            ViewBag.BreakfastIncluded = pricePackage?.BreakfastIncluded ?? false;
            ViewBag.CancellationPolicyDisplayName = pricePackage?.CancellationPolicyDisplayName ?? "";
            return View();
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> MyBookings()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Lấy tất cả bookings của user (theo UserId hoặc Email)
            var bookings = await _db.Bookings
                .Where(b => b.UserId == user.Id || b.Email == user.Email)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            // Load thông tin Property và Room cho mỗi booking
            var bookingList = new List<dynamic>();
            var bookingIds = bookings.Select(b => b.Id).ToList();
            
            // Lấy danh sách booking đã có review
            var reviewedBookingIds = await _db.Reviews
                .Where(r => bookingIds.Contains(r.BookingId))
                .Select(r => r.BookingId)
                .ToListAsync();
            
            foreach (var booking in bookings)
            {
                var property = await _db.Properties.FirstOrDefaultAsync(p => p.Id == booking.PropertyId);
                var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == booking.RoomId);
                
                bookingList.Add(new
                {
                    Booking = booking,
                    Property = property,
                    Room = room
                });
            }

            ViewBag.Bookings = bookingList;
            ViewBag.ReviewIds = reviewedBookingIds;
            return View();
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Details(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var booking = await _db.Bookings.FirstOrDefaultAsync(b => b.Id == id);
            if (booking == null)
            {
                return NotFound();
            }

            // Kiểm tra quyền truy cập
            if (booking.UserId != user.Id && booking.Email != user.Email)
            {
                return Forbid();
            }

            var property = await _db.Properties.FirstOrDefaultAsync(p => p.Id == booking.PropertyId);
            var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == booking.RoomId);

            ViewBag.Booking = booking;
            ViewBag.Property = property;
            ViewBag.Room = room;

            return View();
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Review(int bookingId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var booking = await _db.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId);
            if (booking == null)
            {
                return NotFound();
            }

            // Kiểm tra quyền truy cập
            if (booking.UserId != user.Id && booking.Email != user.Email)
            {
                return Forbid();
            }

            // Kiểm tra đã qua ngày check-out chưa
            if (booking.CheckOut >= DateTime.Now)
            {
                return RedirectToAction("MyBookings");
            }

            // Kiểm tra đã có review chưa
            var existingReview = await _db.Reviews.FirstOrDefaultAsync(r => r.BookingId == bookingId);
            if (existingReview != null)
            {
                return RedirectToAction("MyBookings");
            }

            var property = await _db.Properties.FirstOrDefaultAsync(p => p.Id == booking.PropertyId);
            var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == booking.RoomId);

            ViewBag.Booking = booking;
            ViewBag.Property = property;
            ViewBag.Room = room;

            return View();
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Review(int bookingId, int rating, string? comment, int? cleanlinessRating, int? serviceRating, int? valueRating, int? locationRating)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var booking = await _db.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId);
            if (booking == null)
            {
                return NotFound();
            }

            // Kiểm tra quyền truy cập
            if (booking.UserId != user.Id && booking.Email != user.Email)
            {
                return Forbid();
            }

            // Kiểm tra đã có review chưa
            var existingReview = await _db.Reviews.FirstOrDefaultAsync(r => r.BookingId == bookingId);
            if (existingReview != null)
            {
                return RedirectToAction("MyBookings");
            }

            // Tạo review mới
            var review = new Review
            {
                BookingId = bookingId,
                PropertyId = booking.PropertyId,
                UserId = user.Id,
                Rating = rating,
                Comment = comment,
                CleanlinessRating = cleanlinessRating,
                ServiceRating = serviceRating,
                ValueRating = valueRating,
                LocationRating = locationRating,
                CreatedAt = DateTime.UtcNow
            };

            _db.Reviews.Add(review);
            await _db.SaveChangesAsync();

            TempData["ReviewSuccess"] = "Cảm ơn bạn đã đánh giá!";
            return RedirectToAction("MyBookings");
        }

        private string GenerateBookingCode()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            var result = new string(Enumerable.Repeat(chars, 8)
                .Select(s => s[random.Next(s.Length)]).ToArray());
            return result;
        }
    }
}


