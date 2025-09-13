using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HotelBooking.Models
{
    public class Room
    {
        [Key]
        public int Id { get; set; }
        public int PropertyId { get; set; }

        [Required]
        [MaxLength(128)]
        public string RoomType { get; set; } = string.Empty;

        [Required]
        [MaxLength(256)]
        public string Name { get; set; } = string.Empty;

        public int? Size { get; set; }
        [MaxLength(8)]
        public string SizeUnit { get; set; } = "m2";
        public bool SmokingAllowed { get; set; } = true;
        public int Quantity { get; set; }
        public bool IsSingleBedroom { get; set; } = true;
        public int CapacityAdults { get; set; }
        public int CapacityChildren { get; set; }
        public bool AllowChildren { get; set; }
        public bool AllowExtraBed { get; set; }
        public decimal? SecurityDeposit { get; set; }

        public List<RoomBed> Beds { get; set; } = new();
        public List<RoomAmenity> Amenities { get; set; } = new();
        public List<RoomPhoto> Photos { get; set; } = new();
    }

    public class RoomBed
    {
        [Key]
        public int Id { get; set; }
        public int RoomId { get; set; }
        public List<string> Types { get; set; } = new(); // Danh sách các loại giường
        public List<decimal> Counts { get; set; } = new(); // Danh sách số lượng tương ứng với từng loại giường
        public int BedroomIndex { get; set; } = 0; // 0-based index để phân biệt phòng ngủ

        // Helper properties để tương thích với code cũ
        [NotMapped]
        public string Type 
        { 
            get => Types.FirstOrDefault() ?? string.Empty;
            set 
            {
                if (Types.Count == 0)
                    Types.Add(value);
                else
                    Types[0] = value;
            }
        }

        [NotMapped]
        public decimal Count 
        { 
            get => Counts.FirstOrDefault();
            set 
            {
                if (Counts.Count == 0)
                    Counts.Add(value);
                else
                    Counts[0] = value;
            }
        }

        // Helper method để lấy tất cả bed items từ Lists
        public List<BedItemData> GetAllBedItems()
        {
            var result = new List<BedItemData>();
            for (int i = 0; i < Math.Min(Types.Count, Counts.Count); i++)
            {
                result.Add(new BedItemData 
                { 
                    Type = Types[i], 
                    Count = Counts[i], 
                    BedroomIndex = BedroomIndex 
                });
            }
            return result;
        }

        // Helper method để thêm bed item
        public void AddBedItem(string type, decimal count)
        {
            Types.Add(type);
            Counts.Add(count);
        }
    }

    // Helper class để chứa dữ liệu bed item
    public class BedItemData
    {
        public string Type { get; set; } = string.Empty;
        public decimal Count { get; set; }
        public int BedroomIndex { get; set; } = 0;
    }

    public class RoomAmenity
    {
        [Key]
        public int Id { get; set; }
        public int RoomId { get; set; }
        [MaxLength(128)]
        public string Name { get; set; } = string.Empty;
    }

    public class RoomPhoto
    {
        [Key]
        public int Id { get; set; }
        public int RoomId { get; set; }
        [Required]
        [MaxLength(1024)]
        public string Url { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        [MaxLength(32)]
        public string Category { get; set; } = "additional"; // bedroom/bathroom/additional
    }
}
