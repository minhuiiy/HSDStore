using Microsoft.AspNetCore.Mvc;
using CPTStore.Models;
using CPTStore.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using CPTStore.Extensions;
using System.Threading.Tasks;
using System;
using System.Security.Claims;

namespace CPTStore.Controllers
{
    [Authorize]
    public class MembershipController : Controller
    {
        private readonly IMembershipService _membershipService;
        private readonly ICartService _cartService;

        public MembershipController(IMembershipService membershipService, ICartService cartService)
        {
            _membershipService = membershipService;
            _cartService = cartService;
        }

        // GET: /Membership
        public async Task<IActionResult> Index()
        {
            string userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account");
            }

            var membershipLevel = await _membershipService.GetMembershipLevelAsync(userId);
            var totalPurchases = await _membershipService.GetTotalPurchasesAsync(userId);
            var membershipDiscounts = await _membershipService.GetMembershipDiscountsByLevelAsync(membershipLevel);

            ViewBag.MembershipLevel = membershipLevel;
            ViewBag.TotalPurchases = totalPurchases;

            return View(membershipDiscounts);
        }

        // POST: /Membership/ApplyMembershipDiscount
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApplyMembershipDiscount(string discountCode)
        {
            if (string.IsNullOrWhiteSpace(discountCode))
            {
                TempData["Error"] = "Vui lòng nhập mã giảm giá";
                return RedirectToAction(nameof(Index));
            }

            string userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                // Kiểm tra xem mã giảm giá có phải là mã giảm giá thành viên hợp lệ không
                var membershipLevel = await _membershipService.GetMembershipLevelAsync(userId);
                var isValidMembershipDiscount = await _membershipService.IsValidMembershipDiscountAsync(discountCode, membershipLevel);

                if (!isValidMembershipDiscount)
                {
                    TempData["Error"] = "Mã giảm giá không hợp lệ hoặc không áp dụng cho cấp độ thành viên của bạn";
                    return RedirectToAction(nameof(Index));
                }

                // Áp dụng mã giảm giá vào giỏ hàng
                var discountAmount = await _cartService.ApplyDiscountAsync(userId, discountCode);
                TempData["Success"] = $"Áp dụng mã giảm giá thành công. Bạn được giảm {discountAmount:N0} VNĐ";

                return RedirectToAction("Index", "Cart");
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }
    }
}