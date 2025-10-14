using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;
using HotelBooking.Data;
using Microsoft.EntityFrameworkCore;
using HotelBooking.Models;

namespace HotelBooking.Controllers
{
    public class ChatController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly ILogger<ChatController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public ChatController(IConfiguration configuration, HttpClient httpClient, ILogger<ChatController> logger, ApplicationDbContext context, IWebHostEnvironment env)
        {
            _configuration = configuration;
            _httpClient = httpClient;
            _logger = logger;
            _context = context;
            _env = env;
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
                    return Json(new { success = false, reply = "API key chưa được cấu hình." });
                }

                // Kiểm tra xem người dùng có hỏi về đặt phòng không
                var message = request.Message.ToLower();
                var isBookingRequest = message.Contains("đặt phòng") || message.Contains("tìm khách sạn") ||
                                     message.Contains("khách sạn") || message.Contains("resort") ||
                                     message.Contains("nghỉ dưỡng") || message.Contains("chỗ ở") ||
                                     message.Contains("booking") || message.Contains("đặt chỗ");

                var systemPrompt = @"Bạn là một AI assistant chuyên về du lịch và khách sạn.
Bạn giúp khách hàng tìm khách sạn phù hợp dựa trên nhu cầu của họ.
Hãy trả lời bằng tiếng Việt, thân thiện và hữu ích.

QUAN TRỌNG:
- Nếu khách hàng chỉ chào hỏi hoặc nói chuyện thông thường, hãy trả lời thân thiện và hỏi họ cần gì.
- KHÔNG được tự động đề xuất địa điểm cụ thể (như Quảng Ninh, Hà Nội...) trừ khi khách hàng hỏi về địa điểm đó.
- CHỈ KHI khách hàng hỏi về đặt phòng, tìm khách sạn, resort, chỗ ở thì mới hiển thị danh sách khách sạn thực tế từ hệ thống.
- Nếu có danh sách khách sạn được cung cấp, hãy hiển thị CHÍNH XÁC thông tin từ danh sách đó.
- TUYỆT ĐỐI KHÔNG được tạo ra hoặc gợi ý khách sạn khác ngoài danh sách được cung cấp.
- Nếu không có khách sạn nào trong danh sách, hãy thông báo rằng hiện tại chưa có khách sạn phù hợp.

ĐỊNH DẠNG TRẢ LỜI - QUAN TRỌNG:
- BẮT BUỘC sử dụng HTML tags thay vì markdown
- Sử dụng <strong> thay vì **
- Sử dụng <p> thay vì xuống dòng
- Sử dụng <ul> và <li> cho danh sách
- KHÔNG BAO GIỜ sử dụng ** hoặc ## hoặc bất kỳ markdown syntax nào
- Khi có hotel cards HTML được cung cấp, hãy SỬ DỤNG CHÍNH XÁC HTML đó trong câu trả lời
- KHÔNG được tạo ra HTML mới, chỉ sử dụng HTML đã được cung cấp
- Thêm phần 'Lý do lựa chọn' và 'Bạn có thể hỏi thêm' ở cuối.
- VÍ DỤ: <strong>Lý do lựa chọn:</strong> thay vì **Lý do lựa chọn:**";

                var userMessage = request.Message;
                var context = request.Context;

                // Chỉ lấy khách sạn khi thực sự cần thiết
                if (isBookingRequest)
                {
                    var hotels = await GetHotelsFromDatabase(request.Message, request.Context);

                    if (hotels.Any())
                    {
                        var hotelCardsHtml = "";
                        var hotelTextInfo = "\n\n=== DANH SÁCH KHÁCH SẠN THỰC TẾ TRONG HỆ THỐNG ===\n";
                        
                        foreach (var hotel in hotels.Take(3))
                        {
                            hotelCardsHtml += CreateHotelCardHtml(hotel);
                            hotelTextInfo += $"🏨 {hotel.Name}\n";
                            hotelTextInfo += $"📍 {hotel.Address}\n";
                            hotelTextInfo += $"⭐ {hotel.Rating}/5 sao\n";
                            hotelTextInfo += $"💰 Từ {hotel.PriceFrom:N0} VND/đêm\n";
                            hotelTextInfo += $"📝 {hotel.Description}\n";
                            hotelTextInfo += $"✅ Có sẵn để đặt phòng\n\n";
                        }
                        
                        var hotelInfo = $"{hotelTextInfo}\n\n{hotelCardsHtml}\n\nHƯỚNG DẪN: Hãy trình bày thông tin khách sạn trên một cách chi tiết, thân thiện và hấp dẫn. Thêm phần 'Lý do lựa chọn' và 'Bạn có thể hỏi thêm' ở cuối. Sử dụng emoji và format đẹp.";
                    
                        if (!string.IsNullOrEmpty(context))
                        {
                            userMessage = $"Ngữ cảnh tìm kiếm hiện tại: {context}{hotelInfo}\n\nCâu hỏi của khách hàng: {userMessage}";
                        }
                        else
                        {
                            userMessage = $"{hotelInfo}\n\nCâu hỏi của khách hàng: {userMessage}";
                        }
                    }
                    else
                    {
                        // Không có khách sạn nào trong database
                        userMessage = $"Hiện tại trong hệ thống chưa có khách sạn phù hợp với yêu cầu của bạn. Vui lòng thử lại sau hoặc liên hệ trực tiếp để được hỗ trợ.\n\nCâu hỏi của khách hàng: {userMessage}";
                    }
                }
                else
                {
                    // Trò chuyện bình thường, không cần thông tin khách sạn
                    // Chỉ gửi context nếu người dùng thực sự hỏi về địa điểm
                    var isLocationRequest = message.Contains("quảng ninh") || message.Contains("hà nội") ||
                                         message.Contains("đà nẵng") || message.Contains("hồ chí minh") ||
                                         message.Contains("hạ long") || message.Contains("đi đâu") ||
                                         message.Contains("ở đâu") || message.Contains("địa điểm");

                    if (!string.IsNullOrEmpty(context) && isLocationRequest)
                    {
                        userMessage = $"Ngữ cảnh: {context}\n\nCâu hỏi của khách hàng: {userMessage}";
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
                    max_tokens = 800,
                    temperature = 0.7
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("OpenAI API response status: {StatusCode}", response.StatusCode);
                _logger.LogInformation("OpenAI API response content: {Content}", responseContent);

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<OpenAIResponse>(responseContent);
                    var reply = result?.choices?.FirstOrDefault()?.message?.content ?? "Xin lỗi, tôi không thể trả lời câu hỏi này.";

                    _logger.LogInformation("AI reply generated: {Reply}", reply);
                    return Json(new { success = true, reply = reply });
                }
                else
                {
                    _logger.LogError("OpenAI API error: {StatusCode} - {Content}", response.StatusCode, responseContent);
                    return Json(new { success = false, reply = $"Có lỗi xảy ra khi kết nối với AI (HTTP {response.StatusCode}). Vui lòng thử lại sau." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in SendMessage");
                return Json(new { success = false, reply = $"Có lỗi xảy ra: {ex.Message}" });
            }
        }

        private async Task<List<HotelInfo>> GetHotelsFromDatabase(string message, string context)
        {
            try
            {
                // Tìm kiếm khách sạn dựa trên tin nhắn và ngữ cảnh
                var query = _context.Properties
                    .Where(p => p.Status == PropertyStatus.Approved)
                    .AsQueryable();

                // Tìm kiếm theo địa điểm từ tin nhắn hoặc ngữ cảnh
                var searchText = $"{message} {context}".ToLower();

                if (searchText.Contains("quảng ninh") || searchText.Contains("quang ninh") || searchText.Contains("hạ long") || searchText.Contains("ha long"))
                {
                    query = query.Where(p => p.City.ToLower().Contains("quảng ninh") ||
                                           p.City.ToLower().Contains("quang ninh") ||
                                           p.City.ToLower().Contains("hạ long") ||
                                           p.City.ToLower().Contains("ha long") ||
                                           p.AddressLine.ToLower().Contains("hạ long") ||
                                           p.AddressLine.ToLower().Contains("ha long") ||
                                           p.Name.ToLower().Contains("hạ long") ||
                                           p.Name.ToLower().Contains("ha long"));
                }
                else if (searchText.Contains("hà nội") || searchText.Contains("ha noi"))
                {
                    query = query.Where(p => p.City.ToLower().Contains("hà nội") ||
                                           p.City.ToLower().Contains("ha noi"));
                }
                else if (searchText.Contains("đà nẵng") || searchText.Contains("da nang"))
                {
                    query = query.Where(p => p.City.ToLower().Contains("đà nẵng") ||
                                           p.City.ToLower().Contains("da nang"));
                }
                else if (searchText.Contains("hồ chí minh") || searchText.Contains("ho chi minh") || searchText.Contains("sài gòn") || searchText.Contains("sai gon"))
                {
                    query = query.Where(p => p.City.ToLower().Contains("hồ chí minh") ||
                                           p.City.ToLower().Contains("ho chi minh") ||
                                           p.City.ToLower().Contains("sài gòn") ||
                                           p.City.ToLower().Contains("sai gon"));
                }
                else
                {
                    // Nếu không có địa điểm cụ thể, lấy tất cả khách sạn đã duyệt
                    query = query.Take(5);
                }

                // Lọc theo loại hình nếu người dùng yêu cầu cụ thể
                if (searchText.Contains("resort"))
                {
                    query = query.Where(p => p.Type == PropertyType.Resort);
                }
                else if (searchText.Contains("khách sạn") || searchText.Contains("hotel"))
                {
                    query = query.Where(p => p.Type == PropertyType.Hotel);
                }

                var properties = await query.Take(10).ToListAsync();

                var hotels = new List<HotelInfo>();
                foreach (var property in properties)
                {
                    // Lấy giá phòng thấp nhất (sử dụng LINQ to Objects để tránh lỗi SQLite)
                    var roomPrices = await _context.RoomPrices
                        .Where(rp => rp.PropertyId == property.Id)
                        .ToListAsync();
                    var minPrice = roomPrices.Any() ? roomPrices.Min(rp => rp.Amount) : 0;

                    // Lấy thông tin chi tiết từ PropertyData
                    var propertyData = await _context.PropertyData
                        .FirstOrDefaultAsync(pd => pd.PropertyId == property.Id);
                    var starRating = propertyData?.StarRating ?? 4;
                    var roomDescription = propertyData?.RoomDescription ?? "Phòng hiện đại với đầy đủ tiện nghi";

                    // Tạo mô tả chi tiết với loại hình
                    var propertyType = property.Type == PropertyType.Resort ? "Resort" : "Khách sạn";
                    var description = $"{propertyType} {roomDescription}. ";
                    if (propertyData?.HasRestaurant == true) description += "Có nhà hàng. ";
                    if (propertyData?.HasBar == true) description += "Có quầy bar. ";
                    if (propertyData?.HasParkingArea == true) description += "Có bãi đỗ xe. ";
                    if (propertyData?.HasPublicWifi == true) description += "Có WiFi miễn phí. ";

                    // Ảnh ưu tiên: đường dẫn DB -> ảnh trong thư mục uploads theo PropertyId -> fallback svg
                    var resolvedByDb = ResolveImageUrl(propertyData);
                    var resolvedByFolder = GetLocalPropertyImage(property.Id);
                    var finalImageUrl = !string.IsNullOrEmpty(resolvedByDb) ? resolvedByDb : resolvedByFolder;
                    if (string.IsNullOrEmpty(finalImageUrl)) finalImageUrl = "/images/default-hotel.svg";

                    hotels.Add(new HotelInfo
                    {
                        Name = property.Name,
                        Address = $"{property.AddressLine}, {property.City}",
                        Description = description.Trim(),
                        PriceFrom = minPrice,
                        Rating = starRating,
                        ImageUrl = finalImageUrl
                    });
                }

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
            
            var imageUrl = string.IsNullOrWhiteSpace(hotel.ImageUrl) ? "/images/default-hotel.svg" : hotel.ImageUrl;

            return $@"
<div class='search-card'>
  <div class='hotel-image'>
    <img class='hotel-img' src=""{imageUrl}"" alt=""{hotel.Name}"" onerror=""this.src='/images/default-hotel.svg'"" />
    <button class='heart-btn' title='Yêu thích'>❤</button>
  </div>
  <div class='hotel-details'>
    <div style='display:flex;align-items:center;gap:8px;'>
      <h3 class='hotel-name'>{hotel.Name}</h3>
      <span class='hotel-stars'>{stars}</span>
    </div>
    <div class='hotel-location'>{hotel.Address}</div>
    <div class='room-type'>Phòng tiêu chuẩn</div>
    <div class='promotion'>Bao gồm bữa sáng</div>
    <div class='warning'>Chỉ còn vài phòng với giá này trên trang của chúng tôi</div>
  </div>
  <div class='rating-section'>
    <div class='rating-score'>8,9</div>
    <div class='price'>VND {hotel.PriceFrom:N0}</div>
    <div class='price-includes'>+ thuế và phí</div>
    <button class='book-btn'>Xem chỗ trống</button>
  </div>
</div>";
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

            // If JSON array string, pick the first entry
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
                catch { /* ignore malformed json */ }
            }

            // If absolute URL, return as-is
            if (s.StartsWith("http://") || s.StartsWith("https://")) return s;

            // If delimited list, take the first item
            var first = s.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                         .FirstOrDefault()?.Trim('"', '\'', ' ') ?? string.Empty;
            if (string.IsNullOrEmpty(first)) return string.Empty;

            // Ensure leading slash for static file under wwwroot
            var normalized = first.StartsWith("/") ? first : "/" + first;
            _logger.LogInformation("ChatController: normalized image path => {Path}", normalized);
            return normalized;
        }
    }

    public class ChatRequest
    {
        public string Message { get; set; } = string.Empty;
        public string Context { get; set; } = string.Empty;
        public string CurrentUrl { get; set; } = string.Empty;
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
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal PriceFrom { get; set; }
    public double Rating { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
}