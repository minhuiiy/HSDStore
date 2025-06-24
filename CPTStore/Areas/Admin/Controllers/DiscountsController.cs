using Microsoft.AspNetCore.Mvc;
using CPTStore.Models;
using CPTStore.Services.Interfaces;
using System;
using System.Threading.Tasks;
using System.Linq;

namespace CPTStore.Areas.Admin.Controllers
{
    public class DiscountsController : AdminControllerBase
    {
        private readonly IDiscountService _discountService;

        public DiscountsController(IDiscountService discountService)
        {
            _discountService = discountService;
        }

        // GET: Admin/Discounts
        public async Task<IActionResult> Index(string searchTerm, bool? isActive, int page = 1, int pageSize = 10)
        {
            var discounts = await _discountService.GetDiscountsAsync(searchTerm, isActive, page, pageSize);
            var totalCount = await _discountService.GetTotalDiscountCountAsync(searchTerm, isActive);

            ViewBag.SearchTerm = searchTerm;
            ViewBag.IsActive = isActive;
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            return View(discounts);
        }

        // GET: Admin/Discounts/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var discount = await _discountService.GetDiscountByIdAsync(id);
            if (discount == null)
            {
                return NotFound();
            }

            return View(discount);
        }

        // GET: Admin/Discounts/Create
        public IActionResult Create()
        {
            var discount = new Discount
            {
                IsActive = true,
                StartDate = DateTime.Now,
                EndDate = DateTime.Now.AddDays(30),
                CreatedAt = DateTime.Now
            };

            return View(discount);
        }

        // POST: Admin/Discounts/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Discount discount)
        {
            if (ModelState.IsValid)
            {
                discount.CreatedAt = DateTime.Now;
                await _discountService.CreateDiscountAsync(discount);
                return RedirectToAction(nameof(Index));
            }
            return View(discount);
        }

        // GET: Admin/Discounts/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var discount = await _discountService.GetDiscountByIdAsync(id);
            if (discount == null)
            {
                return NotFound();
            }
            return View(discount);
        }

        // POST: Admin/Discounts/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Discount discount)
        {
            if (id != discount.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    discount.UpdatedAt = DateTime.Now;
                    await _discountService.UpdateDiscountAsync(discount);
                }
                catch (Exception)
                {
                    if (!await DiscountExists(discount.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(discount);
        }

        // GET: Admin/Discounts/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var discount = await _discountService.GetDiscountByIdAsync(id);
            if (discount == null)
            {
                return NotFound();
            }

            return View(discount);
        }

        // POST: Admin/Discounts/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await _discountService.DeleteDiscountAsync(id);
            return RedirectToAction(nameof(Index));
        }

        // POST: Admin/Discounts/ToggleStatus/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var discount = await _discountService.GetDiscountByIdAsync(id);
            if (discount == null)
            {
                return NotFound();
            }

            discount.IsActive = !discount.IsActive;
            discount.UpdatedAt = DateTime.Now;

            await _discountService.UpdateDiscountAsync(discount);
            return RedirectToAction(nameof(Index));
        }

        private async Task<bool> DiscountExists(int id)
        {
            return await _discountService.GetDiscountByIdAsync(id) != null;
        }
    }
}