# 🌐 TỔNG HỢP HOÀN CHỈNH CODE ĐỔI NGÔN NGỮ

## 📁 **1. Controllers/LanguageController.cs**
```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Http;

namespace HotelBooking.Controllers
{
    public class LanguageController : Controller
    {
        [HttpGet]
        public IActionResult SetLanguage(string culture, string returnUrl)
        {
            // Log để debug
            Console.WriteLine($"=== LANGUAGE SWITCH DEBUG ===");
            Console.WriteLine($"Setting culture: {culture}");
            Console.WriteLine($"ReturnUrl: {returnUrl}");
            Console.WriteLine($"Cookie name: {CookieRequestCultureProvider.DefaultCookieName}");
            
            var cookieValue = CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture));
            Console.WriteLine($"Cookie value: {cookieValue}");
            
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                cookieValue,
                new CookieOptions { 
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    HttpOnly = true,  // Bảo mật hơn - chỉ server đọc được
                    Secure = false,   // Set true nếu dùng HTTPS
                    SameSite = SameSiteMode.Lax
                }
            );

            Console.WriteLine("Cookie set successfully");
            Console.WriteLine($"=== END LANGUAGE SWITCH DEBUG ===");

            // Nếu returnUrl null hoặc rỗng, redirect về trang chủ
            if (string.IsNullOrEmpty(returnUrl) || returnUrl == "/")
            {
                return RedirectToAction("Index", "Home");
            }

            return LocalRedirect(returnUrl);
        }
    }
}
```

## 📁 **2. Program.cs (Cấu hình Localization)**
```csharp
// Add Localization services
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[]
    {
        new CultureInfo("vi-VN"),
        new CultureInfo("en-US")
    };

    options.DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture("vi-VN");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;

    // Thêm cookie provider
    options.RequestCultureProviders.Clear();
    options.RequestCultureProviders.Add(new Microsoft.AspNetCore.Localization.CookieRequestCultureProvider());
    options.RequestCultureProviders.Add(new Microsoft.AspNetCore.Localization.QueryStringRequestCultureProvider());
});

// Middleware order - QUAN TRỌNG: Phải đặt trước UseRouting()
app.UseHttpsRedirection();
app.UseStaticFiles();

// Add Request Localization middleware - PHẢI ĐẶT TRƯỚC UseRouting()
app.UseRequestLocalization();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
```

## 📁 **3. Views/Shared/_LanguageSwitcher.cshtml**
```html
@using Microsoft.AspNetCore.Localization
@using Microsoft.AspNetCore.Mvc.Localization
@inject IStringLocalizer<SharedResource> Localizer

@{
    var requestCulture = Context.Features.Get<IRequestCultureFeature>();
    var currentCulture = requestCulture?.RequestCulture?.UICulture?.Name ?? "vi-VN";
}

<div class="language-switcher">
    <div class="dropdown">
        <button class="btn btn-outline-primary dropdown-toggle" type="button" id="languageDropdown"
            data-bs-toggle="dropdown" aria-expanded="false">
            <i class="fas fa-globe"></i> @Localizer["Language"]
        </button>
        <ul class="dropdown-menu" aria-labelledby="languageDropdown">
            <li>
                <a class="dropdown-item @(currentCulture == "vi-VN" ? "active" : "")"
                    href="@Url.Action("SetLanguage", "Language", new { culture = "vi-VN", returnUrl = Context.Request.Path })">
                    🇻🇳 @Localizer["Vietnamese"]
                </a>
            </li>
            <li>
                <a class="dropdown-item @(currentCulture == "en-US" ? "active" : "")"
                    href="@Url.Action("SetLanguage", "Language", new { culture = "en-US", returnUrl = Context.Request.Path })">
                    🇺🇸 @Localizer["English"]
                </a>
            </li>
        </ul>
    </div>
</div>

<style>
    .language-switcher {
        position: relative;
        margin-left: 10px; /* Thêm khoảng cách */
    }

    .language-switcher .dropdown-item.active {
        background-color: #007bff;
        color: white;
    }

    .language-switcher .dropdown-item:hover {
        background-color: #f8f9fa;
    }

    .language-switcher .dropdown-item {
        padding: 8px 16px; /* Thêm padding cho dropdown items */
        margin: 2px 0; /* Thêm margin giữa các items */
    }
</style>
```

## 📁 **4. Resources/SharedResource.cs**
```csharp
namespace HotelBooking.Resources
{
    public class SharedResource
    {
        // This class is used as a marker for localization
        // The actual resources are defined in .resx files
    }
}
```

## 📁 **5. Resources/SharedResource.vi.resx (Tiếng Việt)**
```xml
<?xml version="1.0" encoding="utf-8"?>
<root>
  <data name="Home" xml:space="preserve">
    <value>Trang chủ</value>
  </data>
  <data name="Hotels" xml:space="preserve">
    <value>Khách sạn</value>
  </data>
  <data name="Book" xml:space="preserve">
    <value>Đặt phòng</value>
  </data>
  <data name="Login" xml:space="preserve">
    <value>Đăng nhập</value>
  </data>
  <data name="Register" xml:space="preserve">
    <value>Đăng ký</value>
  </data>
  <data name="Search" xml:space="preserve">
    <value>Tìm kiếm</value>
  </data>
  <data name="Language" xml:space="preserve">
    <value>Ngôn ngữ</value>
  </data>
  <data name="Vietnamese" xml:space="preserve">
    <value>Tiếng Việt</value>
  </data>
  <data name="English" xml:space="preserve">
    <value>Tiếng Anh</value>
  </data>
  <data name="SignIn" xml:space="preserve">
    <value>Đăng nhập</value>
  </data>
  <data name="SignUp" xml:space="preserve">
    <value>Đăng ký</value>
  </data>
  <data name="Flights" xml:space="preserve">
    <value>Chuyến bay</value>
  </data>
  <data name="CarRental" xml:space="preserve">
    <value>Thuê xe</value>
  </data>
  <data name="Help" xml:space="preserve">
    <value>Trợ giúp</value>
  </data>
  <data name="FeaturedHotels" xml:space="preserve">
    <value>Khách sạn nổi bật</value>
  </data>
  <data name="RecentSearches" xml:space="preserve">
    <value>Tìm kiếm gần đây</value>
  </data>
  <data name="HeroTitle" xml:space="preserve">
    <value>Trang, kế tiếp bạn sẽ du lịch đến đâu?</value>
  </data>
  <data name="HeroSubtitle" xml:space="preserve">
    <value>Tìm ưu đãi Genius đặc biệt tại khắp nơi trên thế giới!</value>
  </data>
  <data name="SearchPlaceholder" xml:space="preserve">
    <value>Bạn muốn đến đâu?</value>
  </data>
  <data name="CheckInLabel" xml:space="preserve">
    <value>Ngày nhận phòng</value>
  </data>
  <data name="CheckOutLabel" xml:space="preserve">
    <value>Ngày trả phòng</value>
  </data>
  <data name="GuestsLabel" xml:space="preserve">
    <value>Khách</value>
  </data>
  <data name="SearchButton" xml:space="preserve">
    <value>Tìm</value>
  </data>
  <data name="OffersTitle" xml:space="preserve">
    <value>Ưu đãi</value>
  </data>
  <data name="ViewAllOffers" xml:space="preserve">
    <value>Xem tất cả ưu đãi</value>
  </data>
</root>
```

## 📁 **6. Resources/SharedResource.en.resx (Tiếng Anh)**
```xml
<?xml version="1.0" encoding="utf-8"?>
<root>
  <data name="Home" xml:space="preserve">
    <value>Home</value>
  </data>
  <data name="Hotels" xml:space="preserve">
    <value>Hotels</value>
  </data>
  <data name="Book" xml:space="preserve">
    <value>Book</value>
  </data>
  <data name="Login" xml:space="preserve">
    <value>Login</value>
  </data>
  <data name="Register" xml:space="preserve">
    <value>Register</value>
  </data>
  <data name="Search" xml:space="preserve">
    <value>Search</value>
  </data>
  <data name="Language" xml:space="preserve">
    <value>Language</value>
  </data>
  <data name="Vietnamese" xml:space="preserve">
    <value>Vietnamese</value>
  </data>
  <data name="English" xml:space="preserve">
    <value>English</value>
  </data>
  <data name="SignIn" xml:space="preserve">
    <value>Sign In</value>
  </data>
  <data name="SignUp" xml:space="preserve">
    <value>Sign Up</value>
  </data>
  <data name="Flights" xml:space="preserve">
    <value>Flights</value>
  </data>
  <data name="CarRental" xml:space="preserve">
    <value>Car Rental</value>
  </data>
  <data name="Help" xml:space="preserve">
    <value>Help</value>
  </data>
  <data name="FeaturedHotels" xml:space="preserve">
    <value>Featured Hotels</value>
  </data>
  <data name="RecentSearches" xml:space="preserve">
    <value>Recent Searches</value>
  </data>
  <data name="HeroTitle" xml:space="preserve">
    <value>Where are you going next?</value>
  </data>
  <data name="HeroSubtitle" xml:space="preserve">
    <value>Find special Genius offers all over the world!</value>
  </data>
  <data name="SearchPlaceholder" xml:space="preserve">
    <value>Where do you want to go?</value>
  </data>
  <data name="CheckInLabel" xml:space="preserve">
    <value>Check-in</value>
  </data>
  <data name="CheckOutLabel" xml:space="preserve">
    <value>Check-out</value>
  </data>
  <data name="GuestsLabel" xml:space="preserve">
    <value>Guests</value>
  </data>
  <data name="SearchButton" xml:space="preserve">
    <value>Search</value>
  </data>
  <data name="OffersTitle" xml:space="preserve">
    <value>Offers</value>
  </data>
  <data name="ViewAllOffers" xml:space="preserve">
    <value>View all offers</value>
  </data>
</root>
```

## 📁 **7. Views/_ViewImports.cshtml**
```html
@using HotelBooking
@using HotelBooking.Models
@using HotelBooking.Resources
@using Microsoft.Extensions.Localization
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
```

## 📁 **8. Views/Shared/_Layout.cshtml (Navigation)**
```html
@inject IStringLocalizer<SharedResource> Localizer

<!-- Navigation -->
<ul class="navbar-nav ms-auto align-items-center">
    <li class="nav-item">
        <a class="nav-link active" asp-area="" asp-controller="Home" asp-action="Index">
            <i class="bi bi-houses"></i> @Localizer["Hotels"]
        </a>
    </li>
    <li class="nav-item"><a class="nav-link" href="#"><i class="bi bi-airplane"></i> @Localizer["Flights"]</a></li>
    <li class="nav-item"><a class="nav-link" href="#"><i class="bi bi-car-front"></i> @Localizer["CarRental"]</a></li>
    <li class="nav-item"><a class="nav-link" href="#"><i class="bi bi-question-circle"></i> @Localizer["Help"]</a></li>
    <li class="nav-item">
        @await Html.PartialAsync("_LanguageSwitcher")
    </li>
    
    @if (User.Identity?.IsAuthenticated == true)
    {
        <!-- Authenticated user menu -->
    }
    else
    {
        <li class="nav-item me-2">
            <a asp-area="" asp-controller="Account" asp-action="Register" class="btn btn-light btn-sm ms-lg-3">@Localizer["SignUp"]</a>
        </li>
        <li class="nav-item">
            <a asp-area="" asp-controller="Account" asp-action="Login" class="btn btn-outline-light btn-sm ms-lg-2">@Localizer["SignIn"]</a>
        </li>
    }
</ul>
```

## 📁 **9. Views/Home/Index.cshtml (Localized Content)**
```html
@inject IStringLocalizer<SharedResource> Localizer
@{
    ViewData["Title"] = Localizer["Home"];
}

<!-- Hero Section -->
<section class="py-5" style="background:#003580; color:#fff;">
    <div class="container">
        <h1 class="fw-bold display-5 mb-2">@Localizer["HeroTitle"]</h1>
        <p class="fs-5 mb-4">@Localizer["HeroSubtitle"]</p>

        <div class="search-box">
            <form class="search-form" method="get" action="/Public/Search">
                <!-- Destination -->
                <div class="search-input-group position-relative">
                    <i class="bi bi-search"></i>
                    <input id="destinationInput" name="destination" placeholder="@Localizer["SearchPlaceholder"]" autocomplete="off" />
                </div>
                
                <!-- Search Button -->
                <button type="submit" class="search-btn">@Localizer["SearchButton"]</button>
            </form>
        </div>
    </div>
</section>

<!-- Offers Section -->
<section class="container my-4">
    <div class="d-flex justify-content-between align-items-center mb-2">
        <h5 class="section-title mb-0">@Localizer["OffersTitle"]</h5>
        <a asp-controller="Promo" asp-action="Index" class="btn btn-outline-primary btn-sm">@Localizer["ViewAllOffers"]</a>
    </div>
    <!-- Offers content -->
</section>

<!-- Featured Hotels Section -->
<section class="container my-4">
    <h5 class="section-title mb-3">@Localizer["FeaturedHotels"]</h5>
    <div class="row g-3">
        <!-- Hotel cards -->
    </div>
</section>

<!-- JavaScript for Recent Searches -->
<script>
    let html = '';
    if (items.length > 0) {
        html += '<div class="p-3 border-bottom text-dark"><strong>@Localizer["RecentSearches"]</strong></div>';
        // ... rest of the code
    }
</script>
```

## 🔧 **CÁCH HOẠT ĐỘNG:**

1. **User bấm dropdown** → Gọi `LanguageController.SetLanguage()`
2. **Controller set cookie** `AspNetCore.Culture` với culture value
3. **Middleware đọc cookie** và set culture cho request
4. **Views sử dụng** `@Localizer["Key"]` để hiển thị text đúng ngôn ngữ

## 🚀 **TESTING:**

1. **Mở trình duyệt:** `http://localhost:5187`
2. **Bấm dropdown "🌐 Language"**
3. **Chọn "🇺🇸 English" hoặc "🇻🇳 Tiếng Việt"**
4. **Kiểm tra Developer Tools → Application → Cookies**
5. **Xem cookie `AspNetCore.Culture`**
6. **Refresh trang và kiểm tra text có thay đổi**

## 📝 **LƯU Ý QUAN TRỌNG:**

- **Middleware order:** `UseRequestLocalization()` PHẢI đặt trước `UseRouting()`
- **Cookie name:** `.AspNetCore.Culture`
- **Cookie value:** `c=en-US|uic=en-US` (cho English)
- **Default culture:** `vi-VN`
- **Supported cultures:** `vi-VN`, `en-US`
- **Resource files:** Phải có cùng keys trong cả 2 file `.resx`
- **Debug logs:** Sẽ thấy logs trong terminal khi đổi ngôn ngữ

## 🐛 **TROUBLESHOOTING:**

1. **Nếu text hiển thị như placeholder** → Kiểm tra resource files có đúng keys không
2. **Nếu không có cookie** → Kiểm tra LanguageController có được gọi không
3. **Nếu không đổi ngôn ngữ** → Kiểm tra middleware order trong Program.cs
4. **Nếu dropdown không hoạt động** → Kiểm tra Bootstrap JavaScript có load không

## 📋 **CHECKLIST:**

- ✅ LanguageController.cs
- ✅ Program.cs (middleware order)
- ✅ _LanguageSwitcher.cshtml
- ✅ SharedResource.cs
- ✅ SharedResource.vi.resx
- ✅ SharedResource.en.resx
- ✅ _ViewImports.cshtml
- ✅ _Layout.cshtml (navigation)
- ✅ Home/Index.cshtml (content)
- ✅ CSS styling cho dropdown
- ✅ Debug logs
- ✅ Cookie security (HttpOnly = true)






