using System.Threading.Tasks;

namespace HotelBooking.Services
{
    public interface IBookingEmailService
    {
        Task<bool> SendBookingConfirmationAsync(BookingConfirmationEmailModel model);
    }
}

