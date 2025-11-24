namespace HotelBooking.ViewModels
{
    public class HotelReviewViewModel
    {
        public string Name { get; set; } = string.Empty;
        public decimal Rating { get; set; }
        public string Comment { get; set; } = string.Empty;
        public string TimeAgo { get; set; } = string.Empty;
        public string Initials { get; set; } = string.Empty;
        public bool IsBusiness { get; set; }
        public bool IsReal { get; set; } = false;
    }
}


