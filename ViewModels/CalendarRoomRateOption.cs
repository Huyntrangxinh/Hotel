namespace HotelBooking.ViewModels
{
    public class CalendarRoomRateOption
    {
        public int RoomId { get; set; }
        public int RoomPriceId { get; set; }
        public string OptionName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "VND";
        public string? PolicyDisplayName { get; set; }
        public bool? BreakfastIncluded { get; set; }
    }
}

