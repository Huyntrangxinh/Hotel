using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HotelBooking.Data;
using HotelBooking.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using HotelBooking.Models;
using HotelBooking.ViewModels;

namespace HotelBooking.Controllers
{
    public class PublicController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _users;

        public PublicController(ApplicationDbContext db, UserManager<ApplicationUser> users)
        {
            _db = db;
            _users = users;
        }

        // Public hotel page
        [HttpGet]
        public async Task<IActionResult> Hotel(int id, DateTime? checkin, DateTime? checkout, int? adults, int? children, int? rooms)
        {
            // Set dropdown visibility for layout
            if (User?.Identity?.IsAuthenticated == true)
            {
                var me = await _users.GetUserAsync(User);
                if (me != null)
                {
                    ViewBag.UserHasProperties = await _db.Properties.AnyAsync(p => p.UserId == me.Id);
                }
            }

            var property = await _db.Properties.FirstOrDefaultAsync(p => p.Id == id);
            if (property == null) return NotFound();

            var resolvedCheckIn = (checkin ?? DateTime.Today.AddDays(1)).Date;
            var resolvedCheckOut = (checkout ?? resolvedCheckIn.AddDays(1)).Date;
            if (resolvedCheckOut <= resolvedCheckIn)
            {
                resolvedCheckOut = resolvedCheckIn.AddDays(1);
            }

            var pd = await _db.PropertyData.FirstOrDefaultAsync(x => x.PropertyId == id);
            var pricePackage = await _db.PricePackages.FirstOrDefaultAsync(x => x.PropertyId == id);

            var roomsList = await _db.Rooms
                .Include(r => r.Photos)
                .Include(r => r.Amenities)
                .Where(r => r.PropertyId == id)
                .ToListAsync();

            var roomAvailability = await CalculateRoomAvailabilityAsync(id, roomsList, resolvedCheckIn, resolvedCheckOut);
            var propertySoldOut = roomAvailability.Any() && roomAvailability.Values.All(av => !av);

            // Load all RoomPrices with PricePackage for displaying room options
            // Note: SQLite doesn't support ORDER BY decimal, so we load first then sort in memory
            var allRoomPrices = await _db.RoomPrices
                .Include(rp => rp.PricePackage)
                .Where(rp => rp.PropertyId == id)
                .ToListAsync();
            
            // Sort in memory after loading (SQLite doesn't support ORDER BY decimal)
            allRoomPrices = allRoomPrices
                .OrderBy(rp => rp.RoomId)
                .ThenBy(rp => rp.Amount)
                .ToList();
            
            // Load daily rate overrides for the check-in/check-out period
            var dailyRateOverrides = await _db.RoomDailyRates
                .Where(r => r.PropertyId == id &&
                           r.Price.HasValue &&
                           r.Date >= resolvedCheckIn &&
                           r.Date < resolvedCheckOut)
                .Select(r => new { r.RoomId, r.RoomPriceId, r.Date, r.Price })
                .ToListAsync();
            
            // Create lookup: (RoomId, RoomPriceId, Date) -> Price
            var rateOverrideLookup = dailyRateOverrides
                .GroupBy(r => new { r.RoomId, r.RoomPriceId })
                .ToDictionary(
                    g => (g.Key.RoomId, g.Key.RoomPriceId),
                    g => g.ToDictionary(x => x.Date.Date, x => x.Price!.Value));
            
            // Load rate adjustments for descriptions
            var rateAdjustments = await _db.RoomRateAdjustments
                .Where(ra => ra.PropertyId == id &&
                            ra.StartDate <= resolvedCheckOut &&
                            ra.EndDate >= resolvedCheckIn)
                .Select(ra => new { ra.RoomId, ra.RoomPriceId, ra.StartDate, ra.EndDate, ra.Description })
                .ToListAsync();
            
            // Create lookup: (RoomId, RoomPriceId, Date) -> Description
            var adjustmentLookup = new Dictionary<(int RoomId, int? RoomPriceId, DateTime Date), string>();
            foreach (var adj in rateAdjustments)
            {
                for (var date = adj.StartDate.Date; date <= adj.EndDate.Date && date < resolvedCheckOut; date = date.AddDays(1))
                {
                    if (date >= resolvedCheckIn)
                    {
                        var key = (adj.RoomId, adj.RoomPriceId, date);
                        if (!adjustmentLookup.ContainsKey(key))
                        {
                            adjustmentLookup[key] = adj.Description ?? "";
                        }
                    }
                }
            }
            
            // Group by RoomId for minimum price lookup (backward compatibility)
            var roomPrices = allRoomPrices
                .GroupBy(rp => rp.RoomId)
                .ToDictionary(g => g.Key, g => g.Min(rp => rp.Amount));
            
            // Group all RoomPrices by RoomId for displaying all options
            var roomPricesByRoom = allRoomPrices
                .GroupBy(rp => rp.RoomId)
                .ToDictionary(g => g.Key, g => g.ToList());
            
            // Pass rate overrides and adjustments to view
            ViewBag.RateOverrideLookup = rateOverrideLookup;
            ViewBag.AdjustmentLookup = adjustmentLookup;
            ViewBag.CheckInDate = resolvedCheckIn;
            ViewBag.CheckOutDate = resolvedCheckOut;

            // Get all room photos for gallery
            var allRoomPhotos = roomsList
                .SelectMany(r => r.Photos)
                .OrderBy(p => p.SortOrder)
                .Select(p => p.Url)
                .ToList();

            // Convert PropertyData amenities to list for display
            var propertyAmenities = new List<(string Name, string Icon, string LocalizerKey)>();
            if (pd != null)
            {
                if (pd.HasSmokingArea) propertyAmenities.Add(("Khu vực hút thuốc", "bi-ban", "SmokingArea"));
                if (pd.HasAccessibleBathroom) propertyAmenities.Add(("Phòng tắm cho người khuyết tật", "bi-universal-access-circle", "AccessibleBathroom"));
                if (pd.HasElevator) propertyAmenities.Add(("Thang máy", "bi-arrow-up-circle", "Elevator"));
                if (pd.HasPublicWifi) propertyAmenities.Add(("WiFi công cộng", "bi-wifi", "FreeWiFi"));
                if (pd.HasAccessibleParking) propertyAmenities.Add(("Bãi đỗ xe cho người khuyết tật", "bi-p-circle", "AccessibleParking"));
                if (pd.HasParkingArea) propertyAmenities.Add(("Bãi đỗ xe", "bi-p-circle", "FreeParking"));
                if (pd.HasCafe) propertyAmenities.Add(("Quán cà phê", "bi-cup-hot", "Cafe"));
                if (pd.HasRestaurant) propertyAmenities.Add(("Nhà hàng", "bi-fork-knife", "Restaurant"));
                if (pd.HasBar) propertyAmenities.Add(("Quán bar", "bi-cup-straw", "Bar"));
                if (pd.HasFrontDesk) propertyAmenities.Add(("Quầy lễ tân", "bi-person-badge", "FrontDesk"));
                if (pd.HasExpressCheckIn) propertyAmenities.Add(("Nhận phòng nhanh", "bi-lightning-charge", "ExpressCheckIn"));
                if (pd.HasConcierge) propertyAmenities.Add(("Concierge", "bi-person-badge", "ConciergeService"));
                if (pd.HasExpressCheckOut) propertyAmenities.Add(("Trả phòng nhanh", "bi-lightning-charge-fill", "ExpressCheckOut"));
                if (pd.HasLaundryService) propertyAmenities.Add(("Dịch vụ giặt ủi", "bi-bucket", "LaundryService"));
                if (pd.Has24HourSecurity) propertyAmenities.Add(("Bảo vệ 24 giờ", "bi-shield-check", "24HourSecurity"));
                if (pd.HasLuggageStorage) propertyAmenities.Add(("Ký gửi hành lý", "bi-bag", "LuggageStorage"));
                if (pd.HasAirportTransfer) propertyAmenities.Add(("Đưa đón sân bay", "bi-airplane", "AirportTransfer"));
            }

            // Generate dynamic hotel description
            ViewBag.HotelDescription = GenerateHotelDescription(property.Name);

            // Get reviews from database for this property
            var realReviews = await _db.Reviews
                .Include(r => r.User)
                .Include(r => r.Booking)
                .Where(r => r.PropertyId == id)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            // Create default reviews (10 reviews)
            var defaultReviews = new List<HotelReviewViewModel>
            {
                new HotelReviewViewModel { Name = "Minh P.", Rating = 10.0m, Comment = "Giá trị thực sự tuyệt vời, bạn sẽ khó mà đòi hỏi gì hơn. Phòng mới nhưng bù lại rất sạch sẽ, hồ bơi sâu và quang cảnh thì tuyệt đẹp.", TimeAgo = "3 tuần", Initials = "MP" },
                new HotelReviewViewModel { Name = "NGUYEN M. D.", Rating = 10.0m, Comment = "Nhân viên lễ phép, thân thiện, nhiệt tình. Phòng đẹp, thoáng, có ban công.", TimeAgo = "3 tuần", Initials = "NM" },
                new HotelReviewViewModel { Name = "Tran T. H. D.", Rating = 6.0m, Comment = "2 năm trước mình ở đây hơn 1 tháng, mình thích khách sạn này vì gần chùa nên mình cảm thấy bình yên. 2 năm sau quay lại khách sạn xuống cấp nhanh kinh khủng.", TimeAgo = "4 tuần", Initials = "TT", IsBusiness = true },
                new HotelReviewViewModel { Name = "NGUYEN H. M.", Rating = 9.4m, Comment = "Rất tuyệt vời, gia đình chúng tôi sẽ quay lại lần tiếp theo!", TimeAgo = "5 tuần", Initials = "NH" },
                new HotelReviewViewModel { Name = "Le T. V.", Rating = 9.8m, Comment = "Khách sạn rất đẹp, vị trí thuận tiện, nhân viên phục vụ chu đáo. Bữa sáng phong phú và ngon miệng.", TimeAgo = "1 tháng", Initials = "LV" },
                new HotelReviewViewModel { Name = "Pham D. K.", Rating = 8.5m, Comment = "Phòng rộng rãi, sạch sẽ. Hồ bơi rất đẹp, view đẹp. Chỉ có điều wifi hơi chậm một chút.", TimeAgo = "1 tháng", Initials = "PK" },
                new HotelReviewViewModel { Name = "Hoang N. T.", Rating = 9.2m, Comment = "Trải nghiệm tuyệt vời! Phòng view đẹp, dịch vụ tốt. Sẽ quay lại vào lần tới.", TimeAgo = "2 tháng", Initials = "HT" },
                new HotelReviewViewModel { Name = "Vu T. M.", Rating = 8.8m, Comment = "Khách sạn đẹp, giá cả hợp lý. Nhân viên thân thiện và nhiệt tình. Đáng để trải nghiệm.", TimeAgo = "2 tháng", Initials = "VM" },
                new HotelReviewViewModel { Name = "Doan H. L.", Rating = 9.6m, Comment = "Tuyệt vời từ A đến Z! Phòng sạch, view đẹp, dịch vụ chu đáo. Bữa sáng rất ngon.", TimeAgo = "2 tháng", Initials = "DL" },
                new HotelReviewViewModel { Name = "Bui Q. A.", Rating = 8.9m, Comment = "Khách sạn tốt, vị trí đẹp. Phòng rộng và sạch sẽ. Nhân viên phục vụ tốt.", TimeAgo = "3 tháng", Initials = "BA" }
            };

            // Convert real reviews to ViewModel
            // Review.Rating is 1-5, but ViewModel expects 0-10 scale, so multiply by 2
            var realReviewViewModels = realReviews.Select(r => new HotelReviewViewModel
            {
                Name = r.User?.UserName ?? r.Booking?.FullName ?? "Khách",
                Rating = r.Rating * 2, // Convert from 1-5 scale to 2-10 scale
                Comment = r.Comment ?? "",
                TimeAgo = GetTimeAgo(r.CreatedAt),
                Initials = GetInitials(r.User?.UserName ?? r.Booking?.FullName ?? "K"),
                IsReal = true
            }).ToList();

            // Combine: real reviews first, then default reviews (total 10)
            var allReviews = realReviewViewModels.Take(10).ToList();
            var remainingSlots = 10 - allReviews.Count;
            if (remainingSlots > 0)
            {
                allReviews.AddRange(defaultReviews.Take(remainingSlots));
            }

            // Calculate average ratings (combine real and default)
            // Review.Rating is 1-5, convert to 2-10 scale for consistency with default reviews
            var allRatings = realReviews.Select(r => (double)(r.Rating * 2)).ToList();
            allRatings.AddRange(defaultReviews.Select(d => (double)d.Rating));
            var avgRating = allRatings.Any() ? allRatings.Average() : 8.9;
            
            // Sub-ratings are also 1-5 scale, convert to 2-10 scale for consistency
            var cleanlinessRatings = realReviews.Where(r => r.CleanlinessRating.HasValue).Select(r => (double)(r.CleanlinessRating!.Value * 2)).ToList();
            cleanlinessRatings.AddRange(defaultReviews.Select(d => 8.7)); // Default cleanliness
            var avgCleanliness = cleanlinessRatings.Any() ? cleanlinessRatings.Average() : 8.7;
            
            var serviceRatings = realReviews.Where(r => r.ServiceRating.HasValue).Select(r => (double)(r.ServiceRating!.Value * 2)).ToList();
            serviceRatings.AddRange(defaultReviews.Select(d => 8.7)); // Default service
            var avgService = serviceRatings.Any() ? serviceRatings.Average() : 8.7;
            
            var valueRatings = realReviews.Where(r => r.ValueRating.HasValue).Select(r => (double)(r.ValueRating!.Value * 2)).ToList();
            valueRatings.AddRange(defaultReviews.Select(d => 8.4)); // Default value
            var avgValue = valueRatings.Any() ? valueRatings.Average() : 8.4;
            
            var locationRatings = realReviews.Where(r => r.LocationRating.HasValue).Select(r => (double)(r.LocationRating!.Value * 2)).ToList();
            locationRatings.AddRange(defaultReviews.Select(d => 8.6)); // Default location
            var avgLocation = locationRatings.Any() ? locationRatings.Average() : 8.6;

            ViewBag.Reviews = allReviews;
            ViewBag.AvgRating = avgRating;
            ViewBag.AvgCleanliness = avgCleanliness;
            ViewBag.AvgService = avgService;
            ViewBag.AvgValue = avgValue;
            ViewBag.AvgLocation = avgLocation;
            ViewBag.RoomPricesByRoom = roomPricesByRoom; // All room prices grouped by RoomId for displaying options
            ViewBag.TotalReviews = realReviews.Count + 10;

            var vm = new PublicHotelViewModel
            {
                Property = property,
                PropertyData = pd,
                PricePackage = pricePackage,
                Rooms = roomsList,
                RoomPrices = roomPrices,
                PhotoUrls = (pd?.PhotoPaths ?? "")
                    .Split('|', System.StringSplitOptions.RemoveEmptyEntries)
                    .ToList(),
                RoomPhotoUrls = allRoomPhotos,
                RoomAvailability = roomAvailability,
                PropertySoldOut = propertySoldOut,
                CheckInDate = resolvedCheckIn,
                CheckOutDate = resolvedCheckOut
            };

            // Pass search parameters to view
            ViewBag.Checkin = resolvedCheckIn;
            ViewBag.Checkout = resolvedCheckOut;
            ViewBag.Adults = adults;
            ViewBag.Children = children;
            ViewBag.PropertyAmenities = propertyAmenities;
            ViewBag.Rooms = rooms ?? 1;

            return View(vm);
        }

        private async Task<Dictionary<int, bool>> CalculateRoomAvailabilityAsync(int propertyId, List<Room>? preloadedRooms, DateTime checkIn, DateTime checkOut)
        {
            var startDate = checkIn.Date;
            var endDate = checkOut.Date;
            if (endDate <= startDate)
            {
                endDate = startDate.AddDays(1);
            }

            var rooms = preloadedRooms ?? await _db.Rooms
                .Where(r => r.PropertyId == propertyId)
                .ToListAsync();

            var roomIds = rooms.Select(r => r.Id).ToList();
            if (!roomIds.Any())
            {
                return new Dictionary<int, bool>();
            }

            var bookings = await _db.Bookings
                .Where(b => b.PropertyId == propertyId &&
                            b.Status != BookingStatus.Cancelled &&
                            b.CheckIn < endDate &&
                            b.CheckOut > startDate &&
                            roomIds.Contains(b.RoomId))
                .Select(b => new { b.RoomId, b.CheckIn, b.CheckOut })
                .ToListAsync();

            var bookingLookup = bookings
                .GroupBy(b => b.RoomId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var rates = await _db.RoomDailyRates
                .Where(r => r.PropertyId == propertyId &&
                            r.Date >= startDate &&
                            r.Date < endDate &&
                            roomIds.Contains(r.RoomId))
                .Select(r => new { r.RoomId, r.RoomPriceId, Date = r.Date.Date, r.Allotment, r.IsClosed })
                .ToListAsync();

            var rateLookup = rates
                .GroupBy(r => r.RoomId)
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .GroupBy(x => x.Date)
                        .ToDictionary(
                            dg => dg.Key,
                            dg => dg.First()
                        ));

            var availability = new Dictionary<int, bool>();

            foreach (var room in rooms)
            {
                var baseQuantity = Math.Max(room.Quantity, 0);
                var available = baseQuantity > 0;

                if (!available)
                {
                    availability[room.Id] = false;
                    continue;
                }

                var cursor = startDate;
                while (cursor < endDate)
                {
                    var allotment = baseQuantity;
                    if (rateLookup.TryGetValue(room.Id, out var byDate) &&
                        byDate.TryGetValue(cursor, out var rateForDate))
                    {
                        if (rateForDate.IsClosed)
                        {
                            available = false;
                            break;
                        }

                        if (rateForDate.Allotment.HasValue)
                        {
                            allotment = Math.Min(rateForDate.Allotment.Value, baseQuantity);
                        }
                    }

                    if (allotment <= 0)
                    {
                        available = false;
                        break;
                    }

                    var bookedCount = bookingLookup.TryGetValue(room.Id, out var list)
                        ? list.Count(b => b.CheckIn.Date <= cursor && b.CheckOut.Date > cursor)
                        : 0;

                    if (bookedCount >= allotment)
                    {
                        available = false;
                        break;
                    }

                    cursor = cursor.AddDays(1);
                }

                availability[room.Id] = available;
            }

            return availability;
        }

        private string GetTimeAgo(DateTime dateTime)
        {
            var timeSpan = DateTime.UtcNow - dateTime;
            
            if (timeSpan.TotalDays < 1)
                return "Hôm nay";
            if (timeSpan.TotalDays < 7)
                return $"{(int)timeSpan.TotalDays} ngày trước";
            if (timeSpan.TotalDays < 30)
                return $"{(int)(timeSpan.TotalDays / 7)} tuần trước";
            if (timeSpan.TotalDays < 365)
                return $"{(int)(timeSpan.TotalDays / 30)} tháng trước";
            return $"{(int)(timeSpan.TotalDays / 365)} năm trước";
        }

        private string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "K";
            
            var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return (parts[0][0].ToString() + parts[parts.Length - 1][0].ToString()).ToUpper();
            if (parts.Length == 1)
                return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpper();
            return "K";
        }

        // Search results page: list properties by destination and optional dates
        [HttpGet]
        public async Task<IActionResult> Search(string? destination, DateOnly? checkin, DateOnly? checkout, string? filters, int adults = 2, int children = 0, int rooms = 1)
        {
            var searchCheckInDate = (checkin?.ToDateTime(TimeOnly.MinValue) ?? DateTime.Today.AddDays(1)).Date;
            var searchCheckOutDate = (checkout?.ToDateTime(TimeOnly.MinValue) ?? searchCheckInDate.AddDays(1)).Date;
            if (searchCheckOutDate <= searchCheckInDate)
            {
                searchCheckOutDate = searchCheckInDate.AddDays(1);
            }

            var normalizedCheckin = checkin ?? DateOnly.FromDateTime(searchCheckInDate);
            var normalizedCheckout = checkout ?? DateOnly.FromDateTime(searchCheckOutDate);

            var query = _db.Properties.AsQueryable();
            if (!string.IsNullOrWhiteSpace(destination))
            {
                var dest = destination.Trim();
                query = query.Where(p => p.City.Contains(dest) || p.Name.Contains(dest));
            }

            // Apply filters if provided
            if (!string.IsNullOrWhiteSpace(filters))
            {
                var filterList = filters.Split(',').Select(f => f.Trim()).ToList();
                
                // Load PropertyData for filtering
                var propertyDataQuery = _db.PropertyData.AsQueryable();
                
                foreach (var filter in filterList)
                {
                    switch (filter)
                    {
                        case "type_resort":
                            query = query.Where(p => p.Type == PropertyType.Resort);
                            break;
                        case "type_hotel":
                            query = query.Where(p => p.Type == PropertyType.Hotel);
                            break;
                        case "type_guesthouse":
                            query = query.Where(p => p.Type == PropertyType.GuestHouse);
                            break;
                        case "type_apartment":
                            query = query.Where(p => p.Type == PropertyType.Apartment);
                            break;
                        case "type_villa":
                            query = query.Where(p => p.Type == PropertyType.Villa);
                            break;
                        case "type_homestay":
                            query = query.Where(p => p.Type == PropertyType.Homestay);
                            break;
                        case "type_hostel":
                            query = query.Where(p => p.Type == PropertyType.Hostel);
                            break;
                        case "breakfast":
                            // Filter by breakfast included
                            var breakfastPropertyIds = await _db.PricePackages
                                .Where(pp => pp.BreakfastIncluded == true)
                                .Select(pp => pp.PropertyId)
                                .ToListAsync();
                            query = query.Where(p => breakfastPropertyIds.Contains(p.Id));
                            break;
                        case "pool":
                            // Filter by has pool (assuming this is in PropertyData)
                            var poolPropertyIds = await propertyDataQuery
                                .Where(pd => pd.HasPublicWifi == true) // Placeholder - adjust based on your actual field
                                .Select(pd => pd.PropertyId)
                                .ToListAsync();
                            query = query.Where(p => poolPropertyIds.Contains(p.Id));
                            break;
                        case "star_4_5":
                            // Filter by 4-5 star rating
                            var starPropertyIds = await propertyDataQuery
                                .Where(pd => pd.StarRating >= 4)
                                .Select(pd => pd.PropertyId)
                                .ToListAsync();
                            query = query.Where(p => starPropertyIds.Contains(p.Id));
                            break;
                       case "good_price":
                           // Filter by good price (assuming price < 1000000 VND)
                           var pricePropertyIds = await _db.RoomPrices
                               .Where(rp => rp.Amount < 1000000)
                               .Select(rp => rp.PropertyId)
                               .ToListAsync();
                           query = query.Where(p => pricePropertyIds.Contains(p.Id));
                           break;
                       case "star_5":
                           // Filter by 5 star rating
                           var star5PropertyIds = await propertyDataQuery
                               .Where(pd => pd.StarRating == 5)
                               .Select(pd => pd.PropertyId)
                               .ToListAsync();
                           query = query.Where(p => star5PropertyIds.Contains(p.Id));
                           break;
                       case "star_4":
                           // Filter by 4 star rating
                           var star4PropertyIds = await propertyDataQuery
                               .Where(pd => pd.StarRating == 4)
                               .Select(pd => pd.PropertyId)
                               .ToListAsync();
                           query = query.Where(p => star4PropertyIds.Contains(p.Id));
                           break;
                       case "star_3":
                           // Filter by 3 star rating
                           var star3PropertyIds = await propertyDataQuery
                               .Where(pd => pd.StarRating == 3)
                               .Select(pd => pd.PropertyId)
                               .ToListAsync();
                           query = query.Where(p => star3PropertyIds.Contains(p.Id));
                           break;
                       case "impressive_8":
                           // Filter by high rating (8+)
                           var impressivePropertyIds = await propertyDataQuery
                               .Where(pd => pd.StarRating >= 4) // Assuming 4+ stars is impressive
                               .Select(pd => pd.PropertyId)
                               .ToListAsync();
                           query = query.Where(p => impressivePropertyIds.Contains(p.Id));
                           break;
                       
                       // Linh hoạt hơn filters
                       case "flexible_free_cancellation":
                           // Filter by free cancellation (using CancellationPolicy field)
                           var freeCancellationPropertyIds = await _db.PricePackages
                               .Where(pp => pp.CancellationPolicy != "non_refundable")
                               .Select(pp => pp.PropertyId)
                               .ToListAsync();
                           query = query.Where(p => freeCancellationPropertyIds.Contains(p.Id));
                           break;
                       case "flexible_pay_at_hotel":
                           // Filter by pay at hotel - since this field doesn't exist, we'll skip this filter for now
                           // or you can add this field to the PricePackage model if needed
                           break;
                       // Add more filter cases as needed
                    }
                }
            }

            var results = await query
                .OrderBy(p => p.Name)
                .Take(100)
                .ToListAsync();

            // Load photo data separately and build main photo map in-memory (avoid EF expression tree issues)
            var propertyIds = results.Select(p => p.Id).ToList();
            var pdList = await _db.PropertyData
                .Where(pd => propertyIds.Contains(pd.PropertyId))
                .ToListAsync();

            var photoMap = pdList
                .GroupBy(pd => pd.PropertyId)
                .ToDictionary(
                    g => g.Key,
                    g => {
                        var paths = (g.FirstOrDefault()?.PhotoPaths ?? string.Empty)
                            .Split('|', StringSplitOptions.RemoveEmptyEntries);
                        return paths.FirstOrDefault() ?? "/img/recent.jpg";
                    }
                );

            // Load room prices and compute min in memory to avoid SQLite decimal aggregate issue
            var roomPrices = await _db.RoomPrices
                .Where(rp => propertyIds.Contains(rp.PropertyId))
                .ToListAsync();
            var minPriceMap = roomPrices
                .GroupBy(rp => rp.PropertyId)
                .ToDictionary(g => g.Key, g => g.Min(x => x.Amount));

            // Load a featured room per property to show brief info
            var featuredRooms = await _db.Rooms
                .Include(r => r.Beds)
                .Where(r => results.Select(p => p.Id).Contains(r.PropertyId))
                .GroupBy(r => r.PropertyId)
                .Select(g => g.OrderBy(r => r.Id).First())
                .ToListAsync();
            var roomMap = featuredRooms.ToDictionary(r => r.PropertyId, r => r);

             // Group by PropertyId and check if any package has breakfast included
            // (A property is considered to have breakfast if at least one package includes it)
            var breakfastMap = (await _db.PricePackages
                .Where(pp => propertyIds.Contains(pp.PropertyId))
                .GroupBy(pp => pp.PropertyId)
                .Select(g => new { PropertyId = g.Key, HasBreakfast = g.Any(pp => pp.BreakfastIncluded) })
                .ToListAsync())
                .ToDictionary(x => x.PropertyId, x => x.HasBreakfast);

            var starMap = pdList.ToDictionary(pd => pd.PropertyId, pd => pd.StarRating);

            var propertyAvailability = new Dictionary<int, bool>();
            foreach (var property in results)
            {
                var availability = await CalculateRoomAvailabilityAsync(property.Id, null, searchCheckInDate, searchCheckOutDate);
                propertyAvailability[property.Id] = availability.Values.Any(v => v);
            }

            var vm = new PublicSearchResultsViewModel
            {
                Destination = destination ?? string.Empty,
                Checkin = normalizedCheckin,
                Checkout = normalizedCheckout,
                Properties = results,
                MainPhotoUrls = photoMap,
                MinPriceByProperty = minPriceMap,
                StarRatingByProperty = starMap,
                BreakfastIncludedByProperty = breakfastMap,
                FeaturedRoomByProperty = roomMap,
                PropertyAvailability = propertyAvailability
            };

            // Pass search parameters to view
            ViewBag.SearchDestination = destination;
            ViewBag.SearchCheckin = normalizedCheckin;
            ViewBag.SearchCheckout = normalizedCheckout;
            ViewBag.SearchAdults = adults;
            ViewBag.SearchChildren = children;
            ViewBag.SearchRooms = rooms;

            // Check if this is an AJAX request
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("_SearchResults", vm);
            }
            
            return View(vm);
        }

        private string GenerateHotelDescription(string hotelName)
        {
            return $@"Thông tin về {hotelName}

Khách sạn này là lựa chọn hoàn hảo cho các kỳ nghỉ mát lãng mạn hay tuần trăng mật của các cặp đôi. Quý khách hãy tận hưởng những đêm đáng nhớ nhất cùng người thương của mình tại {hotelName}

Một trong những đặc điểm chính của khách sạn này là các liệu pháp spa đa dạng. Hãy nâng niu bản thân bằng các liệu pháp thư giãn, phục hồi giúp quý khách tươi trẻ thân, tâm.

Từ sự kiện doanh nghiệp đến họp mặt công ty, {hotelName} cung cấp đầy đủ các dịch vụ và tiện nghi đáp ứng mọi nhu cầu của quý khách và đồng nghiệp.

Hãy tận hưởng thời gian vui vẻ cùng cả gia đình với hàng loạt tiện nghi giải trí tại {hotelName}, một khách sạn tuyệt vời phù hợp cho mọi kỳ nghỉ bên người thân.

Nếu dự định có một kỳ nghỉ dài, thì {hotelName} chính là lựa chọn dành cho quý khách. Với đầy đủ tiện nghi với chất lượng dịch vụ tuyệt vời, {hotelName} sẽ khiến quý khách cảm thấy thoải mái như đang ở nhà vậy.

Dịch vụ tuyệt vời, cơ sở vật chất hoàn chỉnh và các tiện nghi khách sạn cung cấp sẽ khiến quý khách không thể phàn nàn trong suốt kỳ lưu trú tại {hotelName}.

Hưởng thụ một ngày thư thái đầy thú vị tại hồ bơi dù quý khách đang du lịch một mình hay cùng người thân.

Quầy tiếp tân 24 giờ luôn sẵn sàng phục vụ quý khách từ thủ tục nhận phòng đến trả phòng hay bất kỳ yêu cầu nào. Nếu cần giúp đỡ xin hãy liên hệ đội ngũ tiếp tân, chúng tôi luôn sẵn sàng hỗ trợ quý khách.

Tận hưởng những món ăn yêu thích với phong cách ẩm thực đặc biệt từ {hotelName} chỉ dành riêng cho quý khách.

Sóng WiFi phủ khắp các khu vực chung của khách sạn cho phép quý khách luôn kết nối với gia đình và bè bạn.

{hotelName} là khách sạn sở hữu đầy đủ tiện nghi và dịch vụ xuất sắc theo nhận định của hầu hết khách lưu trú.

Với những tiện nghi sẵn có {hotelName} thực sự là một nơi lưu trú hoàn hảo.";
        }

    }
}


