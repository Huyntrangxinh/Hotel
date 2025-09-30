using HotelBooking.Data;
using HotelBooking.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelBooking.Controllers
{
    public class PromoController : Controller
    {
        private readonly ApplicationDbContext _db;
        public PromoController(ApplicationDbContext db) { _db = db; }

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
            return View(promo);
        }
    }
}


