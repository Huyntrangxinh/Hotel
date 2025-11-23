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
        [Required(ErrorMessage = "Please Select Check In Date")]
        [Display(Name = "Ngày nhận phòng")]
        public DateTime CheckIn { get; set; }

        [Required(ErrorMessage = "Please Select Check Out Date")]
        [Display(Name = "Ngày trả phòng")]
        public DateTime CheckOut { get; set; }

        [Required(ErrorMessage = "Please Enter Guest Count")]
        [Range(1, 10, ErrorMessage = "Guest Count Range")]
        [Display(Name = "Số khách")]
        public int Guests { get; set; }

        // Pricing
        public decimal PricePerNight { get; set; }
        public int TotalNights { get; set; }
        public decimal TotalPrice { get; set; }
        public int? RoomPriceId { get; set; }  // ID của RoomPrice được chọn
        public int? PricePackageId { get; set; }  // ID của PricePackage được chọn

        // Contact Information
        [Required(ErrorMessage = "Please Enter Full Name")]
        [Display(Name = "Họ tên")]
        public string FullName { get; set; }

        // Flag to indicate if booking is for the same person
        [Display(Name = "Tôi đặt chỗ cho chính mình")]
        public bool IsBookingForSelf { get; set; } = true;

        [Required(ErrorMessage = "Please Enter Phone Number")]
        [Phone(ErrorMessage = "Invalid Phone Number")]
        [Display(Name = "Số điện thoại")]
        public string PhoneNumber { get; set; }

        [Required(ErrorMessage = "Please Enter Email")]
        [EmailAddress(ErrorMessage = "Invalid Email")]
        [Display(Name = "Email")]
        public string Email { get; set; }

        // Guest Information
        [Required(ErrorMessage = "Please Enter Guest Name")]
        [Display(Name = "Họ tên khách hàng")]
        public string GuestName { get; set; }

        // Special Requests
        [Display(Name = "Yêu cầu đặc biệt")]
        public string SpecialRequests { get; set; }

        public bool NonSmokingRoom { get; set; }
        public bool ConnectingRoom { get; set; }
        public bool HighFloor { get; set; }

        // Discount information
        public string DiscountCode { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal DiscountPercentage { get; set; }

        // Terms and Conditions
        [Required(ErrorMessage = "Please Agree To Terms")]
        public bool AgreeToTerms { get; set; }
    }
}
