using HotelBooking.Models;

namespace HotelBooking.ViewModels
{
    public class PreviewViewModel
    {
        public int PropertyId { get; set; }
        public string PropertyName { get; set; } = string.Empty;
        public PropertyData? PropertyData { get; set; }
        public List<Room> Rooms { get; set; } = new();
        public PricePackage? PricePackage { get; set; }
        public Dictionary<int, decimal> RoomPrices { get; set; } = new();
    }
}

