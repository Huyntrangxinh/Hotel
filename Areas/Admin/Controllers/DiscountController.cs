using HotelBooking.Data;
using HotelBooking.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelBooking.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize]
    public class DiscountController : Controller
    {
        private readonly ApplicationDbContext _db;
        public DiscountController(ApplicationDbContext db) { _db = db; }

        public async Task<IActionResult> Index()
        {
            var items = await _db.Discounts.OrderByDescending(x => x.Id).ToListAsync();
            return View(items);
        }

        public IActionResult Create()
        {
            return View(new Discount { StartDate = DateTime.Today, EndDate = DateTime.Today.AddMonths(1) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Discount model)
        {
            ValidateDiscountModel(model);
            if (!ModelState.IsValid) return View(model);

            model.Code = model.Code.Trim().ToUpperInvariant();
            _db.Discounts.Add(model);
            await _db.SaveChangesAsync();
            TempData["success"] = "Đã tạo ưu đãi";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var discount = await _db.Discounts.FindAsync(id);
            if (discount == null) return NotFound();
            return View(discount);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Discount model)
        {
            if (id != model.Id) return NotFound();

            ValidateDiscountModel(model);
            if (!ModelState.IsValid) return View(model);

            try
            {
                model.Code = model.Code.Trim().ToUpperInvariant();
                _db.Discounts.Update(model);
                await _db.SaveChangesAsync();
                TempData["success"] = "Đã cập nhật ưu đãi";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _db.Discounts.AnyAsync(d => d.Id == id))
                {
                    return NotFound();
                }
                throw;
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var discount = await _db.Discounts.FindAsync(id);
            if (discount == null)
            {
                TempData["success"] = "Không tìm thấy mã ưu đãi";
                return RedirectToAction(nameof(Index));
            }

            _db.Discounts.Remove(discount);
            await _db.SaveChangesAsync();
            TempData["success"] = "Đã xóa mã ưu đãi";
            return RedirectToAction(nameof(Index));
        }

        private void ValidateDiscountModel(Discount model)
        {
            if (string.IsNullOrWhiteSpace(model.Code))
            {
                ModelState.AddModelError(nameof(model.Code), "Mã không được để trống");
            }
            if (model.DiscountPercent is null && model.DiscountAmount is null)
            {
                ModelState.AddModelError(string.Empty, "Cần nhập phần trăm hoặc số tiền giảm");
            }
        }
    }
}



