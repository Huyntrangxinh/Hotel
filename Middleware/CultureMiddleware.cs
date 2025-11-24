using Microsoft.AspNetCore.Localization;
using System.Globalization;

namespace HotelBooking.Middleware
{
    public class CultureMiddleware
    {
        private readonly RequestDelegate _next;

        public CultureMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var requestCulture = context.Features.Get<IRequestCultureFeature>();
            if (requestCulture != null)
            {
                var culture = requestCulture.RequestCulture.Culture;
                var uiCulture = requestCulture.RequestCulture.UICulture;
                
                // Force set thread culture
                Thread.CurrentThread.CurrentCulture = culture;
                Thread.CurrentThread.CurrentUICulture = uiCulture;
                
                // Set culture info
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = uiCulture;
                
                Console.WriteLine($"=== CULTURE MIDDLEWARE ===");
                Console.WriteLine($"Current Culture: {CultureInfo.CurrentCulture.Name}");
                Console.WriteLine($"Current UI Culture: {CultureInfo.CurrentUICulture.Name}");
                Console.WriteLine($"Thread Culture: {Thread.CurrentThread.CurrentCulture.Name}");
                Console.WriteLine($"Thread UI Culture: {Thread.CurrentThread.CurrentUICulture.Name}");
                Console.WriteLine($"=== END CULTURE MIDDLEWARE ===");
            }

            await _next(context);
        }
    }
}






