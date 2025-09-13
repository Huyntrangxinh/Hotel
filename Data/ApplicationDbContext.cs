using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using HotelBooking.Models; // ApplicationUser, PartnerAccount, Property

namespace HotelBooking.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // DbSets
        public DbSet<PartnerAccount> PartnerAccounts => Set<PartnerAccount>();

        // 👉 THÊM DÒNG NÀY
        public DbSet<Property> Properties => Set<Property>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<PropertyData> PropertyData => Set<PropertyData>();
        public DbSet<Room> Rooms => Set<Room>();
        public DbSet<RoomBed> RoomBeds => Set<RoomBed>();
        public DbSet<RoomAmenity> RoomAmenities => Set<RoomAmenity>();
        public DbSet<RoomPhoto> RoomPhotos => Set<RoomPhoto>();
        public DbSet<PricePackage> PricePackages => Set<PricePackage>();

        // Helper methods for robust JSON deserialization
        private static List<string> DeserializeStringList(string? json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Trim() == "")
                return new List<string>();
            
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        private static List<decimal> DeserializeDecimalList(string? json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Trim() == "")
                return new List<decimal>();
            
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<List<decimal>>(json) ?? new List<decimal>();
            }
            catch
            {
                return new List<decimal>();
            }
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // PartnerAccount: mỗi user chỉ có 1
            builder.Entity<PartnerAccount>()
                   .HasIndex(p => p.UserId)
                   .IsUnique();

            builder.Entity<PartnerAccount>()
                   .HasOne(p => p.User)
                   .WithOne() // không cần navigation ngược
                   .HasForeignKey<PartnerAccount>(p => p.UserId)
                   .OnDelete(DeleteBehavior.Cascade);

            // 👉 THÊM MAPPING CHO Property
            builder.Entity<Property>()
                   .HasOne(p => p.User)
                   .WithMany() // chưa cần navigation ngược
                   .HasForeignKey(p => p.UserId)
                   .OnDelete(DeleteBehavior.Cascade);

            // Room relations
            builder.Entity<RoomBed>()
                   .HasOne<Room>()
                   .WithMany(r => r.Beds)
                   .HasForeignKey(b => b.RoomId)
                   .OnDelete(DeleteBehavior.Cascade);

            // Configure JSON serialization for RoomBed Lists with robust error handling
            builder.Entity<RoomBed>()
                   .Property(e => e.Types)
                   .HasConversion(
                       v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                       v => DeserializeStringList(v)
                   )
                   .Metadata.SetValueComparer(new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<string>>(
                       (c1, c2) => c1!.SequenceEqual(c2!),
                       c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                       c => c.ToList()));

            builder.Entity<RoomBed>()
                   .Property(e => e.Counts)
                   .HasConversion(
                       v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                       v => DeserializeDecimalList(v)
                   )
                   .Metadata.SetValueComparer(new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<decimal>>(
                       (c1, c2) => c1!.SequenceEqual(c2!),
                       c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                       c => c.ToList()));
            builder.Entity<RoomAmenity>()
                   .HasOne<Room>()
                   .WithMany(r => r.Amenities)
                   .HasForeignKey(a => a.RoomId)
                   .OnDelete(DeleteBehavior.Cascade);
            builder.Entity<RoomPhoto>()
                   .HasOne<Room>()
                   .WithMany(r => r.Photos)
                   .HasForeignKey(p => p.RoomId)
                   .OnDelete(DeleteBehavior.Cascade);

            // PricePackage relations
            builder.Entity<PricePackage>()
                   .HasOne(p => p.Property)
                   .WithMany()
                   .HasForeignKey(p => p.PropertyId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
