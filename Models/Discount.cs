using System.ComponentModel.DataAnnotations;

namespace HotelBooking.Models
{
    public class Discount
    {
        public int Id { get; set; }

        [Required, StringLength(64)]
        public string Code { get; set; } = string.Empty; // Mã giảm giá người dùng nhập

        [Required, StringLength(160)]
        public string Title { get; set; } = string.Empty; // Tiêu đề hiển thị

        [StringLength(512)]
        public string? Description { get; set; }

        // Chỉ dùng 1 trong 2 loại giảm giá
        [Range(0, 100)]
        public int? DiscountPercent { get; set; }

        [Range(0, 999999999)]
        public decimal? DiscountAmount { get; set; }

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public bool IsActive { get; set; } = true;

        [StringLength(512)]
        public string? ImageUrl { get; set; } // banner ưu đãi (tuỳ chọn)
    }
}



