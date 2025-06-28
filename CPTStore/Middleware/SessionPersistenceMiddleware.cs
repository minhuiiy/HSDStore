using Microsoft.AspNetCore.Http;
using CPTStore.Extensions;

namespace CPTStore.Middleware
{
    /// <summary>
    /// Middleware đảm bảo session ID được tạo và lưu trữ ngay khi session được tạo
    /// </summary>
    public class SessionPersistenceMiddleware
    {
        private readonly RequestDelegate _next;

        public SessionPersistenceMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Đảm bảo session ID được tạo và lưu trữ ngay lập tức
            // Điều này giúp giải quyết vấn đề session không được lưu trữ giữa các request
            // khi không có giá trị nào được đặt vào session
            context.GetSessionId();
            
            await _next(context);
        }
    }

    /// <summary>
    /// Extension methods để đăng ký middleware
    /// </summary>
    public static class SessionPersistenceMiddlewareExtensions
    {
        public static IApplicationBuilder UseSessionPersistence(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<SessionPersistenceMiddleware>();
        }
    }
}