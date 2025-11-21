using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Http;

namespace HotelBooking.Controllers
{
    public class LanguageController : Controller
    {
        [HttpGet]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult SetLanguage(string culture, string returnUrl)
        {
            // Log để debug
            Console.WriteLine($"=== LANGUAGE SWITCH DEBUG ===");
            Console.WriteLine($"Setting culture: {culture}");
            Console.WriteLine($"ReturnUrl: {returnUrl}");
            Console.WriteLine($"Cookie name: {CookieRequestCultureProvider.DefaultCookieName}");
            
            var cookieValue = CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture));
            Console.WriteLine($"Cookie value: {cookieValue}");
            
            // Xóa cookie cũ trước khi set cookie mới
            Response.Cookies.Delete(CookieRequestCultureProvider.DefaultCookieName);
            
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                cookieValue,
                new CookieOptions { 
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    HttpOnly = false,  // Cho phép JavaScript đọc được để debug
                    Secure = false,   // Set true nếu dùng HTTPS
                    SameSite = SameSiteMode.Lax,
                    Path = "/"        // QUAN TRỌNG: Cookie có hiệu lực cho toàn bộ website
                }
            );

            Console.WriteLine("Cookie set successfully");
            Console.WriteLine($"=== END LANGUAGE SWITCH DEBUG ===");

            // Nếu returnUrl null hoặc rỗng, redirect về trang chủ
            if (string.IsNullOrEmpty(returnUrl) || returnUrl == "/")
            {
                return RedirectToAction("Index", "Home");
            }

            // Kiểm tra nếu returnUrl là /Partner/Review mà không có propertyId
            if (returnUrl == "/Partner/Review" || returnUrl.StartsWith("/Partner/Review?"))
            {
                // Nếu không có propertyId, redirect về Partner/Index
                if (!returnUrl.Contains("propertyId="))
                {
                    return RedirectToAction("Index", "Partner");
                }
            }

            // Kiểm tra nếu returnUrl là /Partner/PropertyData mà không có propertyId
            if (returnUrl == "/Partner/PropertyData" || returnUrl.StartsWith("/Partner/PropertyData?"))
            {
                // Nếu không có propertyId, redirect về Partner/MyProperties
                if (!returnUrl.Contains("propertyId="))
                {
                    return RedirectToAction("MyProperties", "Partner");
                }
                
                // Nếu có propertyId, giữ nguyên tất cả query parameters (bao gồm tab)
                return LocalRedirect(returnUrl);
            }

            return LocalRedirect(returnUrl);
        }
    }
}
