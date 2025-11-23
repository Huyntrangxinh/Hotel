using System;
using System.ComponentModel.DataAnnotations;

namespace HotelBooking.Models
{
    public class Review
    {
        public int Id { get; set; }
        
        [Required]
        public int BookingId { get; set; }
        public Booking Booking { get; set; }
        
        [Required]
        public int PropertyId { get; set; }
        public Property Property { get; set; }
        
        [Required]
        public string UserId { get; set; }
        public ApplicationUser User { get; set; }
        
        // Rating (1-5)
        [Range(1, 5)]
        public int Rating { get; set; }
        
        // Review text
        [MaxLength(2000)]
        public string? Comment { get; set; }
        
        // Sub-ratings (optional)
        [Range(1, 5)]
        public int? CleanlinessRating { get; set; }
        
        [Range(1, 5)]
        public int? ServiceRating { get; set; }
        
        [Range(1, 5)]
        public int? ValueRating { get; set; }
        
        [Range(1, 5)]
        public int? LocationRating { get; set; }
        
        // Meta
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}

