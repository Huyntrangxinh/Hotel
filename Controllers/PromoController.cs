using HotelBooking.Data;
using HotelBooking.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using HotelBooking.Resources;

namespace HotelBooking.Controllers
{
    public class PromoController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IStringLocalizer<SharedResource> _localizer;
        
        public PromoController(ApplicationDbContext db, IStringLocalizer<SharedResource> localizer) 
        { 
            _db = db; 
            _localizer = localizer;
        }

        [HttpGet("/Promo")]
        public async Task<IActionResult> Index()
        {
            var today = DateTime.Today;
            var items = await _db.Discounts
                .Where(d => d.IsActive && (!d.StartDate.HasValue || d.StartDate <= today) && (!d.EndDate.HasValue || d.EndDate >= today))
                .OrderByDescending(d => d.Id)
                .ToListAsync();
            return View(items);
        }

        [HttpGet("/Promo/{id}")]
        public async Task<IActionResult> Details(int id)
        {
            var today = DateTime.Today;
            var promo = await _db.Discounts
                .FirstOrDefaultAsync(d => d.Id == id && d.IsActive && (!d.StartDate.HasValue || d.StartDate <= today) && (!d.EndDate.HasValue || d.EndDate >= today));
            if (promo == null) return NotFound();
            
            // Load Property nếu có PropertyId
            Property? property = null;
            if (promo.PropertyId.HasValue)
            {
                property = await _db.Properties
                    .FirstOrDefaultAsync(p => p.Id == promo.PropertyId.Value);
            }
            
            ViewBag.Property = property;
            return View(promo);
        }
    }
}


