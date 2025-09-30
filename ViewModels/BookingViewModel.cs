using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HotelBooking.ViewModels
{
    public class BookingViewModel
    {
        // Property and Room Info
        public int PropertyId { get; set; }
        public string PropertyName { get; set; }
        public int RoomId { get; set; }
        public string RoomName { get; set; }
        public decimal? RoomSize { get; set; }
        public string RoomSizeUnit { get; set; }
        public int CapacityAdults { get; set; }
        public int CapacityChildren { get; set; }
        public List<string> RoomPhotos { get; set; } = new List<string>();
        public List<string> RoomAmenities { get; set; } = new List<string>();

        // Booking Dates and Guests
        [Required(ErrorMessage = "Vui lòng chọn ngày nhận phòng")]
        [Display(Name = "Ngày nhận phòng")]
        public DateTime CheckIn { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn ngày trả phòng")]
        [Display(Name = "Ngày trả phòng")]
        public DateTime CheckOut { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số khách")]
        [Range(1, 10, ErrorMessage = "Số khách phải từ 1 đến 10")]
        [Display(Name = "Số khách")]
        public int Guests { get; set; }

        // Pricing
        public decimal PricePerNight { get; set; }
        public int TotalNights { get; set; }
        public decimal TotalPrice { get; set; }

        // Contact Information
        [Required(ErrorMessage = "Vui lòng nhập họ tên")]
        [Display(Name = "Họ tên")]
        public string FullName { get; set; }

        // Flag to indicate if booking is for the same person
        [Display(Name = "Tôi đặt chỗ cho chính mình")]
        public bool IsBookingForSelf { get; set; } = true;

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        [Display(Name = "Số điện thoại")]
        public string PhoneNumber { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập email")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        [Display(Name = "Email")]
        public string Email { get; set; }

        // Guest Information
        [Required(ErrorMessage = "Vui lòng nhập họ tên khách hàng")]
        [Display(Name = "Họ tên khách hàng")]
        public string GuestName { get; set; }

        // Special Requests
        [Display(Name = "Yêu cầu đặc biệt")]
        public string SpecialRequests { get; set; }

        public bool NonSmokingRoom { get; set; }
        public bool ConnectingRoom { get; set; }
        public bool HighFloor { get; set; }

        // Terms and Conditions
        [Required(ErrorMessage = "Vui lòng đồng ý với điều khoản và điều kiện")]
        public bool AgreeToTerms { get; set; }
    }
}
