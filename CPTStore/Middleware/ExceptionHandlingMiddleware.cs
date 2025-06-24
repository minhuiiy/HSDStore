using CPTStore.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

namespace CPTStore.Middleware
{
    /// <summary>
    /// Middleware xử lý ngoại lệ toàn cục
    /// </summary>
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            _logger.LogError(exception, "Đã xảy ra lỗi: {Message}", exception.Message);

            var response = context.Response;
            response.ContentType = "application/json";

            var errorResponse = new ErrorResponse
            {
                Success = false
            };

            switch (exception)
            {
                case OrderServiceException orderEx:
                    response.StatusCode = GetStatusCodeFromOrderErrorCode(orderEx.ErrorCode);
                    errorResponse.Message = orderEx.Message;
                    errorResponse.ErrorCode = (int)orderEx.ErrorCode;
                    break;

                case InvalidOperationException _:
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    errorResponse.Message = exception.Message;
                    break;

                case UnauthorizedAccessException _:
                    response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    errorResponse.Message = "Bạn không có quyền thực hiện thao tác này";
                    break;

                default:
                    response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    errorResponse.Message = "Đã xảy ra lỗi trong quá trình xử lý yêu cầu";
                    break;
            }

            var jsonResponse = JsonSerializer.Serialize(errorResponse);
            await response.WriteAsync(jsonResponse);
        }

        private int GetStatusCodeFromOrderErrorCode(OrderErrorCode errorCode)
        {
            return errorCode switch
            {
                OrderErrorCode.OrderNotFound => (int)HttpStatusCode.NotFound,
                OrderErrorCode.EmptyCart => (int)HttpStatusCode.BadRequest,
                OrderErrorCode.InsufficientInventory => (int)HttpStatusCode.BadRequest,
                OrderErrorCode.CannotCancelOrder => (int)HttpStatusCode.BadRequest,
                OrderErrorCode.PaymentError => (int)HttpStatusCode.BadRequest,
                OrderErrorCode.DatabaseError => (int)HttpStatusCode.InternalServerError,
                OrderErrorCode.EmailError => (int)HttpStatusCode.InternalServerError,
                _ => (int)HttpStatusCode.InternalServerError
            };
        }
    }

    /// <summary>
    /// Lớp đại diện cho phản hồi lỗi
    /// </summary>
    public class ErrorResponse
    {
        /// <summary>
        /// Trạng thái thành công
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Thông báo lỗi
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Mã lỗi (nếu có)
        /// </summary>
        public int? ErrorCode { get; set; }
    }
}