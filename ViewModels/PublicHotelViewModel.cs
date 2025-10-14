using System.Collections.Generic;
using HotelBooking.Models;

namespace HotelBooking.ViewModels
{
    public class PublicHotelViewModel
    {
        public Property Property { get; set; } = null!;
        public PropertyData? PropertyData { get; set; }
        public PricePackage? PricePackage { get; set; }
        public List<Room> Rooms { get; set; } = new();
        public Dictionary<int, decimal> RoomPrices { get; set; } = new();
        public List<string> PhotoUrls { get; set; } = new();
        public List<string> RoomPhotoUrls { get; set; } = new();
    }

    public class PublicSearchResultsViewModel
    {
        public string Destination { get; set; } = string.Empty;
        public DateOnly? Checkin { get; set; }
        public DateOnly? Checkout { get; set; }
        public List<Property> Properties { get; set; } = new();
        public Dictionary<int, string> MainPhotoUrls { get; set; } = new();
        public Dictionary<int, decimal> MinPriceByProperty { get; set; } = new();
        public Dictionary<int, int?> StarRatingByProperty { get; set; } = new();
        public Dictionary<int, bool> BreakfastIncludedByProperty { get; set; } = new();
        public Dictionary<int, HotelBooking.Models.Room> FeaturedRoomByProperty { get; set; } = new();
    }
}


