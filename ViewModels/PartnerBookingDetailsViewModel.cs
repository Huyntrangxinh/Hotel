namespace HotelBooking.ViewModels
{
    public class PartnerBookingDetailsViewModel
    {
        public string BookingCode { get; set; } = string.Empty;
        public string PropertyName { get; set; } = string.Empty;
        public string RoomName { get; set; } = string.Empty;
        public string GuestName { get; set; } = string.Empty;
        public string ContactName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime CheckIn { get; set; }
        public DateTime CheckOut { get; set; }
        public int TotalNights { get; set; }
        public int Guests { get; set; }
        public decimal PricePerNight { get; set; }
        public decimal TotalPrice { get; set; }
        public string? DiscountCode { get; set; }
        public decimal DiscountAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? SpecialRequests { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool NonSmokingRoom { get; set; }
        public bool ConnectingRoom { get; set; }
        public bool HighFloor { get; set; }
    }
}

