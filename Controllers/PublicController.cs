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

            var pd = await _db.PropertyData.FirstOrDefaultAsync(x => x.PropertyId == id);
            var pricePackage = await _db.PricePackages.FirstOrDefaultAsync(x => x.PropertyId == id);

            var roomsList = await _db.Rooms
                .Include(r => r.Photos)
                .Include(r => r.Amenities)
                .Where(r => r.PropertyId == id)
                .ToListAsync();

            var roomPrices = await _db.RoomPrices
                .Where(rp => rp.PropertyId == id)
                .ToDictionaryAsync(rp => rp.RoomId, rp => rp.Amount);

            // Get all room photos for gallery
            var allRoomPhotos = roomsList
                .SelectMany(r => r.Photos)
                .OrderBy(p => p.SortOrder)
                .Select(p => p.Url)
                .ToList();

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
                RoomPhotoUrls = allRoomPhotos
            };

            // Pass search parameters to view
            ViewBag.Checkin = checkin;
            ViewBag.Checkout = checkout;
            ViewBag.Adults = adults;
            ViewBag.Children = children;
            ViewBag.Rooms = rooms;

            return View(vm);
        }

        // Search results page: list properties by destination and optional dates
        [HttpGet]
        public async Task<IActionResult> Search(string? destination, DateOnly? checkin, DateOnly? checkout, string? filters, int adults = 2, int children = 0, int rooms = 1)
        {
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

            var breakfastMap = await _db.PricePackages
                .Where(pp => propertyIds.Contains(pp.PropertyId))
                .ToDictionaryAsync(pp => pp.PropertyId, pp => pp.BreakfastIncluded);

            var starMap = pdList.ToDictionary(pd => pd.PropertyId, pd => pd.StarRating);

            var vm = new PublicSearchResultsViewModel
            {
                Destination = destination ?? string.Empty,
                Checkin = checkin,
                Checkout = checkout,
                Properties = results,
                MainPhotoUrls = photoMap,
                MinPriceByProperty = minPriceMap,
                StarRatingByProperty = starMap,
                BreakfastIncludedByProperty = breakfastMap,
                FeaturedRoomByProperty = roomMap
            };

            // Pass search parameters to view
            ViewBag.SearchDestination = destination;
            ViewBag.SearchCheckin = checkin;
            ViewBag.SearchCheckout = checkout;
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

    }
}


