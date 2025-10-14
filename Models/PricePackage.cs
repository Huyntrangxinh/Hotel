using System.ComponentModel.DataAnnotations;

namespace HotelBooking.Models
{
    public class PricePackage
    {
        public int Id { get; set; }
        
        [Required]
        public int PropertyId { get; set; }
        public Property Property { get; set; }
        
        [Required]
        [StringLength(50)]
        public string CancellationPolicy { get; set; } = string.Empty;
        
        [Required]
        public bool BreakfastIncluded { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        // Helper properties for display
        public string CancellationPolicyDisplayName
        {
            get
            {
                return CancellationPolicy switch
                {
                    "refund_1" => "Có thể hoàn tiền | cho đến 1 ngày trước Ngày làm thủ tục nhận phòng",
                    "refund_3" => "Có thể hoàn tiền | cho đến 3 ngày trước Ngày làm thủ tục nhận phòng",
                    "refund_7" => "Có thể hoàn tiền | cho đến 7 ngày trước Ngày làm thủ tục nhận phòng",
                    "non_refundable" => "Không thể hoàn tiền",
                    _ => CancellationPolicy
                };
            }
        }
        
        public string BreakfastDisplayName => BreakfastIncluded ? "Bao gồm" : "Không bao gồm";
    }
}

