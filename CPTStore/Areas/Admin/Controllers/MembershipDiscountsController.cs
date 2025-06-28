using CPTStore.Models;
using CPTStore.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace CPTStore.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class MembershipDiscountsController : Controller
    {
        private readonly IMembershipService _membershipService;

        public MembershipDiscountsController(IMembershipService membershipService)
        {
            _membershipService = membershipService;
        }

        public async Task<IActionResult> Index()
        {
            var discounts = await _membershipService.GetAllMembershipDiscountsAsync();
            return View(discounts);
        }

        public IActionResult Create()
        {
            var discount = new MembershipDiscount
            {
                IsActive = true,
                CreatedAt = DateTime.Now
            };
            return View(discount);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MembershipDiscount discount)
        {
            if (ModelState.IsValid)
            {
                await _membershipService.CreateMembershipDiscountAsync(discount);
                TempData["Success"] = "Mã giảm giá thành viên đã được tạo thành công";
                return RedirectToAction(nameof(Index));
            }
            return View(discount);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var discount = await _membershipService.GetMembershipDiscountByIdAsync(id);
            if (discount == null)
            {
                return NotFound();
            }
            return View(discount);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, MembershipDiscount discount)
        {
            if (id != discount.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                discount.UpdatedAt = DateTime.Now;
                await _membershipService.UpdateMembershipDiscountAsync(discount);
                TempData["Success"] = "Mã giảm giá thành viên đã được cập nhật thành công";
                return RedirectToAction(nameof(Index));
            }
            return View(discount);
        }

        public async Task<IActionResult> Details(int id)
        {
            var discount = await _membershipService.GetMembershipDiscountByIdAsync(id);
            if (discount == null)
            {
                return NotFound();
            }
            return View(discount);
        }

        public async Task<IActionResult> Delete(int id)
        {
            var discount = await _membershipService.GetMembershipDiscountByIdAsync(id);
            if (discount == null)
            {
                return NotFound();
            }
            return View(discount);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await _membershipService.DeleteMembershipDiscountAsync(id);
            TempData["Success"] = "Mã giảm giá thành viên đã được xóa thành công";
            return RedirectToAction(nameof(Index));
        }
    }
}