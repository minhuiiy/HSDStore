using Microsoft.AspNetCore.Mvc;
using CPTStore.Models;
using CPTStore.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using CPTStore.Extensions;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using CPTStore.ViewModels;

namespace CPTStore.Controllers
{
    [Authorize]
    public class DiscountController : Controller
    {
        public DiscountController(
            IMembershipService membershipService, 
            IDiscountService discountService, 
            ICartService cartService,
            ISavedDiscountService savedDiscountService)
        {
            _membershipService = membershipService;
            _discountService = discountService;
            _cartService = cartService;
            _savedDiscountService = savedDiscountService;
        }

        private readonly IMembershipService _membershipService;
        private readonly IDiscountService _discountService;
        private readonly ICartService _cartService;
        private readonly ISavedDiscountService _savedDiscountService;

        // GET: /Discount
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
            var generalDiscounts = await _discountService.GetActiveDiscountsAsync();
            var savedDiscounts = await _savedDiscountService.GetSavedDiscountsByUserIdAsync(userId);

            var viewModel = new DiscountViewModel
            {
                MembershipLevel = membershipLevel,
                TotalPurchases = totalPurchases,
                MembershipDiscounts = membershipDiscounts,
                GeneralDiscounts = generalDiscounts,
                SavedDiscounts = savedDiscounts
            };

            return View(viewModel);
        }

        // POST: /Discount/ApplyMembershipDiscount
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
                
                // Kiểm tra xem người dùng đã lưu mã giảm giá này chưa
                if (await _savedDiscountService.HasUserSavedDiscountAsync(userId, discountCode))
                {
                    // Đánh dấu mã giảm giá đã được sử dụng
                    await _savedDiscountService.MarkAsUsedAsync(userId, discountCode);
                }
                
                // Tăng số lần sử dụng của mã giảm giá
                var discount = await _membershipService.GetMembershipDiscountByCodeAsync(discountCode);
                if (discount != null)
                {
                    await _discountService.IncrementUsageCountAsync(discount.Id);
                }
                
                TempData["Success"] = $"Áp dụng mã giảm giá thành công. Bạn được giảm {discountAmount:N0} VNĐ";

                return RedirectToAction("Index", "Cart");
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: /Discount/ApplyGeneralDiscount
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApplyGeneralDiscount(string discountCode)
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
                // Kiểm tra xem mã giảm giá có hợp lệ không
                var cartTotal = await _cartService.GetCartTotalAsync(userId);
                var isValidDiscount = await _discountService.IsDiscountValidAsync(discountCode, cartTotal);

                if (!isValidDiscount)
                {
                    TempData["Error"] = "Mã giảm giá không hợp lệ hoặc không áp dụng được cho đơn hàng này";
                    return RedirectToAction(nameof(Index));
                }

                // Áp dụng mã giảm giá vào giỏ hàng
                var discountAmount = await _cartService.ApplyDiscountAsync(userId, discountCode);
                
                // Kiểm tra xem người dùng đã lưu mã giảm giá này chưa
                if (await _savedDiscountService.HasUserSavedDiscountAsync(userId, discountCode))
                {
                    // Đánh dấu mã giảm giá đã được sử dụng
                    await _savedDiscountService.MarkAsUsedAsync(userId, discountCode);
                }
                
                // Tăng số lần sử dụng của mã giảm giá
                var discount = await _discountService.GetDiscountByCodeAsync(discountCode);
                if (discount != null)
                {
                    await _discountService.IncrementUsageCountAsync(discount.Id);
                }
                
                TempData["Success"] = $"Áp dụng mã giảm giá thành công. Bạn được giảm {discountAmount:N0} VNĐ";

                return RedirectToAction("Index", "Cart");
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: /Discount/SaveDiscount
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveDiscount(string discountCode)
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
                // Kiểm tra xem mã giảm giá có hợp lệ không
                var discount = await _discountService.GetDiscountByCodeAsync(discountCode);
                if (discount == null || !discount.IsValid)
                {
                    TempData["Error"] = "Mã giảm giá không hợp lệ hoặc đã hết hạn";
                    return RedirectToAction(nameof(Index));
                }

                // Kiểm tra xem người dùng đã lưu mã giảm giá này chưa
                if (await _savedDiscountService.HasUserSavedDiscountAsync(userId, discountCode))
                {
                    TempData["Error"] = "Bạn đã lưu mã giảm giá này rồi";
                    return RedirectToAction(nameof(Index));
                }

                // Lưu mã giảm giá
                await _savedDiscountService.SaveDiscountAsync(userId, discountCode);
                TempData["Success"] = "Lưu mã giảm giá thành công";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: /Discount/UseSavedDiscount
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UseSavedDiscount(int id)
        {
            string userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                // Lấy thông tin mã giảm giá đã lưu
                var savedDiscount = await _savedDiscountService.GetSavedDiscountByIdAsync(id);
                if (savedDiscount == null)
                {
                    TempData["Error"] = "Mã giảm giá không tồn tại";
                    return RedirectToAction(nameof(Index));
                }

                // Kiểm tra xem mã giảm giá có thuộc về người dùng không
                if (savedDiscount.UserId != userId)
                {
                    TempData["Error"] = "Bạn không có quyền sử dụng mã giảm giá này";
                    return RedirectToAction(nameof(Index));
                }

                // Kiểm tra xem mã giảm giá đã được sử dụng chưa
                if (savedDiscount.IsUsed)
                {
                    TempData["Error"] = "Mã giảm giá này đã được sử dụng";
                    return RedirectToAction(nameof(Index));
                }

                // Kiểm tra xem mã giảm giá có còn hiệu lực không
                var discount = await _discountService.GetDiscountByCodeAsync(savedDiscount.DiscountCode);
                if (discount == null || !discount.IsValid)
                {
                    TempData["Error"] = "Mã giảm giá không hợp lệ hoặc đã hết hạn";
                    return RedirectToAction(nameof(Index));
                }

                // Kiểm tra xem mã giảm giá có áp dụng được cho giỏ hàng không
                var cartTotal = await _cartService.GetCartTotalAsync(userId);
                if (!await _discountService.IsDiscountValidAsync(savedDiscount.DiscountCode, cartTotal))
                {
                    TempData["Error"] = "Mã giảm giá không áp dụng được cho đơn hàng này";
                    return RedirectToAction(nameof(Index));
                }

                // Áp dụng mã giảm giá vào giỏ hàng
                var discountAmount = await _cartService.ApplyDiscountAsync(userId, savedDiscount.DiscountCode);
                
                // Đánh dấu mã giảm giá đã được sử dụng
                await _savedDiscountService.MarkAsUsedAsync(id);
                
                // Tăng số lần sử dụng của mã giảm giá
                await _discountService.IncrementUsageCountAsync(discount.Id);

                TempData["Success"] = $"Áp dụng mã giảm giá thành công. Bạn được giảm {discountAmount:N0} VNĐ";

                return RedirectToAction("Index", "Cart");
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: /Discount/DeleteSavedDiscount
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSavedDiscount(int id)
        {
            string userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                // Lấy thông tin mã giảm giá đã lưu
                var savedDiscount = await _savedDiscountService.GetSavedDiscountByIdAsync(id);
                if (savedDiscount == null)
                {
                    TempData["Error"] = "Mã giảm giá không tồn tại";
                    return RedirectToAction(nameof(Index));
                }

                // Kiểm tra xem mã giảm giá có thuộc về người dùng không
                if (savedDiscount.UserId != userId)
                {
                    TempData["Error"] = "Bạn không có quyền xóa mã giảm giá này";
                    return RedirectToAction(nameof(Index));
                }

                // Xóa mã giảm giá đã lưu
                await _savedDiscountService.DeleteSavedDiscountAsync(id);
                TempData["Success"] = "Xóa mã giảm giá thành công";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }
    }
}