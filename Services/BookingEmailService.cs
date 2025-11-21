using System;
using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HotelBooking.Services
{
    public class BookingEmailService : IBookingEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<BookingEmailService> _logger;

        public BookingEmailService(IConfiguration configuration, ILogger<BookingEmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<bool> SendBookingConfirmationAsync(BookingConfirmationEmailModel model)
        {
            var emailBody = BuildBookingEmailBody(model);
            return await SendEmailAsync(model.Email, "Xác nhận đặt phòng thành công", emailBody);
        }

        private async Task<bool> SendEmailAsync(string email, string subject, string htmlContent)
        {
            try
            {
                var smtpServer = _configuration["EmailSettings:SmtpServer"];
                var smtpPortValue = _configuration["EmailSettings:SmtpPort"];
                var smtpUsername = _configuration["EmailSettings:SmtpUsername"];
                var smtpPassword = _configuration["EmailSettings:SmtpPassword"];
                var fromEmail = _configuration["EmailSettings:FromEmail"];
                var fromName = _configuration["EmailSettings:FromName"];

                if (string.IsNullOrWhiteSpace(smtpServer) ||
                    string.IsNullOrWhiteSpace(smtpPortValue) ||
                    string.IsNullOrWhiteSpace(smtpUsername) ||
                    string.IsNullOrWhiteSpace(smtpPassword) ||
                    string.IsNullOrWhiteSpace(fromEmail))
                {
                    _logger.LogError("Email configuration is missing or incomplete");
                    return false;
                }

                if (!int.TryParse(smtpPortValue, out var smtpPort))
                {
                    smtpPort = 587;
                }

                using var message = new MailMessage
                {
                    From = new MailAddress(fromEmail, string.IsNullOrWhiteSpace(fromName) ? "Hotel Booking" : fromName),
                    Subject = subject,
                    Body = htmlContent,
                    IsBodyHtml = true
                };

                message.To.Add(email);

                using var smtpClient = new SmtpClient(smtpServer, smtpPort)
                {
                    EnableSsl = true,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Credentials = new NetworkCredential(smtpUsername, smtpPassword)
                };

                await smtpClient.SendMailAsync(message);
                _logger.LogInformation("Booking confirmation email sent to {Email}", email);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to send booking confirmation email to {Email}", email);
                return false;
            }
        }

        private string BuildBookingEmailBody(BookingConfirmationEmailModel booking)
        {
            var builder = new StringBuilder();
            var culture = new CultureInfo("vi-VN");

            var hotel = string.IsNullOrWhiteSpace(booking.PropertyName)
                ? "Khách sạn bạn đã đặt"
                : booking.PropertyName;

            builder.AppendLine("<!DOCTYPE html>");
            builder.AppendLine("<html lang='vi'>");
            builder.AppendLine("<head>");
            builder.AppendLine("<meta charset='UTF-8' />");
            builder.AppendLine("<title>Xác nhận đặt phòng</title>");
            builder.AppendLine(@"<style>
                    body { font-family: 'Segoe UI', Arial, sans-serif; background:#f4f6fb; color:#1f2937; margin:0; padding:0; }
                    .wrapper { max-width: 640px; margin: 0 auto; padding: 32px 16px; }
                    .card { background:#ffffff; border-radius:16px; overflow:hidden; box-shadow:0 20px 45px rgba(15,23,42,0.08); }
                    .hero { background: linear-gradient(135deg,#2563eb,#1d4ed8); color:white; padding:32px; text-align:center; }
                    .hero h1 { margin:0; font-size:26px; }
                    .hero p { margin:8px 0 0; opacity:0.9; }
                    .content { padding:32px; }
                    .section-title { text-transform:uppercase; font-size:12px; letter-spacing:0.08em; color:#94a3b8; margin-bottom:12px; }
                    .info-card { border:1px solid #e2e8f0; border-radius:12px; padding:16px 20px; margin-bottom:18px; }
                    .info-row { display:flex; justify-content:space-between; margin-bottom:8px; font-size:14px; color:#475569; }
                    .info-row span:last-child { font-weight:600; color:#0f172a; }
                    .badge { display:inline-flex; align-items:center; gap:6px; background:#e0f2fe; color:#0369a1; padding:6px 12px; border-radius:999px; font-size:13px; margin-bottom:16px; }
                    .footer { padding:24px 32px 32px; text-align:center; color:#94a3b8; font-size:13px; }
                    .cta-button { display:inline-block; margin-top:20px; padding:14px 28px; background:#2563eb; color:white; border-radius:10px; text-decoration:none; font-weight:600; }
                    .next-steps li { margin-bottom:8px; }
                    ul { padding-left: 20px; }
                </style>");
            builder.AppendLine("</head>");
            builder.AppendLine("<body>");
            builder.AppendLine("<div class='wrapper'>");
            builder.AppendLine("<div class='card'>");
            builder.AppendLine("<div class='hero'>");
            builder.AppendLine($"<div class='badge'>🏨 {hotel}</div>");
            builder.AppendLine("<h1>Xác nhận đặt phòng thành công!</h1>");
            builder.AppendLine("<p>Cảm ơn bạn đã tin tưởng và lựa chọn chúng tôi</p>");
            builder.AppendLine("</div>");
            builder.AppendLine("<div class='content'>");
            builder.AppendLine($"<p>Xin chào <strong>{booking.FullName}</strong>,</p>");
            builder.AppendLine("<p>Đơn đặt phòng của bạn đã được xác nhận. Dưới đây là thông tin chi tiết:</p>");

            builder.AppendLine("<div class='section-title'>THÔNG TIN ĐẶT PHÒNG</div>");
            builder.AppendLine("<div class='info-card'>");
            builder.AppendLine($"<div class='info-row'><span>Mã đặt phòng</span><span>{booking.BookingCode}</span></div>");
            builder.AppendLine($"<div class='info-row'><span>Khách sạn</span><span>{hotel}</span></div>");
            builder.AppendLine($"<div class='info-row'><span>Loại phòng</span><span>{booking.RoomName}</span></div>");
            builder.AppendLine($"<div class='info-row'><span>Ngày nhận phòng</span><span>{booking.CheckIn:dd/MM/yyyy}</span></div>");
            builder.AppendLine($"<div class='info-row'><span>Ngày trả phòng</span><span>{booking.CheckOut:dd/MM/yyyy}</span></div>");
            builder.AppendLine($"<div class='info-row'><span>Số đêm</span><span>{booking.TotalNights} đêm</span></div>");
            builder.AppendLine($"<div class='info-row'><span>Số khách</span><span>{booking.Guests} người</span></div>");
            builder.AppendLine($"<div class='info-row'><span>Tổng tiền</span><span style='color:#dc2626'>{booking.TotalPrice.ToString("c0", culture)}</span></div>");
            builder.AppendLine("</div>");

                builder.AppendLine("<div class='section-title'>THÔNG TIN LIÊN HỆ</div>");
                builder.AppendLine("<div class='info-card'>");
                builder.AppendLine($"<div class='info-row'><span>Khách lưu trú</span><span>{(!string.IsNullOrWhiteSpace(booking.GuestName) ? booking.GuestName : booking.FullName)}</span></div>");
                builder.AppendLine($"<div class='info-row'><span>Số điện thoại</span><span>{booking.PhoneNumber}</span></div>");
                builder.AppendLine($"<div class='info-row'><span>Email</span><span>{booking.Email}</span></div>");
                builder.AppendLine("</div>");

            builder.AppendLine("<div class='section-title'>BƯỚC TIẾP THEO</div>");
            builder.AppendLine("<div class='info-card next-steps'>");
            builder.AppendLine("<ul>");
            builder.AppendLine("<li>Vui lòng lưu email này làm xác nhận khi nhận phòng.</li>");
            builder.AppendLine("<li>Chuẩn bị CMND/CCCD hoặc hộ chiếu khớp với người đặt.</li>");
            builder.AppendLine("<li>Đến khách sạn theo khung giờ nhận phòng đã ghi.</li>");
            builder.AppendLine("<li>Nếu cần hỗ trợ khẩn cấp, trả lời email này hoặc gọi hotline 1900 1234.</li>");
            builder.AppendLine("</ul>");
            builder.AppendLine("</div>");

            builder.AppendLine($"<p>Chúc bạn có kỳ nghỉ tuyệt vời tại {hotel}!</p>");
            builder.AppendLine("<a href='https://www.citadines.com' class='cta-button'>Xem thêm ưu đãi</a>");
            builder.AppendLine("</div>");
            builder.AppendLine("<div class='footer'>");
            builder.AppendLine("<p>Citadines Marina Hạ Long • Bãi Cháy, TP. Hạ Long</p>");
            builder.AppendLine("<p>Hotline: 1900 1234 • Email: support@hotelbooking.com</p>");
            builder.AppendLine("</div>");
            builder.AppendLine("</div>");
            builder.AppendLine("</div>");
            builder.AppendLine("</body>");
            builder.AppendLine("</html>");

            return builder.ToString();
        }
    }
}

