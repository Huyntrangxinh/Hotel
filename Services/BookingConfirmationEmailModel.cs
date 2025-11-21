using System;

namespace HotelBooking.Services
{
    public class BookingConfirmationEmailModel
    {
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string GuestName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string BookingCode { get; set; } = string.Empty;
        public string PropertyName { get; set; } = string.Empty;
        public string RoomName { get; set; } = string.Empty;
        public DateTime CheckIn { get; set; }
        public DateTime CheckOut { get; set; }
        public int TotalNights { get; set; }
        public int Guests { get; set; }
        public decimal TotalPrice { get; set; }
    }
}

