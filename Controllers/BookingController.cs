using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HotelBooking.Data;
using HotelBooking.Models;
using HotelBooking.ViewModels;
using Microsoft.AspNetCore.Identity;

namespace HotelBooking.Controllers
{
    public class BookingController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public BookingController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Book(int propertyId, int roomId, DateTime? checkIn, DateTime? checkOut, int? guests)
        {
            var property = await _db.Properties.FirstOrDefaultAsync(p => p.Id == propertyId);
            if (property == null) return NotFound();

            var room = await _db.Rooms
                .Include(r => r.Photos)
                .Include(r => r.Amenities)
                .FirstOrDefaultAsync(r => r.Id == roomId && r.PropertyId == propertyId);
            if (room == null) return NotFound();

            var roomPrice = await _db.RoomPrices
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
                TotalPrice = (roomPrice?.Amount ?? 0) * nights
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
                var roomPrice = await _db.RoomPrices
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
                model.TotalPrice = model.PricePerNight * model.TotalNights;
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            var booking = new Booking
            {
                PropertyId = model.PropertyId,
                RoomId = model.RoomId,
                UserId = user?.Id,
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
                Status = BookingStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            _db.Bookings.Add(booking);
            await _db.SaveChangesAsync();

            return RedirectToAction("Payment", new { bookingId = booking.Id });
        }

        [HttpGet]
        public IActionResult Success(string bookingId)
        {
            ViewBag.BookingId = bookingId;
            return View();
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
            ViewBag.TotalPrice = booking.TotalPrice;

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
    }
}


