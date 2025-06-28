using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Diagnostics;
using CPTStore.Models;

namespace CPTStore.Controllers
{
    public class ErrorController : Controller
    {
        private readonly ILogger<ErrorController> _logger;

        public ErrorController(ILogger<ErrorController> logger)
        {
            _logger = logger;
        }

        [Route("Error")]
        public IActionResult Index(int? statusCode = null, string? message = null)
        {
            var viewModel = new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                StatusCode = statusCode,
                Message = message ?? "Đã xảy ra lỗi không xác định"
            };

            // Nếu không có mã trạng thái được chỉ định, hãy thử lấy từ IExceptionHandlerPathFeature
            if (!statusCode.HasValue)
            {
                var exceptionFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
                if (exceptionFeature != null)
                {
                    _logger.LogError(exceptionFeature.Error, "Đã xảy ra lỗi không xử lý được");
                    viewModel.Message = exceptionFeature.Error.Message;
                    
                    // Kiểm tra nếu là lỗi tồn kho
                    if (exceptionFeature.Error is InvalidOperationException && 
                        exceptionFeature.Error.Message.Contains("không đủ số lượng trong kho"))
                    {
                        return RedirectToAction("OutOfStock", new { message = exceptionFeature.Error.Message });
                    }
                }
            }

            // Đặt mã trạng thái phản hồi nếu có
            if (statusCode.HasValue)
            {
                HttpContext.Response.StatusCode = statusCode.Value;
            }

            return View(viewModel);
        }
        
        [Route("OutOfStock")]
        public IActionResult OutOfStock(string message)
        {
            var viewModel = new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                StatusCode = 400,
                Message = message ?? "Sản phẩm không đủ số lượng trong kho",
                Title = "Lỗi tồn kho",
                SuggestedAction = "Vui lòng kiểm tra lại giỏ hàng của bạn và giảm số lượng sản phẩm hoặc chọn sản phẩm khác. Bạn cũng có thể thử đồng bộ hóa tồn kho để cập nhật thông tin mới nhất."
            };
            
            return View("OutOfStock", viewModel);
        }
    }
}