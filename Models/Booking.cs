using System;

namespace HotelBooking.Models
{
    public enum BookingStatus
    {
        Pending = 0,
        Confirmed = 1,
        Cancelled = 2
    }

    public class Booking
    {
        public int Id { get; set; }

        // Relations
        public int PropertyId { get; set; }
        public int RoomId { get; set; }
        public string UserId { get; set; }

        // Customer contact
        public string FullName { get; set; }
        public string PhoneNumber { get; set; }
        public string Email { get; set; }
        public string GuestName { get; set; }

        // Stay info
        public DateTime CheckIn { get; set; }
        public DateTime CheckOut { get; set; }
        public int Guests { get; set; }

        // Special requests
        public bool NonSmokingRoom { get; set; }
        public bool ConnectingRoom { get; set; }
        public bool HighFloor { get; set; }
        public string SpecialRequests { get; set; }

        // Pricing snapshot
        public decimal PricePerNight { get; set; }
        public int TotalNights { get; set; }
        public decimal TotalPrice { get; set; }

        // Meta
        public BookingStatus Status { get; set; } = BookingStatus.Pending;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}







