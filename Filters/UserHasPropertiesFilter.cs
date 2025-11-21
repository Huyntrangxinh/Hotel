using HotelBooking.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using HotelBooking.Models;

namespace HotelBooking.Filters
{
    public class UserHasPropertiesFilter : IAsyncActionFilter
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public UserHasPropertiesFilter(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // Set default value
            if (context.Controller is Controller controller)
            {
                controller.ViewBag.UserHasProperties = false;
            }

            // Check if user is signed in
            if (context.HttpContext.User.Identity?.IsAuthenticated == true)
            {
                var userId = _userManager.GetUserId(context.HttpContext.User);
                if (!string.IsNullOrEmpty(userId))
                {
                    var hasProperties = await _db.Properties.AnyAsync(p => p.UserId == userId);
                    if (context.Controller is Controller controller2)
                    {
                        controller2.ViewBag.UserHasProperties = hasProperties;
                    }
                }
            }

            await next();
        }
    }
}

