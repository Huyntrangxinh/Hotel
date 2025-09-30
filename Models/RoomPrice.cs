using System.ComponentModel.DataAnnotations;

namespace HotelBooking.Models
{
    public class RoomPrice
    {
        public int Id { get; set; }

        [Required]
        public int PropertyId { get; set; }

        [Required]
        public int RoomId { get; set; }

        [Range(0, 1000000000)]
        public decimal Amount { get; set; }

        [StringLength(8)]
        public string Currency { get; set; } = "VND";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}



