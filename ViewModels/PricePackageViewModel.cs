namespace HotelBooking.ViewModels
{
    public class PricePackageViewModel
    {
        public int Id { get; set; }
        public string CancellationPolicy { get; set; } = string.Empty;
        public string CancellationPolicyDisplayName { get; set; } = string.Empty;
        public bool BreakfastIncluded { get; set; }
        public string BreakfastDisplayName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}

