using System.ComponentModel.DataAnnotations;

namespace HotelBooking.Models
{
    public class Destination
    {
        public int Id { get; set; }

        [Required, StringLength(120)]
        public string Name { get; set; } = string.Empty; // Tên hiển thị (Địa điểm)

        [StringLength(512)]
        public string? ImageUrl { get; set; } // Cho phép dán URL ảnh

        [StringLength(120)]
        public string? Country { get; set; } = "Việt Nam";
    }
}


