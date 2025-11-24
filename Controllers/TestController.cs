using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using HotelBooking.Resources;

namespace HotelBooking.Controllers
{
    public class TestController : Controller
    {
        private readonly IStringLocalizer<SharedResource> _localizer;

        public TestController(IStringLocalizer<SharedResource> localizer)
        {
            _localizer = localizer;
        }

        public IActionResult Index()
        {
            var model = new
            {
                Home = _localizer["Home"],
                Hotels = _localizer["Hotels"],
                Search = _localizer["Search"],
                Language = _localizer["Language"],
                HeroTitle = _localizer["HeroTitle"],
                OffersTitle = _localizer["OffersTitle"],
                CurrentCulture = System.Globalization.CultureInfo.CurrentCulture.Name,
                CurrentUICulture = System.Globalization.CultureInfo.CurrentUICulture.Name,
                ThreadCulture = System.Threading.Thread.CurrentThread.CurrentCulture.Name,
                ThreadUICulture = System.Threading.Thread.CurrentThread.CurrentUICulture.Name
            };

            return Json(model);
        }
    }
}






