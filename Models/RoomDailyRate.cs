using System.ComponentModel.DataAnnotations;

namespace HotelBooking.Models
{
    public class RoomDailyRate
    {
        public int Id { get; set; }

        [Required]
        public int PropertyId { get; set; }

        [Required]
        public int RoomId { get; set; }

        [Required]
        public DateTime Date { get; set; }

        public decimal? Price { get; set; }

        public bool IsClosed { get; set; } = false;

        public int? MinStayNights { get; set; }
        public int? MaxStayNights { get; set; }
        public int? Allotment { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}


