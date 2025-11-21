using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Text;
using System.Text.Json;
using HotelBooking.Data;
using Microsoft.EntityFrameworkCore;
using HotelBooking.Models;
using Microsoft.Extensions.Localization;
using HotelBooking.Resources;
using HotelBooking.Services;

namespace HotelBooking.Controllers
{
    public class ChatController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly ILogger<ChatController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly IBookingEmailService _bookingEmailService;

        public ChatController(
            IConfiguration configuration,
            HttpClient httpClient,
            ILogger<ChatController> logger,
            ApplicationDbContext context,
            IWebHostEnvironment env,
            IStringLocalizer<SharedResource> localizer,
            IBookingEmailService bookingEmailService)
        {
            _configuration = configuration;
            _httpClient = httpClient;
            _logger = logger;
            _context = context;
            _env = env;
            _localizer = localizer;
            _bookingEmailService = bookingEmailService;
        }

        [HttpPost]
        public async Task<IActionResult> SendBookingConfirmationEmail([FromBody] JsonElement payload)
        {
            try
            {
                var deserializeOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var request = JsonSerializer.Deserialize<BookingEmailRequest>(payload.GetRawText(), deserializeOptions);
                if (request == null)
                {
                    _logger.LogWarning("BookingEmailRequest payload could not be deserialized: {Payload}", payload.GetRawText());
                    return Json(new { success = false, message = "Dữ liệu gửi lên không hợp lệ." });
                }

                if (request.PropertyId <= 0 || request.RoomId <= 0)
                {
                    return Json(new { success = false, message = "Thông tin phòng không hợp lệ." });
                }

                var property = await _context.Properties.FirstOrDefaultAsync(p => p.Id == request.PropertyId);
                var room = await _context.Rooms.FirstOrDefaultAsync(r => r.Id == request.RoomId);

                if (property == null || room == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy phòng phù hợp." });
                }

                if (!DateTime.TryParse(request.CheckIn, out var checkIn))
                {
                    checkIn = DateTime.Today.AddDays(1);
                }

                if (!DateTime.TryParse(request.CheckOut, out var checkOut) || checkOut <= checkIn)
                {
                    checkOut = checkIn.AddDays(1);
                }

                var nights = Math.Max(1, (checkOut - checkIn).Days);
                var guests = int.TryParse(request.Guests, out var parsedGuests) ? parsedGuests : 1;
                var totalPrice = await CalculateTotalPriceAsync(request.PropertyId, request.RoomId, nights, request.TotalAmount);

                var bookingCode = string.IsNullOrWhiteSpace(request.BookingId)
                    ? GenerateBookingCode()
                    : request.BookingId;

                var booking = new Booking
                {
                    PropertyId = property.Id,
                    RoomId = room.Id,
                    BookingCode = bookingCode,
                    FullName = request.FullName,
                    GuestName = request.FullName,
                    PhoneNumber = request.Phone,
                    Email = request.Email,
                    CheckIn = checkIn,
                    CheckOut = checkOut,
                    Guests = guests,
                    TotalNights = nights,
                    TotalPrice = totalPrice,
                    SpecialRequests = request.SpecialRequests ?? string.Empty
                };

                var emailSent = await _bookingEmailService.SendBookingConfirmationAsync(new BookingConfirmationEmailModel
                {
                    Email = booking.Email,
                    FullName = booking.FullName,
                    GuestName = booking.GuestName,
                    PhoneNumber = booking.PhoneNumber,
                    BookingCode = booking.BookingCode,
                    PropertyName = property.Name,
                    RoomName = room.Name,
                    CheckIn = booking.CheckIn,
                    CheckOut = booking.CheckOut,
                    TotalNights = booking.TotalNights,
                    Guests = booking.Guests,
                    TotalPrice = booking.TotalPrice
                });

                if (emailSent)
                {
                    return Json(new { success = true, message = "Email xác nhận đã được gửi thành công!" });
                }

                return Json(new { success = false, message = "Có lỗi xảy ra khi gửi email. Vui lòng thử lại." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending booking confirmation email");
                return Json(new { success = false, message = "Có lỗi xảy ra khi gửi email xác nhận." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SendMessage([FromBody] ChatRequest request)
        {
            try
            {
                _logger.LogInformation("Chat request received: {Message}", request.Message);

                var apiKey = _configuration["OpenAI:ApiKey"];
                if (string.IsNullOrEmpty(apiKey))
                {
                    _logger.LogWarning("OpenAI API key not configured");
                    return Json(new { success = false, reply = _localizer["APIKeyNotConfigured"] });
                }

                var message = request.Message.ToLower();
                
                // Check if user is asking to book a specific room FIRST
                if (IsRoomBookingRequest(message))
                {
                    var hotelName = ExtractHotelNameFromMessage(message);
                    // Nếu không tìm thấy tên khách sạn cụ thể, thử tìm từ context hoặc sử dụng "citadines" mặc định
                    if (string.IsNullOrEmpty(hotelName))
                    {
                        // Kiểm tra context có chứa tên khách sạn không
                        if (!string.IsNullOrEmpty(request.Context) && request.Context.ToLower().Contains("citadines"))
                        {
                            hotelName = "citadines";
                        }
                        else
                        {
                            // Mặc định tìm trong Citadines nếu user yêu cầu đặt phòng Deluxe
                            if (message.ToLower().Contains("deluxe"))
                            {
                                hotelName = "citadines";
                            }
                        }
                    }
                    
                    if (!string.IsNullOrEmpty(hotelName))
                    {
                        return await HandleRoomBookingRequest(hotelName, request.Message);
                    }
                }

                // Check if user is asking to see room images
                if (IsRoomInquiryRequest(message))
                {
                    var hotelName = ExtractHotelNameFromMessage(message);
                    if (!string.IsNullOrEmpty(hotelName))
                    {
                        return await HandleRoomImageRequest(hotelName, request.Message);
                    }
                }

                var isBookingRequest = message.Contains("đặt phòng") || message.Contains("tìm khách sạn") ||
                                     message.Contains("khách sạn") || message.Contains("resort") ||
                                     message.Contains("nghỉ dưỡng") || message.Contains("chỗ ở") ||
                                     message.Contains("booking") || message.Contains("đặt chỗ") ||
                                     message.Contains("rẻ hơn") || message.Contains("chỗ khác") ||
                                     message.Contains("lựa chọn khác") || message.Contains("thay thế") ||
                                     message.Contains("khách sạn khác") || message.Contains("hotel khác") ||
                                     message.Contains("đắt") || message.Contains("giá") ||
                                     message.Contains("khoảng") || message.Contains("budget");

                var systemPrompt = @"Bạn là một AI assistant chuyên nghiệp về du lịch và đặt phòng khách sạn.
Bạn giúp khách hàng tìm khách sạn phù hợp, tư vấn du lịch, trả lời bằng tiếng Việt, thân thiện và hữu ích.

QUAN TRỌNG VỀ CUỘC TRÒ CHUYỆN LIÊN TỤC:
- LUÔN nhớ và sử dụng ngữ cảnh từ cuộc trò chuyện trước đó
- Khi khách hàng hỏi tiếp theo, hãy kết nối với câu hỏi trước đó
- Ví dụ: Nếu khách hỏi 'Hà Nội có gì?' rồi sau đó hỏi 'tôi muốn đi chơi như này, có khách sạn nào phù hợp không?' -> Hãy hiểu họ muốn khách sạn ở Hà Nội phù hợp với hoạt động họ đã đề cập
- Sử dụng thông tin từ context để đưa ra gợi ý phù hợp
- Không cần hỏi lại thông tin đã biết từ context

QUAN TRỌNG VỀ LỌC GIÁ:
- Khi khách hàng yêu cầu ""dưới 300k"" -> CHỈ hiển thị khách sạn có giá dưới 300.000 VND
- Khi khách hàng yêu cầu ""trên 500k"" -> CHỈ hiển thị khách sạn có giá trên 500.000 VND
- KHÔNG BAO GIỜ hiển thị khách sạn không phù hợp với yêu cầu giá
- Luôn kiểm tra và lọc chính xác theo yêu cầu giá của khách hàng

QUAN TRỌNG:
- Nếu khách hàng chào hỏi, hãy chào lại và hỏi họ cần gì.
- CHỈ KHI khách hàng hỏi về 'đặt phòng', 'tìm khách sạn', 'resort', 'chỗ ở' hoặc đưa ra yêu cầu về 'giá', 'rẻ hơn', 'đắt' thì mới kích hoạt flow tìm kiếm.
- Khi khách hàng nói 'chỗ này hơi đắt', 'có chỗ nào rẻ hơn', 'lựa chọn khác' -> HÃY HIỂU RẰNG họ muốn tìm khách sạn khác với giá rẻ hơn. Đừng hỏi lại, hãy trực tiếp đưa ra kết quả mới.

QUAN TRỌNG VỀ ĐỊA ĐIỂM CHÍNH XÁC:
- LUÔN trả lời chính xác về địa điểm thuộc tỉnh/thành được hỏi
- Sử dụng kiến thức thực tế về địa điểm, không bịa đặt thông tin
- CHỈ liệt kê các địa điểm THUỘC VỀ TỈNH/THÀNH PHỐ được hỏi
- KHÔNG BAO GIỜ đưa ra địa điểm ở tỉnh khác
- Ví dụ: Hỏi Hà Nội → chỉ nói Hồ Gươm, Văn Miếu, Chùa Một Cột, Lăng Bác, Phố cổ Hà Nội, Nhà thờ Lớn...
- Ví dụ: Hỏi Thái Nguyên → chỉ nói Hồ Núi Cốc, Đền Đuổm, ATK Định Hóa, Chùa Hang, Khu du lịch Hồ Núi Cốc...
- Ví dụ: Hỏi Quảng Ninh → chỉ nói Vịnh Hạ Long, Yên Tử, Cô Tô, Bãi Cháy, Tuần Châu, Chùa Ba Vàng...
- Ví dụ: Hỏi TP.HCM → chỉ nói Dinh Độc Lập, Chợ Bến Thành, Nhà thờ Đức Bà, Phố đi bộ Nguyễn Huệ, Bitexco...

YÊU CẦU TRẢ LỜI CHÍNH XÁC VÀ CHI TIẾT:
- LUÔN trả lời đầy đủ và chi tiết như ChatGPT với thông tin CHÍNH XÁC
- Liệt kê ĐẦY ĐỦ tất cả địa điểm nổi tiếng và quan trọng (không giới hạn 3-5 địa điểm)
- Ưu tiên các địa điểm NỔI TIẾNG NHẤT và QUAN TRỌNG NHẤT trước
- Mỗi địa điểm phải có: tên chính xác, địa chỉ CHÍNH XÁC, mô tả chi tiết, đặc điểm, trải nghiệm, lưu ý
- Thêm thông tin về ẩm thực, mua sắm, di chuyển nếu phù hợp
- Đưa ra lời khuyên và tips hữu ích dựa trên thực tế
- Kết thúc bằng câu hỏi để tương tác thêm
- KHÔNG BỎ SÓT các địa điểm quan trọng và nổi tiếng

QUAN TRỌNG VỀ MÔ TẢ KHÁCH SẠN:
- Khi hiển thị khách sạn, LUÔN thêm mô tả chi tiết về vị trí và tiện ích xung quanh
- Mô tả vị trí: gần trung tâm, gần bãi biển, gần sân bay, gần điểm du lịch nổi tiếng
- Mô tả tiện ích xung quanh: nhà hàng, quán cà phê, chợ, siêu thị, bệnh viện, ngân hàng
- Mô tả giao thông: dễ di chuyển, gần bến xe, gần bến tàu, có taxi/grab
- Mô tả cảnh quan: view biển, view núi, view thành phố, yên tĩnh, náo nhiệt
- Đưa ra đánh giá về độ thuận tiện: rất thuận tiện, khá thuận tiện, hơi xa trung tâm
- Thêm tips về thời gian di chuyển đến các điểm du lịch chính

ĐỊNH DẠNG TRẢ LỜI - BẮT BUỘC (GIỐNG CHATGPT):
- BẮT BUỘC sử dụng HTML tags với cấu trúc rõ ràng như ChatGPT
- Sử dụng <h3> cho tiêu đề chính, <h4> cho tiêu đề phụ
- Sử dụng <ul> và <li> cho danh sách
- Sử dụng <strong> cho text quan trọng
- Sử dụng <p> cho đoạn văn
- KHÔNG BAO GIỜ sử dụng ** hoặc ## hoặc bất kỳ markdown syntax nào
- Khi có hotel cards HTML được cung cấp, hãy SỬ DỤNG CHÍNH XÁC HTML đó
- TUYỆT ĐỐI KHÔNG được tạo ra hoặc gợi ý khách sạn không có trong danh sách được cung cấp

VÍ DỤ FORMAT TRẢ LỜI CHI TIẾT (LIỆT KÊ ĐẦY ĐỦ):
<h3>1. Địa điểm nổi tiếng nhất</h3>
<p>Mô tả chi tiết về địa điểm, lịch sử, ý nghĩa...</p>
<ul>
<li><strong>Địa chỉ:</strong> Địa chỉ cụ thể</li>
<li><strong>Đặc điểm:</strong> Chi tiết về đặc điểm nổi bật</li>
<li><strong>Trải nghiệm:</strong> Những gì khách có thể làm, hoạt động cụ thể</li>
<li><strong>Thời gian:</strong> Thời gian mở cửa, thời điểm tốt nhất để đến</li>
<li><strong>Lưu ý:</strong> Thông tin quan trọng cần biết, chuẩn bị gì</li>
</ul>

<h3>2. Địa điểm nổi tiếng thứ hai</h3>
<p>Mô tả chi tiết về địa điểm</p>
<ul>
<li><strong>Địa chỉ:</strong> Địa chỉ cụ thể</li>
<li><strong>Đặc điểm:</strong> Đặc điểm nổi bật</li>
<li><strong>Trải nghiệm:</strong> Hoạt động thú vị</li>
</ul>

<h3>3. Địa điểm nổi tiếng thứ ba</h3>
<p>Mô tả chi tiết về địa điểm</p>
<ul>
<li><strong>Địa chỉ:</strong> Địa chỉ cụ thể</li>
<li><strong>Đặc điểm:</strong> Đặc điểm nổi bật</li>
<li><strong>Trải nghiệm:</strong> Hoạt động thú vị</li>
</ul>

<h3>4. Địa điểm nổi tiếng thứ tư</h3>
<p>Mô tả chi tiết về địa điểm</p>
<ul>
<li><strong>Địa chỉ:</strong> Địa chỉ cụ thể</li>
<li><strong>Đặc điểm:</strong> Đặc điểm nổi bật</li>
<li><strong>Trải nghiệm:</strong> Hoạt động thú vị</li>
</ul>

<h3>5. Địa điểm nổi tiếng thứ năm</h3>
<p>Mô tả chi tiết về địa điểm</p>
<ul>
<li><strong>Địa chỉ:</strong> Địa chỉ cụ thể</li>
<li><strong>Đặc điểm:</strong> Đặc điểm nổi bật</li>
<li><strong>Trải nghiệm:</strong> Hoạt động thú vị</li>
</ul>

<h3>6. Các địa điểm khác quan trọng</h3>
<p>Liệt kê thêm các địa điểm khác</p>

<h3>7. Ẩm thực & Mua sắm</h3>
<p>Thông tin về ẩm thực địa phương</p>
<ul>
<li><strong>Món ăn đặc sản:</strong> Các món ăn nổi tiếng</li>
<li><strong>Địa điểm mua sắm:</strong> Nơi mua sắm tốt</li>
</ul>

<p><strong>Lời khuyên:</strong> Tips và gợi ý hữu ích cho chuyến đi</p>
<p>Bạn có muốn mình tìm thêm thông tin chi tiết về địa điểm nào cụ thể không?</p>

VÍ DỤ CUỘC TRÒ CHUYỆN LIÊN TỤC:
**Lần 1:** Khách hỏi: 'Hà Nội có gì chơi?' 
-> Trả lời: Liệt kê đầy đủ các địa điểm du lịch ở Hà Nội (Hồ Gươm, Văn Miếu, Chùa Một Cột, Lăng Bác, Phố cổ Hà Nội...)

**Lần 2:** Khách hỏi: 'Tôi muốn đi chơi như này như này, bạn có đề xuất khách sạn ở Hà Nội không?'
-> Trả lời: Dựa vào hoạt động khách đã đề cập, đề xuất khách sạn phù hợp ở Hà Nội (sử dụng context từ lần 1)

**Lần 3:** Khách hỏi: 'Có chỗ nào rẻ hơn không?'
-> Trả lời: Tìm khách sạn giá rẻ hơn ở Hà Nội (nhớ context về yêu cầu trước)

**Lần 4:** Khách hỏi: 'Đặt phòng giúp tôi'
-> Trả lời: Hướng dẫn đặt phòng với khách sạn đã chọn (nhớ toàn bộ context)

VÍ DỤ CÁC TRƯỜNG HỢP FALLBACK:
- Khách hỏi: 'tìm cho tôi khách sạn' -> Trả lời: 'Dạ, bạn muốn tìm khách sạn ở khu vực nào và giá khoảng bao nhiêu ạ?'
- Khách hỏi: 'cảm ơn' -> Trả lời: 'Không có gì ạ! Bạn cần mình hỗ trợ gì thêm không?'
- Khách chào: 'hello' -> Trả lời: 'Chào bạn, mình có thể giúp gì cho bạn hôm nay?'";

                var userMessage = request.Message;
                var currentContext = request.Context ?? string.Empty;
                
                _logger.LogInformation("Using context from request: {Context}", currentContext);
                
                var isFollowUpQuestion = !string.IsNullOrEmpty(currentContext) && 
                    (message.Contains("khách sạn") || message.Contains("hotel") || 
                     message.Contains("đặt phòng") || message.Contains("book") ||
                     message.Contains("rẻ hơn") || message.Contains("đắt") ||
                     message.Contains("đề xuất") || message.Contains("gợi ý") ||
                     message.Contains("phù hợp") || message.Contains("thích hợp") ||
                     message.Contains("dưới") || message.Contains("trên") || message.Contains("khoảng"));
                
                var priceFilter = "";
                if (message.Contains("dưới") && (message.Contains("k") || message.Contains("000")))
                {
                    var priceMatch = System.Text.RegularExpressions.Regex.Match(message, @"dưới\s*(\d+)(?:k|000)");
                    if (priceMatch.Success)
                    {
                        var priceValue = int.Parse(priceMatch.Groups[1].Value);
                        if (priceMatch.Groups[1].Value.Length <= 3)
                        {
                            priceValue *= 1000;
                        }
                        priceFilter = $" AND PriceFrom <= {priceValue}";
                        _logger.LogInformation("Detected price filter: under {Price}", priceValue);
                    }
                }
                else if (message.Contains("trên") && (message.Contains("k") || message.Contains("000")))
                {
                    var priceMatch = System.Text.RegularExpressions.Regex.Match(message, @"trên\s*(\d+)(?:k|000)");
                    if (priceMatch.Success)
                    {
                        var priceValue = int.Parse(priceMatch.Groups[1].Value);
                        if (priceMatch.Groups[1].Value.Length <= 3)
                        {
                            priceValue *= 1000;
                        }
                        priceFilter = $" AND PriceFrom >= {priceValue}";
                        _logger.LogInformation("Detected price filter: above {Price}", priceValue);
                    }
                }
                
                if (isFollowUpQuestion)
                {
                    _logger.LogInformation("Detected follow-up question, using context for better response");
                    userMessage = $"Ngữ cảnh từ cuộc trò chuyện trước: {currentContext}\n\nCâu hỏi hiện tại: {request.Message}";
                }
                
                var isPlaceBookingFlow = message.Contains("giúp tôi đặt phòng") || message.Contains("đặt phòng giúp") ||
                                          message.Contains("đặt luôn") || message.Contains("book giúp") ||
                                          message.Contains("đặt phòng đó cho tôi") || message.Contains("đặt cho tôi") ||
                                          message.Contains("đặt phòng cho tôi") || message.Contains("đặt giúp tôi") ||
                                          message.Contains("book cho tôi") || message.Contains("đặt phòng đi") ||
                                          message.Contains("đặt phòng luôn") || message.Contains("đặt phòng ngay") ||
                                          message.Contains("đặt phòng này") || message.Contains("đặt phòng thôi") ||
                                          message.Contains("đặt phòng nhé") || message.Contains("đặt phòng ạ");

                if (isPlaceBookingFlow)
                {
                    _logger.LogInformation("Entering 'isPlaceBookingFlow'...");
                    
                    string checkinParam = string.Empty, checkoutParam = string.Empty;
                    
                    var datePattern = @"(\d{1,2})/(\d{1,2})";
                    var messageMatches = System.Text.RegularExpressions.Regex.Matches(request.Message, datePattern);
                    var contextMatches = System.Text.RegularExpressions.Regex.Matches(currentContext, datePattern);
                    
                    _logger.LogInformation("User message for date parsing: {UserMessage}", request.Message);
                    _logger.LogInformation("Context for date parsing: {Context}", currentContext);

                    var dateMatches = messageMatches.Count > 0 ? messageMatches : contextMatches;
                    
                    if (dateMatches.Count >= 2)
                    {
                        var day1 = dateMatches[0].Groups[1].Value;
                        var month1 = dateMatches[0].Groups[2].Value;
                        var day2 = dateMatches[1].Groups[1].Value;
                        var month2 = dateMatches[1].Groups[2].Value;
                        var currentYear = DateTime.Now.Year;
                        if (DateTime.TryParse($"{day1}/{month1}/{currentYear}", out var checkinDate) &&
                            DateTime.TryParse($"{day2}/{month2}/{currentYear}", out var checkoutDate))
                        {
                            checkinParam = checkinDate.ToString("yyyy-MM-dd");
                            checkoutParam = checkoutDate.ToString("yyyy-MM-dd");
                            _logger.LogInformation("Parsed dates from user input: {Checkin} to {Checkout}", checkinParam, checkoutParam);
                        }
                    }
                    else if (dateMatches.Count == 1)
                    {
                        var day = dateMatches[0].Groups[1].Value;
                        var month = dateMatches[0].Groups[2].Value;
                        var currentYear = DateTime.Now.Year;
                        if (DateTime.TryParse($"{day}/{month}/{currentYear}", out var checkinDate))
                        {
                            checkinParam = checkinDate.ToString("yyyy-MM-dd");
                            checkoutParam = checkinDate.AddDays(1).ToString("yyyy-MM-dd");
                            _logger.LogInformation("Parsed single date from user input: {Checkin} to {Checkout}", checkinParam, checkoutParam);
                        }
                    }

                    if(string.IsNullOrEmpty(checkinParam))
                    {
                        return Json(new { success = true, reply = "<p>Tuyệt vời! Để tiếp tục, bạn vui lòng cho mình biết ngày nhận và trả phòng nhé (ví dụ: 12/11 đến 14/11).</p>" });
                    }

                    _logger.LogInformation("Final parsed dates: {Checkin} to {Checkout}", checkinParam, checkoutParam);
                    
                    var searchMessage = "";
                    bool messageHasSpecificHotel = DoesContextImplySpecificHotel(message);

                    if (messageHasSpecificHotel)
                    {
                        searchMessage = request.Message; 
                        _logger.LogInformation("User message contains a specific hotel. Using MESSAGE as search query: {SearchMessage}", searchMessage);
                    }
                    else if (!string.IsNullOrEmpty(currentContext))
                    {
                        searchMessage = currentContext;
                        _logger.LogInformation("User message is generic. Using CONTEXT from previous conversation: {SearchMessage}", searchMessage);
                    }
                    else
                    {
                        _logger.LogWarning("Ambiguous booking request. No context and message is not specific.");
                        return Json(new { 
                            success = true, 
                            reply = "<p>Bạn muốn đặt khách sạn nào ạ? 🤔</p><p>Bạn có thể cho mình biết tên khách sạn hoặc yêu cầu tìm kiếm (ví dụ: 'khách sạn 300k ở Quảng Ninh') để mình tìm giúp nhé.</p>" 
                        });
                    }
                    
                    _logger.LogInformation("Final Search message for booking: {SearchMessage}", searchMessage);
                    
                    var hotels = await GetHotelsFromDatabase(searchMessage, priceFilter);
                    
                    _logger.LogInformation("Found {Count} hotels for booking", hotels.Count);
                    
                    if (hotels.Any())
                    {
                        var hotelCardsHtml = "";
                    
                        foreach (var hotel in hotels)
                    {
                        var firstRoom = await _context.Rooms
                                .Where(r => r.PropertyId == hotel.PropertyId)
                            .FirstOrDefaultAsync();
                        
                        var roomId = firstRoom?.Id ?? 0;
                            var bookingUrl = $"/Booking/Book?propertyId={hotel.PropertyId}&roomId={roomId}&checkIn={checkinParam}&checkOut={checkoutParam}&guests=2";
                        
                        var displayCheckin = checkinParam;
                        var displayCheckout = checkoutParam;
                        
                        if (DateTime.TryParse(checkinParam, out var checkinDate))
                        {
                            displayCheckin = checkinDate.ToString("dd/MM/yyyy");
                        }
                        if (DateTime.TryParse(checkoutParam, out var checkoutDate))
                        {
                            displayCheckout = checkoutDate.ToString("dd/MM/yyyy");
                        }
                        
                            var hotelCardHtml = $@"
                            <div class='hotel-card' style='border: 1px solid #ddd; border-radius: 8px; padding: 16px; margin: 16px 0; background: white; box-shadow: 0 2px 4px rgba(0,0,0,0.1);'>
                                <div style='display: flex; gap: 16px;'>
                                    <div style='flex: 0 0 200px;'>
                                        <img src='{NormalizeImagePath(hotel.ImageUrl)}' alt='{hotel.Name}' style='width: 100%; height: 150px; object-fit: cover; border-radius: 8px;' />
                                    </div>
                                    <div style='flex: 1;'>
                                        <h3 style='margin: 0 0 8px 0; color: #333;'>{hotel.Name}</h3>
                                        <p style='margin: 4px 0; color: #666;'><strong>Địa chỉ:</strong> {hotel.Address}</p>
                                        <p style='margin: 4px 0; color: #666;'><strong>Loại phòng:</strong> Phòng tiêu chuẩn</p>
                                        <p style='margin: 4px 0; color: #666;'><strong>Bao gồm:</strong> Bữa sáng</p>
                                        <p style='margin: 4px 0; color: #666;'><strong>Ngày nhận phòng:</strong> {displayCheckin}</p>
                                        <p style='margin: 4px 0; color: #666;'><strong>Ngày trả phòng:</strong> {displayCheckout}</p>
                                        <div style='margin-top: 12px;'>
                                            <span style='background: #007bff; color: white; padding: 4px 8px; border-radius: 4px; font-size: 12px;'>{hotel.Rating}/10</span>
                                            <span style='color: #e74c3c; font-weight: bold; font-size: 18px; margin-left: 12px;'>{hotel.PriceFrom:N0} VND</span>
                                        </div>
                                        <div style='margin-top: 12px;'>
                                            <a href='{bookingUrl}' style='background: #007bff; color: white; padding: 10px 20px; text-decoration: none; border-radius: 4px; display: inline-block;'>Đặt phòng ngay</a>
                                        </div>
                                    </div>
                                </div>
                            </div>";
                            
                            hotelCardsHtml += hotelCardHtml;
                        }
                        
                        return Json(new { 
                            success = true, 
                            reply = $"<p>Mình đã tìm được một vài chỗ ở phù hợp cho bạn đây!</p>{hotelCardsHtml}<p><strong>Bạn có thể:</strong></p><ul><li>Hỏi mình về tiện nghi của một khách sạn cụ thể.</li><li>Yêu cầu khoảng giá mong muốn (ví dụ: 'dưới 500k').</li><li>Nói 'đặt phòng' + tên khách sạn bạn chọn.</li></ul><p>Mình luôn sẵn sàng giúp bạn tìm được nơi ở ưng ý nhất!</p>",
                            newContext = searchMessage
                        });
                    }
                    else
                    {
                        var bookingGuide = @"<p><strong>Rất tiếc, mình không tìm thấy khách sạn phù hợp với yêu cầu từ cuộc trò chuyện trước.</strong></p>
<p>Bạn có thể thử tìm kiếm lại:</p>
<p><a class='book-btn' href='/Public/Search' style='display:inline-block;padding:10px 14px;background:#006ce4;color:#fff;border-radius:6px;text-decoration:none;'>Xem khách sạn</a></p>";
                        return Json(new { success = true, reply = bookingGuide });
                    }
                }

                if (isBookingRequest)
                {
                    var mergedSearchText = string.Join(' ', new[] { request.Message, request.Context ?? string.Empty, request.CurrentUrl ?? string.Empty });
                    var hotels = await GetHotelsFromDatabase(mergedSearchText, priceFilter);
                    
                    string newContextForFrontEnd = request.Context ?? string.Empty; 

                    if (hotels.Any())
                    {
                        var hotelCardsHtml = "";
                        var hotelNamesForContext = new List<string>();

                        foreach (var hotel in hotels)
                        {
                            hotelCardsHtml += CreateHotelCardHtml(hotel);
                            hotelNamesForContext.Add(hotel.Name); 
                        }
                        
                        newContextForFrontEnd = $"Đã hiển thị cho khách hàng: {string.Join(", ", hotelNamesForContext)}. Yêu cầu gốc: {request.Message}";

                        var introHtml = "<p><strong>✨ Mình đã tìm được một vài chỗ ở phù hợp cho bạn đây!</strong></p>";
                        var outroHtml = "<div style=\"margin-top:12px\">"
                                      + "<p><strong>Bạn có thể:</strong></p>"
                                      + "<ul>"
                                      + "<li>Hỏi mình về tiện nghi của một khách sạn cụ thể.</li>"
                                      + "<li>Yêu cầu khoảng giá mong muốn (ví dụ: 'dưới 500k').</li>"
                                      + "<li>Nói 'đặt phòng' + tên khách sạn bạn chọn.</li>"
                                      + "</ul>"
                                      + "<p>Mình luôn sẵn sàng giúp bạn tìm được nơi ở ưng ý nhất! 😊</p>"
                                      + "</div>";
                        var finalReply = introHtml + hotelCardsHtml + outroHtml;
                        
                        return Json(new { success = true, reply = finalReply, newContext = newContextForFrontEnd });
                    }
                    else
                    {
                        // If no hotels found after price filtering, return a direct response instead of sending to AI
                        var noHotelsMessage = "<p><strong>Rất tiếc, mình không tìm thấy khách sạn phù hợp với yêu cầu của bạn.</strong></p>" +
                                            "<p>Bạn có thể thử:</p>" +
                                            "<ul>" +
                                            "<li>Tăng ngân sách lên một chút</li>" +
                                            "<li>Tìm ở khu vực khác</li>" +
                                            "<li>Yêu cầu khác (ví dụ: 'khách sạn 500k')</li>" +
                                            "</ul>" +
                                            "<p>Mình luôn sẵn sàng giúp bạn tìm được nơi ở ưng ý nhất! 😊</p>";
                        
                        return Json(new { success = true, reply = noHotelsMessage, newContext = "Không tìm thấy khách sạn phù hợp với yêu cầu." });
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(request.Context))
                    {
                        if (!isFollowUpQuestion)
                    {
                        userMessage = $"Ngữ cảnh: {request.Context}\n\nCâu hỏi của khách hàng: {userMessage}";
                        }
                    }
                }

                var messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userMessage }
                };
                var requestBody = new
                {
                    model = "gpt-3.5-turbo",
                    messages = messages,
                    max_tokens = 3000,
                    temperature = 0.7
                };
                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<OpenAIResponse>(responseContent);
                    var reply = result?.choices?.FirstOrDefault()?.message?.content ?? _localizer["SorryError"];
                    
                    var newContext = reply;
                    if (isBookingRequest)
                    {
                        newContext = request.Context ?? string.Empty;
                    }
                    
                    return Json(new { success = true, reply = reply, newContext = newContext });
                }
                else
                {
                    _logger.LogError("OpenAI API error: {StatusCode} - {Content}", response.StatusCode, responseContent);
                    return Json(new { success = false, reply = _localizer["ConnectionError"] });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in SendMessage");
                return Json(new { success = false, reply = $"{_localizer["SystemError"]}: {ex.Message}" });
            }
        }

        private bool DoesContextImplySpecificHotel(string context)
        {
            if (string.IsNullOrEmpty(context)) return false;
            var searchText = context.ToLower();
            
            // Chỉ nhận diện là tìm khách sạn cụ thể khi:
            // 1. Có từ khóa "cụ thể" hoặc "chi tiết" 
            // 2. Có tên khách sạn + từ khóa "thông tin" hoặc "giá"
            // 3. Có tên khách sạn + từ khóa "phòng"
            // KHÔNG nhận diện khi có từ khóa "giá dưới", "giá trên", "dưới", "trên"
            
            if (searchText.Contains("giá dưới") || searchText.Contains("giá trên") || 
                searchText.Contains("dưới") || searchText.Contains("trên") ||
                searchText.Contains("khách sạn khác") || searchText.Contains("hotel khác"))
            {
                return false; // Đây là tìm kiếm theo tiêu chí, không phải khách sạn cụ thể
            }
            
            return searchText.Contains("citadines") || searchText.Contains("marina") ||
                   searchText.Contains("khách sạn 2") || searchText.Contains("hotel 2") ||
                   searchText.Contains("vinpearl") ||
                   searchText.Contains("legacy") || searchText.Contains("yên tử");
        }

        private async Task<List<HotelInfo>> GetHotelsFromDatabase(string rawSearchText, string priceFilter = "")
        {
            try
            {
                var query = _context.Properties
                    .Where(p => p.Status == PropertyStatus.Approved)
                    .AsQueryable();

                var searchText = (rawSearchText ?? string.Empty).ToLower();
                _logger.LogInformation("GetHotelsFromDatabase searching with text: {SearchText}", searchText);

                bool hasSpecificHotel = DoesContextImplySpecificHotel(searchText);
                _logger.LogInformation("DoesContextImplySpecificHotel result: {Result} for search text: {SearchText}", hasSpecificHotel, searchText);
                
                if (hasSpecificHotel)
                {
                    _logger.LogInformation("Found specific hotel in search text.");
                    
                    if (searchText.Contains("legacy") || searchText.Contains("yên tử"))
                    {
                        query = query.Where(p => p.Name.ToLower().Contains("legacy"));
                    }
                    else if (searchText.Contains("citadines") || searchText.Contains("marina"))
                    {
                        query = query.Where(p => p.Name.ToLower().Contains("citadines") || p.Name.ToLower().Contains("marina"));
                    }
                    else if (searchText.Contains("khách sạn 2") || searchText.Contains("hotel 2"))
                    {
                        query = query.Where(p => p.Name.ToLower().Contains("khách sạn 2") || p.Name.ToLower().Contains("hotel 2"));
                    }
                }
                else
                {
                    _logger.LogInformation("General search. Checking for locations.");
                    // Check for specific provinces/cities
                    if (searchText.Contains("quảng ninh") || searchText.Contains("quang ninh") || searchText.Contains("hạ long") || searchText.Contains("ha long"))
                    {
                        query = query.Where(p => p.City.ToLower().Contains("quảng ninh") ||
                                               p.City.ToLower().Contains("hạ long") ||
                                               p.AddressLine.ToLower().Contains("hạ long"));
                        _logger.LogInformation("Searching for Quang Ninh properties. Query will return all matching properties.");
                    }
                    else if (searchText.Contains("hà nội") || searchText.Contains("ha noi") || searchText.Contains("hanoi"))
                    {
                        query = query.Where(p => p.City.ToLower().Contains("hà nội") ||
                                               p.City.ToLower().Contains("ha noi") ||
                                               p.City.ToLower().Contains("hanoi"));
                        _logger.LogInformation("Searching for Ha Noi properties. Query will return all matching properties.");
                    }
                    else if (searchText.Contains("thái nguyên") || searchText.Contains("thai nguyen"))
                    {
                        query = query.Where(p => p.City.ToLower().Contains("thái nguyên") ||
                                               p.City.ToLower().Contains("thai nguyen"));
                        _logger.LogInformation("Searching for Thai Nguyen properties. Query will return all matching properties.");
                    }
                    else if (searchText.Contains("hải phòng") || searchText.Contains("hai phong"))
                    {
                        query = query.Where(p => p.City.ToLower().Contains("hải phòng") ||
                                               p.City.ToLower().Contains("hai phong"));
                        _logger.LogInformation("Searching for Hai Phong properties. Query will return all matching properties.");
                    }
                    else if (searchText.Contains("tp.hcm") || searchText.Contains("hồ chí minh") || searchText.Contains("ho chi minh") || searchText.Contains("sài gòn") || searchText.Contains("sai gon"))
                    {
                        query = query.Where(p => p.City.ToLower().Contains("tp.hcm") ||
                                               p.City.ToLower().Contains("hồ chí minh") ||
                                               p.City.ToLower().Contains("ho chi minh") ||
                                               p.City.ToLower().Contains("sài gòn") ||
                                               p.City.ToLower().Contains("sai gon"));
                        _logger.LogInformation("Searching for TP.HCM properties. Query will return all matching properties.");
                    }
                    else if (searchText.Contains("đà nẵng") || searchText.Contains("da nang"))
                    {
                        query = query.Where(p => p.City.ToLower().Contains("đà nẵng") ||
                                               p.City.ToLower().Contains("da nang"));
                        _logger.LogInformation("Searching for Da Nang properties. Query will return all matching properties.");
                    }
                    else if (searchText.Contains("nha trang"))
                    {
                        query = query.Where(p => p.City.ToLower().Contains("nha trang"));
                        _logger.LogInformation("Searching for Nha Trang properties. Query will return all matching properties.");
                    }
                    else if (searchText.Contains("phú quốc") || searchText.Contains("phu quoc"))
                    {
                        query = query.Where(p => p.City.ToLower().Contains("phú quốc") ||
                                               p.City.ToLower().Contains("phu quoc"));
                        _logger.LogInformation("Searching for Phu Quoc properties. Query will return all matching properties.");
                    }
                    else
                    {
                        query = query.Take(5); 
                    }

                    // Only filter by type if user specifically asks for resort or hotel
                    if (searchText.Contains("resort"))
                    {
                        query = query.Where(p => p.Type == PropertyType.Resort);
                        _logger.LogInformation("Filtering for Resort type only.");
                    }
                    else if (searchText.Contains("khách sạn") || searchText.Contains("hotel"))
                    {
                        // For general "khách sạn" search, include both Hotel and Resort
                        query = query.Where(p => p.Type == PropertyType.Hotel || p.Type == PropertyType.Resort);
                        _logger.LogInformation("Including both Hotel and Resort types for general search.");
                        
                        // *** TÍNH NĂNG MỚI: Xử lý "khách sạn khác" ***
                        if (searchText.Contains("khách sạn khác") || searchText.Contains("hotel khác"))
                        {
                            // Loại bỏ Legacy Yên Tử khi user hỏi "khách sạn khác"
                            query = query.Where(p => !p.Name.ToLower().Contains("legacy") && 
                                               !p.Name.ToLower().Contains("yên tử") &&
                                               !p.Name.ToLower().Contains("yen tu"));
                            _logger.LogInformation("User asking for OTHER hotels, excluding Legacy Yên Tử.");
                        }
                    }
                    // If no specific type mentioned, include all types (Hotel, Resort, etc.)
                }

                // For specific province/city searches, get all properties; for others, limit to 10
                var isSpecificLocationSearch = searchText.Contains("quảng ninh") || searchText.Contains("quang ninh") || searchText.Contains("hạ long") || searchText.Contains("ha long") ||
                                               searchText.Contains("hà nội") || searchText.Contains("ha noi") || searchText.Contains("hanoi") ||
                                               searchText.Contains("thái nguyên") || searchText.Contains("thai nguyen") ||
                                               searchText.Contains("hải phòng") || searchText.Contains("hai phong") ||
                                               searchText.Contains("tp.hcm") || searchText.Contains("hồ chí minh") || searchText.Contains("ho chi minh") || searchText.Contains("sài gòn") || searchText.Contains("sai gon") ||
                                               searchText.Contains("đà nẵng") || searchText.Contains("da nang") ||
                                               searchText.Contains("nha trang") ||
                                               searchText.Contains("phú quốc") || searchText.Contains("phu quoc");
                
                var properties = isSpecificLocationSearch
                    ? await query.ToListAsync()
                    : await query.Take(10).ToListAsync();

                _logger.LogInformation("Found {Count} properties from database", properties.Count);
                foreach (var prop in properties)
                {
                    _logger.LogInformation("Property: {Name} (ID: {Id}, Type: {Type}, City: {City})", 
                        prop.Name, prop.Id, prop.Type, prop.City);
                }

                var hotels = new List<HotelInfo>();
                foreach (var property in properties)
                {
                    _logger.LogInformation("Processing property: {Name} (ID: {Id})", property.Name, property.Id);
                    
                    var roomPrices = await _context.RoomPrices
                        .Where(rp => rp.PropertyId == property.Id)
                        .ToListAsync();
                    var minPrice = roomPrices.Any() ? roomPrices.Min(rp => rp.Amount) : 0;
                    
                    _logger.LogInformation("Property {Name}: Found {RoomCount} room prices, min price: {MinPrice}", 
                        property.Name, roomPrices.Count, minPrice);

                    var propertyData = await _context.PropertyData
                        .FirstOrDefaultAsync(pd => pd.PropertyId == property.Id);
                    var starRating = propertyData?.StarRating ?? 4;
                    var roomDescription = propertyData?.RoomDescription ?? "Phòng hiện đại với đầy đủ tiện nghi";
                    
                    var propertyType = property.Type == PropertyType.Resort ? "Resort" : "Khách sạn";
                    var description = $"{propertyType} {roomDescription}. ";
                    if (propertyData?.HasRestaurant == true) description += "Có nhà hàng. ";
                    if (propertyData?.HasBar == true) description += "Có quầy bar. ";
                    if (propertyData?.HasParkingArea == true) description += "Có bãi đỗ xe. ";
                    if (propertyData?.HasPublicWifi == true) description += "Có WiFi miễn phí. ";
                    
                    var resolvedByDb = ResolveImageUrl(propertyData);
                    var resolvedByFolder = GetLocalPropertyImage(property.Id);
                    var finalImageUrl = !string.IsNullOrEmpty(resolvedByDb) ? resolvedByDb : resolvedByFolder;
                    if (string.IsNullOrEmpty(finalImageUrl)) finalImageUrl = "/images/default-hotel.svg";

                    hotels.Add(new HotelInfo
                    {
                        PropertyId = property.Id,
                        RoomId = 0, 
                        Name = property.Name,
                        Address = $"{property.AddressLine}, {property.City}",
                        Description = description.Trim(),
                        PriceFrom = minPrice,
                        Rating = starRating,
                        ImageUrl = finalImageUrl
                    });
                    
                    _logger.LogInformation("Added hotel to list: {Name} (Price: {Price})", property.Name, minPrice);
                }

                _logger.LogInformation("Final hotels list contains {Count} hotels before price filtering", hotels.Count);
                    
                // Apply price filtering BEFORE returning results
                if (!string.IsNullOrEmpty(priceFilter))
                    {
                    var originalCount = hotels.Count;
                    if (priceFilter.Contains("<="))
                    {
                        var maxPrice = int.Parse(priceFilter.Split("<=")[1].Trim());
                        hotels = hotels.Where(h => h.PriceFrom <= maxPrice).ToList();
                        _logger.LogInformation("Applied price filter: under {MaxPrice}. Filtered from {OriginalCount} to {FilteredCount} hotels", 
                            maxPrice, originalCount, hotels.Count);
                    }
                    else if (priceFilter.Contains(">="))
                    {
                        var minPrice = int.Parse(priceFilter.Split(">=")[1].Trim());
                        hotels = hotels.Where(h => h.PriceFrom >= minPrice).ToList();
                        _logger.LogInformation("Applied price filter: above {MinPrice}. Filtered from {OriginalCount} to {FilteredCount} hotels", 
                            minPrice, originalCount, hotels.Count);
                    }
                }
                else if (hasSpecificHotel)
                {
                    _logger.LogInformation("Skipping price filters because a specific hotel was requested.");
                }
                
                _logger.LogInformation("Final hotels list after price filtering contains {Count} hotels", hotels.Count);

                return hotels;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting hotels from database");
                return new List<HotelInfo>();
            }
        }
        
        private string CreateHotelCardHtml(HotelInfo hotel)
        {
            var stars = new string('★', (int)hotel.Rating);
            var amenities = GetAmenitiesFromDescription(hotel.Description);
            var safeName = string.IsNullOrWhiteSpace(hotel.Name) ? "Khách sạn" : System.Net.WebUtility.HtmlEncode(hotel.Name);
            var safeAddress = string.IsNullOrWhiteSpace(hotel.Address) ? "Địa chỉ đang cập nhật" : System.Net.WebUtility.HtmlEncode(hotel.Address);
            var safePrice = hotel.PriceFrom > 0 ? hotel.PriceFrom.ToString("N0") : "Liên hệ";
            var imageUrl = string.IsNullOrWhiteSpace(hotel.ImageUrl) ? "/images/default-hotel.svg" : hotel.ImageUrl;

            // Tạo mô tả chi tiết về vị trí và tiện ích
            var locationDescription = GenerateLocationDescription(hotel);

            return $@"
<div class='search-card'>
  <div class='hotel-image'>
    <img class='hotel-img' src=""{imageUrl}"" alt=""{hotel.Name}"" onerror=""this.src='/images/default-hotel.svg'"" />
    <button class='heart-btn' title='Yêu thích'>❤</button>
  </div>
  <div class='hotel-details'>
    <div style='display:flex;align-items:center;gap:8px;'>
      <h3 class='hotel-name'>{safeName}</h3>
      <span class='hotel-stars'>{stars}</span>
    </div>
    <div class='hotel-location'>{safeAddress}</div>
    <div class='room-type'>Phòng tiêu chuẩn</div>
    <div class='promotion'>Bao gồm bữa sáng</div>
    <div class='warning'>Chỉ còn vài phòng với giá này trên trang của chúng tôi</div>
    <div style='margin-top: 12px; padding: 8px; background: #f8f9fa; border-radius: 6px; font-size: 13px; color: #495057;'>
      <strong>📍 Vị trí & Tiện ích:</strong><br/>
      {locationDescription}
    </div>
  </div>
  <div class='rating-section'>
    <div class='rating-score'>8,9</div>
    <div class='price'>VND {safePrice}</div>
    <div class='price-includes'>+ thuế và phí</div>
    <button class='book-btn'>Xem chỗ trống</button>
  </div>
</div>";
        }

        private string GenerateLocationDescription(HotelInfo hotel)
        {
            var description = new List<string>();
            var name = hotel.Name?.ToLower() ?? "";
            var address = hotel.Address?.ToLower() ?? "";

            // Mô tả vị trí dựa trên tên và địa chỉ
            if (name.Contains("citadines") || name.Contains("marina"))
            {
                description.Add("🏖️ Nằm ngay bãi biển Bãi Cháy, view biển tuyệt đẹp");
                description.Add("🚶‍♂️ Đi bộ 2 phút đến bãi biển, 5 phút đến cáp treo Sun World");
                description.Add("🍽️ Xung quanh có nhiều nhà hàng hải sản, quán cà phê view biển");
                description.Add("🚗 Có bãi đỗ xe riêng, dễ dàng di chuyển bằng taxi/grab");
                description.Add("⏰ 15 phút đến cảng tàu du lịch Vịnh Hạ Long, 20 phút đến sân bay");
            }
            else if (name.Contains("legacy") || name.Contains("yên tử"))
            {
                description.Add("🏔️ Nằm trong khu du lịch Yên Tử, view núi rừng xanh mát");
                description.Add("🚶‍♂️ Đi bộ 10 phút đến chùa Yên Tử, 5 phút đến cáp treo");
                description.Add("🍽️ Có nhà hàng trong resort, xung quanh có quán ăn địa phương");
                description.Add("🚗 Có bãi đỗ xe rộng, xe bus từ Hà Nội đến tận cổng");
                description.Add("⏰ 30 phút đến trung tâm Uông Bí, 45 phút đến Hạ Long");
            }
            else if (name.Contains("khách sạn 2") || address.Contains("bãi cháy"))
            {
                description.Add("🏖️ Gần bãi biển Bãi Cháy, view biển đẹp");
                description.Add("🚶‍♂️ Đi bộ 3 phút đến bãi biển, 10 phút đến chợ đêm");
                description.Add("🍽️ Xung quanh có nhiều quán ăn, nhà hàng giá rẻ");
                description.Add("🚗 Có chỗ đỗ xe, dễ dàng gọi taxi/grab");
                description.Add("⏰ 20 phút đến cảng tàu, 25 phút đến sân bay");
            }
            else
            {
                // Mô tả chung dựa trên địa chỉ
                if (address.Contains("hạ long") || address.Contains("quảng ninh") || address.Contains("bãi cháy"))
                {
                    description.Add("🏖️ Gần các điểm du lịch nổi tiếng của Quảng Ninh");
                    description.Add("🚶‍♂️ Thuận tiện di chuyển đến Vịnh Hạ Long, Yên Tử");
                    description.Add("🍽️ Xung quanh có nhiều nhà hàng, quán ăn địa phương");
                    description.Add("🚗 Dễ dàng di chuyển bằng taxi, xe bus, xe máy");
                    description.Add("⏰ Gần trung tâm thành phố, tiện lợi cho du lịch");
                }
                else
                {
                    description.Add("📍 Vị trí thuận tiện, dễ dàng di chuyển");
                    description.Add("🍽️ Xung quanh có nhiều nhà hàng, quán ăn");
                    description.Add("🚗 Có chỗ đỗ xe, dễ dàng gọi taxi/grab");
                    description.Add("⏰ Gần các điểm du lịch và tiện ích công cộng");
                }
            }

            // Thêm tiện ích xung quanh
            description.Add("🏪 Gần siêu thị, chợ, ngân hàng, bệnh viện");
            description.Add("📶 WiFi miễn phí, có lễ tân 24/7");

            return string.Join("<br/>", description);
        }

        private List<string> GetAmenitiesFromDescription(string description)
        {
            var amenities = new List<string>();
            if (description.Contains("nhà hàng")) amenities.Add("🍽️ Nhà hàng");
            if (description.Contains("bar")) amenities.Add("🍸 Quầy bar");
            if (description.Contains("đỗ xe")) amenities.Add("🅿️ Bãi đỗ xe");
            if (description.Contains("WiFi")) amenities.Add("📶 WiFi miễn phí");
            if (description.Contains("hồ bơi")) amenities.Add("🏊 Hồ bơi");
            if (description.Contains("spa")) amenities.Add("🧘 Spa");
            return amenities;
        }

        private string ResolveImageUrl(PropertyData? propertyData)
        {
            var candidates = new List<string?>
            {
                propertyData?.ExteriorPhotoPath,
                propertyData?.RoomPhotoPath,
                propertyData?.PhotoPaths
            };

            foreach (var candidate in candidates)
            {
                var url = NormalizeImagePath(candidate);
                if (!string.IsNullOrEmpty(url)) return url;
            }

            return string.Empty;
        }

        private string GetLocalPropertyImage(int propertyId)
        {
            try
            {
                var folder = Path.Combine(_env.WebRootPath, "uploads", "properties", propertyId.ToString());
                if (!Directory.Exists(folder)) return string.Empty;
                var patterns = new[] { "*.jpg", "*.jpeg", "*.png", "*.webp", "*.avif" };
                foreach (var pattern in patterns)
                {
                    var file = Directory.EnumerateFiles(folder, pattern).FirstOrDefault();
                    if (!string.IsNullOrEmpty(file))
                    {
                        var fileName = Path.GetFileName(file);
                        return $"/uploads/properties/{propertyId}/{fileName}";
                    }
                }
                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private string NormalizeImagePath(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            var s = raw.Trim();

            if (s.StartsWith("[") && s.EndsWith("]"))
            {
                try
                {
                    var arr = System.Text.Json.JsonSerializer.Deserialize<List<string>>(s);
                    if (arr != null && arr.Count > 0)
                    {
                        s = arr[0].Trim('"', '\'', ' ');
                    }
                }
                catch { }
            }
            
            if (s.StartsWith("http://") || s.StartsWith("https://")) return s;
            
            var first = s.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                         .FirstOrDefault()?.Trim('"', '\'', ' ') ?? string.Empty;
            if (string.IsNullOrEmpty(first)) return string.Empty;

            var normalized = first.StartsWith("/") ? first : "/" + first;
            return normalized;
        }

        private async Task<List<RoomPhoto>> GetRoomPhotos(int propertyId, string roomType = "")
        {
            try
            {
                var query = _context.RoomPhotos
                    .Where(rp => _context.Rooms.Any(r => r.Id == rp.RoomId && r.PropertyId == propertyId))
                    .AsQueryable();

                if (!string.IsNullOrEmpty(roomType))
                {
                    query = query.Where(rp => _context.Rooms.Any(r => r.Id == rp.RoomId && 
                        (r.RoomType.ToLower().Contains(roomType.ToLower()) || r.Name.ToLower().Contains(roomType.ToLower()))));
                }

                return await query
                    .OrderBy(rp => rp.SortOrder)
                    .ThenBy(rp => rp.Category)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting room photos for property {PropertyId}", propertyId);
                return new List<RoomPhoto>();
            }
        }

        private async Task<string> CreateRoomCardsHtml(int propertyId, string hotelName)
        {
            try
            {
                _logger.LogInformation("Creating room cards for property {PropertyId}, hotel {HotelName}", propertyId, hotelName);
                
                // Lấy thông tin phòng từ database
                var rooms = await _context.Rooms
                    .Where(r => r.PropertyId == propertyId)
                    .Include(r => r.Photos)
                    .Include(r => r.Amenities)
                    .ToListAsync();

                _logger.LogInformation("Found {RoomCount} rooms for property {PropertyId}", rooms.Count, propertyId);

                if (!rooms.Any())
                {
                    _logger.LogWarning("No rooms found for property {PropertyId}", propertyId);
                    return "<p>Xin lỗi, chưa có thông tin phòng cho khách sạn này.</p>";
                }

                var roomsHtml = "<div style='margin: 16px 0;'>";
                roomsHtml += $"<h4 style='color: #1e40af; margin-bottom: 16px;'>🏨 Các loại phòng tại {hotelName}</h4>";

                foreach (var room in rooms.Take(3)) // Hiển thị tối đa 3 loại phòng
                {
                    _logger.LogInformation("Processing room {RoomId}: {RoomName}", room.Id, room.Name);
                    
                    // Lấy giá phòng
                    var roomPrices = await _context.RoomPrices
                        .Where(rp => rp.RoomId == room.Id)
                        .ToListAsync();
                    
                    var roomPrice = roomPrices.OrderBy(rp => rp.Amount).FirstOrDefault();

                    var price = roomPrice?.Amount ?? 0;
                    var formattedPrice = price > 0 ? $"{price:N0} VND" : "Liên hệ";
                    _logger.LogInformation("Room {RoomId} price: {Price}", room.Id, formattedPrice);

                    // Lấy tất cả ảnh phòng
                    var roomPhotos = room.Photos?.OrderBy(p => p.SortOrder).ToList() ?? new List<RoomPhoto>();
                    var mainPhoto = roomPhotos.FirstOrDefault()?.Url ?? "/images/default-room.jpg";
                    var normalizedMainPhoto = NormalizeImagePath(mainPhoto);
                    _logger.LogInformation("Room {RoomId} has {PhotoCount} photos", room.Id, roomPhotos.Count);

                    // Tạo danh sách tất cả ảnh cho modal
                    var allPhotos = roomPhotos.Select(p => NormalizeImagePath(p.Url)).ToList();
                    var allPhotosJson = System.Text.Json.JsonSerializer.Serialize(allPhotos);

                    // Tạo card phòng
                    roomsHtml += $@"
                        <div style='display: flex; margin-bottom: 20px; border: 1px solid #e5e7eb; border-radius: 8px; overflow: hidden; background: white; box-shadow: 0 2px 4px rgba(0,0,0,0.1);'>
                            <!-- Ảnh phòng -->
                            <div style='flex: 0 0 200px;'>
                                <img src='{normalizedMainPhoto}' 
                                     alt='{room.Name}' 
                                     style='width: 100%; height: 150px; object-fit: cover; cursor: pointer;'
                                     onclick='openImageModal(&quot;{normalizedMainPhoto}&quot;, &quot;{room.Name}&quot;, {allPhotosJson})'
                                     onerror='this.src=&quot;/images/default-room.jpg&quot; this.onerror=null;'>";

                    // Thêm gallery ảnh nhỏ
                    if (roomPhotos.Any())
                    {
                        roomsHtml += "<div style='display: flex; gap: 4px; margin: 8px; overflow-x: auto;'>";
                        foreach (var photo in roomPhotos.Take(6)) // Hiển thị tối đa 6 ảnh
                        {
                            var normalizedPhoto = NormalizeImagePath(photo.Url);
                            roomsHtml += $@"
                                <img src='{normalizedPhoto}' 
                                     alt='{room.Name}' 
                                     style='width: 60px; height: 60px; object-fit: cover; border-radius: 4px; cursor: pointer; border: 2px solid #e5e7eb;'
                                     onclick='openImageModal(&quot;{normalizedPhoto}&quot;, &quot;{room.Name}&quot;, {allPhotosJson})'
                                     onerror='this.src=&quot;/images/default-room.jpg&quot; this.onerror=null;'>";
                        }
                        roomsHtml += "</div>";
                    }

                    roomsHtml += $@"
                                <div style='padding: 8px; background: #f8f9fa;'>
                                    <div style='display: flex; gap: 8px; font-size: 12px; color: #6b7280;'>
                                        <span>📐 {room.Size} {room.SizeUnit}</span>
                                        <span>👥 {room.CapacityAdults} người</span>
                                    </div>
                                </div>
                            </div>
                            
                            <!-- Thông tin phòng -->
                            <div style='flex: 1; padding: 16px;'>
                                <h5 style='margin: 0 0 8px 0; color: #1e40af; font-size: 16px;'>{room.Name}</h5>
                                <p style='margin: 0 0 12px 0; color: #6b7280; font-size: 14px;'>Loại: {room.RoomType}</p>
                                
                                <!-- Tiện nghi -->
                                <div style='margin-bottom: 12px;'>
                                    <div style='display: flex; flex-wrap: wrap; gap: 4px;'>";

                        // Hiển thị tiện nghi
                        var amenities = room.Amenities?.Take(4) ?? new List<RoomAmenity>();
                        foreach (var amenity in amenities)
                        {
                            roomsHtml += $"<span style='background: #e0f2fe; color: #1e40af; padding: 2px 6px; border-radius: 4px; font-size: 12px;'>{amenity.Name}</span>";
                        }

                        roomsHtml += @"
                                    </div>
                                </div>
                                
                                <!-- Chính sách -->
                                <div style='font-size: 12px; color: #6b7280; margin-bottom: 12px;'>
                                    <div style='display: flex; align-items: center; gap: 4px; margin-bottom: 4px;'>
                                        <span>✅</span>
                                        <span>Thanh toán tại khách sạn</span>
                                    </div>
                                    <div style='display: flex; align-items: center; gap: 4px;'>
                                        <span>✅</span>
                                        <span>Có thể hủy miễn phí</span>
                                    </div>
                                </div>
                            </div>";
                        
                        roomsHtml += @"
                            <!-- Giá và nút chọn -->
                            <div style='flex: 0 0 150px; padding: 16px; text-align: center; background: #f8f9fa; display: flex; flex-direction: column; justify-content: space-between;'>
                                <div>
                                    <div style='font-size: 18px; font-weight: bold; color: #dc2626; margin-bottom: 4px;'>" + formattedPrice + @"</div>
                                    <div style='font-size: 12px; color: #6b7280;'>Bao gồm thuế và phí</div>
                                </div>
                                <button style='background: #3b82f6; color: white; border: none; padding: 8px 16px; border-radius: 6px; cursor: pointer; font-size: 14px; margin-top: 8px;' 
                                        onclick='alert(&quot;Tính năng đặt phòng sẽ được phát triển!&quot;)'>
                                    Chọn phòng
                                </button>
                            </div>
                        </div>";
                }

                roomsHtml += "</div>";
                return roomsHtml;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating room cards for property {PropertyId}", propertyId);
                return "<p>Xin lỗi, có lỗi xảy ra khi tải thông tin phòng.</p>";
            }
        }

        private bool IsRoomInquiryRequest(string message)
        {
            var roomKeywords = new[] { "phòng", "room", "xem phòng", "ảnh phòng", "hình ảnh phòng", "phòng như thế nào", "phòng ra sao" };
            var hotelKeywords = new[] { "citadines", "legacy", "khách sạn 2", "hotel 2", "marina", "vinpearl" };
            
            return roomKeywords.Any(rk => message.Contains(rk)) && 
                   hotelKeywords.Any(hk => message.Contains(hk));
        }

        private bool IsRoomBookingRequest(string message)
        {
            var lowerMessage = message.ToLower();
            
            // Kiểm tra từ khóa đặt phòng
            var bookingKeywords = new[] { "đặt phòng", "book", "booking", "đặt", "chọn phòng", "giúp tôi đặt" };
            var hasBookingKeyword = bookingKeywords.Any(k => lowerMessage.Contains(k));
            
            // Kiểm tra từ khóa phòng cụ thể
            var roomKeywords = new[] { "phòng", "room", "deluxe", "twin", "standard", "suite", "giường" };
            var hasRoomKeyword = roomKeywords.Any(k => lowerMessage.Contains(k));
            
            // Kiểm tra tên phòng cụ thể
            var specificRoomNames = new[] { "deluxe 2 giường", "deluxe twin", "2 giường", "twin" };
            var hasSpecificRoom = specificRoomNames.Any(r => lowerMessage.Contains(r));
            
            // Kiểm tra có từ khóa khách sạn không
            var hotelKeywords = new[] { "citadines", "marina", "hạ long", "hotel", "khách sạn" };
            var hasHotelKeyword = hotelKeywords.Any(h => lowerMessage.Contains(h));
            
            _logger.LogInformation("Room booking check - Message: '{Message}', HasBooking: {HasBooking}, HasRoom: {HasRoom}, HasSpecific: {HasSpecific}, HasHotel: {HasHotel}", 
                message, hasBookingKeyword, hasRoomKeyword, hasSpecificRoom, hasHotelKeyword);
            
            // Nếu có từ khóa đặt phòng + (phòng cụ thể HOẶC tên phòng cụ thể) + có thể có tên khách sạn
            return hasBookingKeyword && (hasRoomKeyword || hasSpecificRoom);
        }

        private string ExtractHotelNameFromMessage(string message)
        {
            var hotelNames = new[] { "citadines", "legacy", "khách sạn 2", "hotel 2", "marina", "vinpearl" };
            var lowerMessage = message.ToLower();
            
            foreach (var hotelName in hotelNames)
            {
                if (lowerMessage.Contains(hotelName))
                {
                    return hotelName;
                }
            }
            
            return "";
        }

        private async Task<IActionResult> HandleRoomImageRequest(string hotelName, string originalMessage)
        {
            try
            {
                _logger.LogInformation("Handling room image request for hotel: {HotelName}", hotelName);

                // Find the property by name
                var property = await _context.Properties
                    .Where(p => p.Name.ToLower().Contains(hotelName.ToLower()) && p.Status == PropertyStatus.Approved)
                    .FirstOrDefaultAsync();

                if (property == null)
                {
                    return Json(new { 
                        success = true, 
                        reply = $"<p>Xin lỗi, mình không tìm thấy khách sạn '{hotelName}' trong hệ thống.</p>" 
                    });
                }

                // Get room photos for this property
                var roomPhotos = await GetRoomPhotos(property.Id);
                
                if (!roomPhotos.Any())
                {
                    return Json(new { 
                        success = true, 
                        reply = $"<p>Xin lỗi, hiện tại chưa có ảnh phòng cho khách sạn '{property.Name}'.</p>" 
                    });
                }

                // Create HTML with room cards
                var roomCardsHtml = await CreateRoomCardsHtml(property.Id, property.Name);
                
                var reply = $"<p><strong>🏨 Đây là các loại phòng tại {property.Name}:</strong></p>{roomCardsHtml}";
                
                return Json(new { 
                    success = true, 
                    reply = reply,
                    newContext = $"Đã hiển thị ảnh phòng cho khách sạn {property.Name}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling room image request for hotel: {HotelName}", hotelName);
                return Json(new { 
                    success = true, 
                    reply = "<p>Xin lỗi, có lỗi xảy ra khi tải ảnh phòng. Vui lòng thử lại sau.</p>" 
                });
            }
        }

        private (string checkin, string checkout) ExtractDatesFromText(string text)
        {
            try
            {
                var datePattern = @"(\d{1,2})\/(\d{1,2})";
                var matches = System.Text.RegularExpressions.Regex.Matches(text ?? string.Empty, datePattern);
                var year = DateTime.Now.Year;
                if (matches.Count >= 2)
                {
                    var d1 = int.Parse(matches[0].Groups[1].Value);
                    var m1 = int.Parse(matches[0].Groups[2].Value);
                    var d2 = int.Parse(matches[1].Groups[1].Value);
                    var m2 = int.Parse(matches[1].Groups[2].Value);
                    var checkin = new DateTime(year, m1, d1).ToString("yyyy-MM-dd");
                    var checkout = new DateTime(year, m2, d2).ToString("yyyy-MM-dd");
                    return (checkin, checkout);
                }
                if (matches.Count == 1)
                {
                    var d = int.Parse(matches[0].Groups[1].Value);
                    var m = int.Parse(matches[0].Groups[2].Value);
                    var ci = new DateTime(year, m, d);
                    return (ci.ToString("yyyy-MM-dd"), ci.AddDays(1).ToString("yyyy-MM-dd"));
                }
            }
            catch { }
            return (string.Empty, string.Empty);
        }

        private async Task<IActionResult> HandleRoomBookingRequest(string hotelName, string originalMessage)
        {
            try
            {
                _logger.LogInformation("Handling room booking request for hotel: {HotelName}", hotelName);

                var property = await _context.Properties
                    .Where(p => p.Name.ToLower().Contains(hotelName.ToLower()) && p.Status == PropertyStatus.Approved)
                    .FirstOrDefaultAsync();

                if (property == null)
                {
                    return Json(new { 
                        success = true, 
                        reply = $"<p>Xin lỗi, mình không tìm thấy khách sạn '{hotelName}' trong hệ thống.</p>" 
                    });
                }

                // Tìm phòng cụ thể mà user yêu cầu
                var specificRoom = await FindSpecificRoom(property.Id, originalMessage);
                
                if (specificRoom != null)
                {
                    // Hiển thị thông tin phòng cụ thể
                    var (checkin, checkout) = ExtractDatesFromText(originalMessage);
                    var bookingUrl = $"/Booking/Book?propertyId={property.Id}&roomId={specificRoom.Id}&checkIn={checkin}&checkOut={checkout}&guests=2";
                    var roomInfoHtml = await CreateSpecificRoomBookingHtml(specificRoom, property.Name, bookingUrl);
                    
                    // Parse dates for booking form
                    var checkinDate = DateTime.TryParse(checkin, out var ci) ? ci : DateTime.Now.AddDays(1);
                    var checkoutDate = DateTime.TryParse(checkout, out var co) ? co : DateTime.Now.AddDays(2);
                    var bookingFormHtml = CreateBookingFormHtml(property.Id, specificRoom.Id, property.Name, specificRoom.Name, checkinDate, checkoutDate, 2);
                    var reply = $"<p><strong>✅ Mình đã tìm thấy phòng bạn yêu cầu tại {property.Name}:</strong></p>{roomInfoHtml}{bookingFormHtml}";
                    
                    return Json(new { 
                        success = true, 
                        reply = reply,
                        newContext = $"Đã tìm thấy phòng {specificRoom.Name} tại {property.Name}"
                    });
                }
                else
                {
                    // Nếu không tìm thấy phòng cụ thể, hiển thị tất cả phòng
                    var roomCardsHtml = await CreateRoomCardsHtml(property.Id, property.Name);
                    var reply = $"<p><strong>🏨 Mình không tìm thấy phòng cụ thể bạn yêu cầu, nhưng đây là các phòng có sẵn tại {property.Name}:</strong></p>{roomCardsHtml}";
                    
                    return Json(new { 
                        success = true, 
                        reply = reply,
                        newContext = $"Đã hiển thị tất cả phòng tại {property.Name}"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling room booking request for hotel: {HotelName}", hotelName);
                return Json(new { 
                    success = true, 
                    reply = "<p>Xin lỗi, có lỗi xảy ra khi xử lý yêu cầu đặt phòng. Vui lòng thử lại sau.</p>" 
                });
            }
        }

        private async Task<Room> FindSpecificRoom(int propertyId, string message)
        {
            var rooms = await _context.Rooms
                .Where(r => r.PropertyId == propertyId)
                .Include(r => r.Photos)
                .Include(r => r.Amenities)
                .ToListAsync();

            var lowerMessage = message.ToLower();
            
            _logger.LogInformation("Finding specific room for property {PropertyId}, message: '{Message}'", propertyId, message);
            _logger.LogInformation("Available rooms: {RoomCount}", rooms.Count);
            foreach (var room in rooms)
            {
                _logger.LogInformation("Room: {RoomName} (Type: {RoomType})", room.Name, room.RoomType);
            }
            
            // Tìm phòng theo tên cụ thể
            foreach (var room in rooms)
            {
                if (lowerMessage.Contains(room.Name.ToLower()) || 
                    lowerMessage.Contains(room.RoomType.ToLower()))
                {
                    _logger.LogInformation("Found room by exact name match: {RoomName}", room.Name);
                    return room;
                }
            }

            // Tìm phòng theo từ khóa cụ thể
            if (lowerMessage.Contains("deluxe") && lowerMessage.Contains("twin"))
            {
                var room = rooms.FirstOrDefault(r => r.RoomType.ToLower().Contains("deluxe") && 
                                                   r.Name.ToLower().Contains("twin"));
                if (room != null)
                {
                    _logger.LogInformation("Found room by deluxe + twin: {RoomName}", room.Name);
                    return room;
                }
            }

            if (lowerMessage.Contains("deluxe 2 giường"))
            {
                var room = rooms.FirstOrDefault(r => r.Name.ToLower().Contains("deluxe") && 
                                                   r.Name.ToLower().Contains("2 giường"));
                if (room != null)
                {
                    _logger.LogInformation("Found room by 'deluxe 2 giường': {RoomName}", room.Name);
                    return room;
                }
            }

            if (lowerMessage.Contains("deluxe"))
            {
                var room = rooms.FirstOrDefault(r => r.RoomType.ToLower().Contains("deluxe"));
                if (room != null)
                {
                    _logger.LogInformation("Found room by deluxe: {RoomName}", room.Name);
                    return room;
                }
            }

            if (lowerMessage.Contains("twin"))
            {
                var room = rooms.FirstOrDefault(r => r.Name.ToLower().Contains("twin"));
                if (room != null)
                {
                    _logger.LogInformation("Found room by twin: {RoomName}", room.Name);
                    return room;
                }
            }

            _logger.LogInformation("No specific room found, returning null");
            return null;
        }

        private async Task<string> CreateSpecificRoomBookingHtml(Room room, string hotelName, string bookingUrl)
        {
            try
            {
                // Lấy giá phòng
                var roomPrices = await _context.RoomPrices
                    .Where(rp => rp.RoomId == room.Id)
                    .ToListAsync();
                
                var roomPrice = roomPrices.OrderBy(rp => rp.Amount).FirstOrDefault();
                var price = roomPrice?.Amount ?? 0;
                var formattedPrice = price > 0 ? $"{price:N0} VND" : "Liên hệ";

                // Lấy ảnh phòng chính
                var mainPhoto = room.Photos?.FirstOrDefault()?.Url ?? "/images/default-room.jpg";
                var normalizedPhoto = NormalizeImagePath(mainPhoto);

                var html = $@"
                    <div style='display: flex; margin-bottom: 12px; border: 1px solid #3b82f6; border-radius: 8px; overflow: hidden; background: white; box-shadow: 0 2px 6px rgba(59, 130, 246, 0.12); max-width: 920px;'>
                        <!-- Ảnh phòng -->
                        <div style='flex: 0 0 190px;'>
                            <img src='{normalizedPhoto}' 
                                 alt='{room.Name}' 
                                 style='width: 100%; height: 120px; object-fit: cover;'
                                 onerror='this.src=&quot;/images/default-room.jpg&quot; this.onerror=null;'>
                            <div style='padding: 8px; background: #f0f9ff;'>
                                <div style='display: flex; gap: 6px; font-size: 12px; color: #1e40af; font-weight: 700;'>
                                    <span>📐 {room.Size} {room.SizeUnit}</span>
                                    <span>👥 {room.CapacityAdults} người</span>
                                </div>
                            </div>
                        </div>
                        
                        <!-- Thông tin phòng -->
                        <div style='flex: 1; padding: 12px;'>
                            <h4 style='margin: 0 0 6px 0; color: #1e40af; font-size: 16px; font-weight: 700;'>{room.Name}</h4>
                            <p style='margin: 0 0 10px 0; color: #6b7280; font-size: 13px; font-weight: 500;'>Loại: {room.RoomType}</p>
                            
                            <!-- Tiện nghi -->
                            <div style='margin-bottom: 10px;'>
                                <h5 style='margin: 0 0 4px 0; color: #1e40af; font-size: 13px;'>Tiện nghi:</h5>
                                <div style='display: flex; flex-wrap: wrap; gap: 4px;'>";

                // Hiển thị tiện nghi
                var amenities = room.Amenities?.Take(6) ?? new List<RoomAmenity>();
                foreach (var amenity in amenities)
                {
                    html += $"<span style='background: #dbeafe; color: #1e40af; padding: 2px 6px; border-radius: 6px; font-size: 12px; font-weight: 500;'>{amenity.Name}</span>";
                }

                html += $@"
                                </div>
                            </div>
                            
                            <!-- Chính sách -->
                            <div style='font-size: 12px; color: #6b7280; margin-bottom: 10px;'>
                                <div style='display: flex; align-items: center; gap: 6px; margin-bottom: 6px;'>
                                    <span>✅</span>
                                    <span>Thanh toán tại khách sạn</span>
                                </div>
                                <div style='display: flex; align-items: center; gap: 6px;'>
                                    <span>✅</span>
                                    <span>Có thể hủy miễn phí</span>
                                </div>
                            </div>
                        </div>
                        
                        <!-- Giá và nút đặt phòng -->
                        <div style='flex: 0 0 140px; padding: 12px; text-align: center; background: #f0f9ff; display: flex; flex-direction: column; justify-content: space-between;'>
                            <div>
                                <div style='font-size: 18px; font-weight: 800; color: #dc2626; margin-bottom: 4px;'>{formattedPrice}</div>
                                <div style='font-size: 12px; color: #6b7280; margin-bottom: 8px;'>Bao gồm thuế và phí</div>
                            </div>
                            <button style='background: #3b82f6; color: white; border: none; padding: 8px 12px; border-radius: 8px; cursor: pointer; font-size: 14px; font-weight: 700; box-shadow: 0 2px 8px rgba(59, 130, 246, 0.2);' 
                                    onclick='window.location.href=&quot;{bookingUrl}&quot;'>
                                Đặt phòng ngay
                            </button>
                        </div>
                    </div>";

                return html;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating specific room booking HTML for room {RoomId}", room.Id);
                return "<p>Xin lỗi, có lỗi xảy ra khi tải thông tin phòng.</p>";
            }
        }

        private async Task<decimal> CalculateTotalPriceAsync(int propertyId, int roomId, int nights, string? fallbackTotal)
        {
            var roomPrice = await _context.RoomPrices
                .FirstOrDefaultAsync(rp => rp.PropertyId == propertyId && rp.RoomId == roomId);

            if (roomPrice != null && roomPrice.Amount > 0)
            {
                return roomPrice.Amount * nights;
            }

            var parsedFallback = ParseCurrency(fallbackTotal);
            return parsedFallback > 0 ? parsedFallback : nights * 2000000m;
        }

        private static decimal ParseCurrency(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return 0;
            var digits = new string(value.Where(char.IsDigit).ToArray());
            if (decimal.TryParse(digits, out var result))
            {
                return result;
            }

            return 0;
        }

        private string GenerateBookingCode()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 8)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        private string CreateBookingFormHtml(int propertyId, int roomId, string propertyName, string roomName, DateTime checkIn, DateTime checkOut, int guests)
        {
            var propertyNameJson = JsonSerializer.Serialize(propertyName);
            var roomNameJson = JsonSerializer.Serialize(roomName);
            var formId = $"bookingForm_{propertyId}_{roomId}_{Guid.NewGuid().ToString("N")[..6]}";
            var formIdJson = JsonSerializer.Serialize(formId);

            return $@"
            <div id='{formId}' style='margin-top: 20px; padding: 20px; background: rgba(255, 255, 255, 0.9); border-radius: 12px; border: 1px solid rgba(59, 130, 246, 0.2); box-shadow: 0 4px 15px rgba(59, 130, 246, 0.1); backdrop-filter: blur(10px);'>
                <h4 style='color: #1e40af; margin-bottom: 20px; display: flex; align-items: center;'>
                    <span style='margin-right: 10px;'>📝</span>
                    Thu thập thông tin đặt phòng
                </h4>
                
                <div style='background: #f0f9ff; padding: 15px; border-radius: 8px; margin-bottom: 20px; border-left: 4px solid #3b82f6;'>
                    <p style='margin: 0; color: #1e40af; font-weight: 500;'>
                        💬 <strong>Nếu bạn muốn tôi đặt phòng cho bạn, hãy cung cấp các thông tin sau:</strong>
                    </p>
                </div>
                
                <div style='display: grid; gap: 15px;'>
                    <div>
                        <label style='display: block; font-weight: 600; color: #374151; margin-bottom: 5px;'>👤 Họ và tên*</label>
                        <input type='text' name='bookingFullName' placeholder='Nhập họ tên như trên CMND (không dấu)' 
                               style='width: 100%; padding: 10px; border: 1px solid #d1d5db; border-radius: 6px; font-size: 14px;' />
                    </div>
                    
                    <div>
                        <label style='display: block; font-weight: 600; color: #374151; margin-bottom: 5px;'>📱 Số điện thoại*</label>
                        <input type='tel' name='bookingPhone' placeholder='VD: +84 8012345678' 
                               style='width: 100%; padding: 10px; border: 1px solid #d1d5db; border-radius: 6px; font-size: 14px;' />
                    </div>
                    
                    <div>
                        <label style='display: block; font-weight: 600; color: #374151; margin-bottom: 5px;'>📧 Email*</label>
                        <input type='email' name='bookingEmail' placeholder='VD: email@example.com' 
                               style='width: 100%; padding: 10px; border: 1px solid #d1d5db; border-radius: 6px; font-size: 14px;' />
                    </div>
                    
                    <div>
                        <label style='display: block; font-weight: 600; color: #374151; margin-bottom: 5px;'>💬 Yêu cầu đặc biệt (tùy chọn)</label>
                        <textarea name='bookingSpecialRequests' placeholder='Nhập yêu cầu đặc biệt của bạn (không bắt buộc)' 
                                  style='width: 100%; padding: 10px; border: 1px solid #d1d5db; border-radius: 6px; font-size: 14px; height: 80px; resize: vertical;'></textarea>
                    </div>
                </div>
                
                <div style='margin-top: 20px; text-align: center;'>
                    <button onclick='collectBookingInfo({formIdJson}, {propertyId}, {roomId}, {propertyNameJson}, {roomNameJson}, ""{checkIn:yyyy-MM-dd}"", ""{checkOut:yyyy-MM-dd}"", {guests})' 
                            style='background: #3b82f6; color: white; padding: 12px 24px; border: none; border-radius: 6px; font-weight: bold; cursor: pointer; font-size: 14px;'>
                        📋 Thu thập thông tin
                    </button>
                </div>
                
                <div class='booking-summary' style='display: none; margin-top: 20px; padding: 15px; background: #f0f9ff; border-radius: 8px; border: 1px solid #3b82f6;'>
                    <h5 style='color: #1e40af; margin-bottom: 15px;'>📋 Thông tin đặt phòng của bạn:</h5>
                    <div class='booking-details'></div>
                    <div style='margin-top: 15px; text-align: center;'>
                        <button onclick='confirmBooking()' 
                                style='background: #10b981; color: white; padding: 10px 20px; border: none; border-radius: 6px; font-weight: bold; cursor: pointer; margin-right: 10px;'>
                            ✅ Xác nhận đặt phòng
                        </button>
                        <button onclick='editBookingInfo({formIdJson})' 
                                style='background: #6b7280; color: white; padding: 10px 20px; border: none; border-radius: 6px; font-weight: bold; cursor: pointer;'>
                            ✏️ Chỉnh sửa
                        </button>
                    </div>
                </div>
            </div>
            
            <script>
            function collectBookingInfo(formId, propertyId, roomId, propertyName, roomName, checkIn, checkOut, guests) {{
                const formRoot = document.getElementById(formId);
                if (!formRoot) {{
                    alert('Không thể tìm thấy form đặt phòng.');
                    return;
                }}

                const fullName = (formRoot.querySelector(""[name='bookingFullName']"")?.value || '').trim();
                const phone = (formRoot.querySelector(""[name='bookingPhone']"")?.value || '').trim();
                const email = (formRoot.querySelector(""[name='bookingEmail']"")?.value || '').trim();
                const specialRequests = (formRoot.querySelector(""[name='bookingSpecialRequests']"")?.value || '').trim();
                
                if (!fullName || !phone || !email) {{
                    alert('Vui lòng điền đầy đủ thông tin bắt buộc (Họ tên, Số điện thoại, Email)');
                    return;
                }}
                
                // Hiển thị thông tin đã thu thập
                const summaryDiv = formRoot.querySelector('.booking-summary');
                const detailsDiv = formRoot.querySelector('.booking-details');
                
                detailsDiv.innerHTML = `
                    <div style='margin-bottom: 10px;'><strong>👤 Họ tên:</strong> ${{fullName}}</div>
                    <div style='margin-bottom: 10px;'><strong>📱 Số điện thoại:</strong> ${{phone}}</div>
                    <div style='margin-bottom: 10px;'><strong>📧 Email:</strong> ${{email}}</div>
                    <div style='margin-bottom: 10px;'><strong>📅 Ngày nhận phòng:</strong> ${{checkIn}}</div>
                    <div style='margin-bottom: 10px;'><strong>📅 Ngày trả phòng:</strong> ${{checkOut}}</div>
                    <div style='margin-bottom: 10px;'><strong>👥 Số khách:</strong> ${{guests}} người</div>
                    ${{specialRequests ? `<div style='margin-bottom: 10px;'><strong>💬 Yêu cầu đặc biệt:</strong> ${{specialRequests}}</div>` : ''}}
                `;
                
                if (summaryDiv) {{
                    summaryDiv.style.display = 'block';
                }}
                
                // Lưu thông tin vào localStorage để sử dụng sau
                localStorage.setItem('bookingInfo', JSON.stringify({{
                    formId: formId,
                    propertyId: propertyId,
                    propertyName: propertyName,
                    roomId: roomId,
                    roomName: roomName,
                    checkIn: checkIn,
                    checkOut: checkOut,
                    guests: guests,
                    fullName: fullName,
                    phone: phone,
                    email: email,
                    specialRequests: specialRequests
                }}));
            }}
            
            function confirmBooking() {{
                const bookingInfo = JSON.parse(localStorage.getItem('bookingInfo') || '{{}}');
                if (bookingInfo.propertyId) {{
                    // Load booking payment script dynamically
                    if (typeof BookingPayment === 'undefined') {{
                        const script = document.createElement('script');
                        script.src = '/js/booking-payment.js';
                        script.onload = function() {{
                            BookingPayment.showPaymentForm(bookingInfo);
                        }};
                        document.head.appendChild(script);
                    }} else {{
                        BookingPayment.showPaymentForm(bookingInfo);
                    }}
                }}
            }}
            
            function editBookingInfo(formId) {{
                const formRoot = document.getElementById(formId);
                if (!formRoot) return;
                const summary = formRoot.querySelector('.booking-summary');
                if (summary) summary.style.display = 'none';
            }}
            </script>";
        }

    }

    public class ChatRequest
    {
        public string Message { get; set; } = string.Empty;
        public string Context { get; set; } = string.Empty;
        public string CurrentUrl { get; set; } = string.Empty;
    }

    public class BookingEmailRequest
    {
        public int PropertyId { get; set; }
        public int RoomId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string CheckIn { get; set; } = string.Empty;
        public string CheckOut { get; set; } = string.Empty;
        public string Guests { get; set; } = string.Empty;
        public string BookingId { get; set; } = string.Empty;
        public string HotelName { get; set; } = string.Empty;
        public string RoomType { get; set; } = string.Empty;
        public string PropertyName { get; set; } = string.Empty;
        public string RoomName { get; set; } = string.Empty;
        public string TotalAmount { get; set; } = string.Empty;
        public string SpecialRequests { get; set; } = string.Empty;
    }

    public class OpenAIResponse
    {
        public Choice[]? choices { get; set; }
    }

    public class Choice
    {
        public Message? message { get; set; }
    }

    public class Message
    {
        public string? content { get; set; }
    }
}

public class HotelInfo
{
    public int PropertyId { get; set; }
    public int RoomId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal PriceFrom { get; set; }
    public double Rating { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
}