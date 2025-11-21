using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using HotelBooking.Models;
using HotelBooking.Data;
using QuestPDF.Infrastructure; // Dòng này đã đúng vị trí
using Microsoft.Extensions.Localization;
using System.Globalization;
using Microsoft.AspNetCore.Localization;
using HotelBooking.Services;
using HotelBooking.Filters;

namespace HotelBooking
{
    public class Program
    {
        public static async Task  Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // === THÊM DÒNG NÀY ĐỂ KHAI BÁO LICENSE ===
            QuestPDF.Settings.License = LicenseType.Community;
            
            // Add services to the container.
            builder.Services
                .AddControllersWithViews(options =>
                {
                    options.Filters.Add<UserHasPropertiesFilter>();
                })
                .AddViewLocalization()
                .AddDataAnnotationsLocalization();
            
            // Add HttpClient for OpenAI API
            builder.Services.AddHttpClient();
            builder.Services.AddScoped<IBookingEmailService, BookingEmailService>();

            // Add Localization services
            builder.Services.AddLocalization();
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

            // Kết nối SQLite từ appsettings.json
            builder.Services.AddDbContext<ApplicationDbContext>(opt =>
            {
                opt.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"));
            });

            // Đăng kí ASP.NET Core Identity
            builder.Services
                .AddIdentity<ApplicationUser, IdentityRole>(opt =>
                {
                    opt.Password.RequiredLength = 6;
                    opt.Password.RequireDigit = false;
                    opt.Password.RequireUppercase = false;
                    opt.Password.RequireNonAlphanumeric = false;
                    opt.User.RequireUniqueEmail = true;
                })
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();

            // Cấu hình đường dẫn đăng nhập/không có quyền
            builder.Services.ConfigureApplicationCookie(o =>
            {
                o.LoginPath = "/account/login";
                o.AccessDeniedPath = "/account/access-denied";
            });

            // Google OAuth (đọc từ appsettings)
            var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
            var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
            if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
            {
                builder.Services.AddAuthentication().AddGoogle(o =>
                {
                    o.ClientId = googleClientId;
                    o.ClientSecret = googleClientSecret;
                    o.Scope.Add("profile");
                    o.Scope.Add("email");
                    o.SaveTokens = true;
                    // Redirect back to login/register when user cancels on Google consent
                    o.Events.OnRemoteFailure = context =>
                    {
                        var returnUrl = context.Properties?.RedirectUri ?? "/";
                        context.Response.Redirect(returnUrl.Contains("/Account/Register", StringComparison.OrdinalIgnoreCase)
                            ? "/Account/Register"
                            : "/Account/Login");
                        context.HandleResponse();
                        return Task.CompletedTask;
                    };
                });
            }

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            // Add Request Localization middleware - PHẢI ĐẶT TRƯỚC UseRouting()
            app.UseRequestLocalization();
            
            // Add custom culture middleware
            app.UseMiddleware<HotelBooking.Middleware.CultureMiddleware>();
            
            // Force set thread culture
            CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("vi-VN");
            CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("vi-VN");

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();


            

            app.MapControllerRoute(
    name: "Admin",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");



            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");



// ...
using (var scope = app.Services.CreateScope())
{
    await DbSeeder.SeedRolesAndAdminAsync(scope.ServiceProvider);
}
            app.Run();
        }
    }
}