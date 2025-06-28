using Microsoft.AspNetCore.Http;
using System;
using System.Security.Claims;

namespace CPTStore.Extensions
{
    /// <summary>
    /// Các phương thức mở rộng cho HttpContext.Session
    /// </summary>
    public static class SessionExtensions
    {
        private const string SessionIdKey = "CPTStore.SessionId";

        /// <summary>
        /// Lấy ID phiên làm việc của người dùng
        /// </summary>
        /// <param name="httpContext">HttpContext</param>
        /// <returns>ID phiên làm việc</returns>
        public static string GetSessionId(this HttpContext httpContext)
        {
            // Kiểm tra xem đã có ID phiên làm việc được lưu trữ chưa
            if (httpContext.Session.GetString(SessionIdKey) == null)
            {
                // Nếu chưa có, tạo một ID mới và lưu trữ
                string sessionId = Guid.NewGuid().ToString();
                httpContext.Session.SetString(SessionIdKey, sessionId);
                return sessionId;
            }

            // Nếu đã có, trả về ID đã lưu trữ
            return httpContext.Session.GetString(SessionIdKey) ?? string.Empty;
        }

        /// <summary>
        /// Lấy ID người dùng hoặc ID phiên làm việc
        /// </summary>
        /// <param name="httpContext">HttpContext</param>
        /// <param name="userId">ID người dùng (nếu đã đăng nhập)</param>
        /// <returns>ID người dùng hoặc ID phiên làm việc</returns>
        public static string GetUserIdOrSessionId(this HttpContext httpContext, string? userId = null)
        {
            // Nếu người dùng đã đăng nhập, trả về ID người dùng
            if (!string.IsNullOrEmpty(userId))
            {
                return userId;
            }

            // Nếu người dùng chưa đăng nhập, trả về ID phiên làm việc
            return httpContext.GetSessionId();
        }

        /// <summary>
        /// Lấy ID người dùng hoặc ID phiên làm việc
        /// </summary>
        /// <param name="session">ISession</param>
        /// <param name="claimsPrincipal">ClaimsPrincipal</param>
        /// <returns>ID người dùng hoặc ID phiên làm việc</returns>
        public static string GetUserIdOrSessionId(this ISession session, ClaimsPrincipal claimsPrincipal)
        {
            // Nếu người dùng đã đăng nhập, trả về ID người dùng
            if (claimsPrincipal.Identity?.IsAuthenticated == true)
            {
                string? userId = claimsPrincipal.FindFirst("sub")?.Value;
                if (!string.IsNullOrEmpty(userId))
                {
                    return userId;
                }
            }

            // Nếu người dùng chưa đăng nhập, trả về ID phiên làm việc
            // Kiểm tra xem đã có ID phiên làm việc được lưu trữ chưa
            string? sessionId = session.GetString(SessionIdKey);
            if (string.IsNullOrEmpty(sessionId))
            {
                // Nếu chưa có, tạo một ID mới và lưu trữ
                sessionId = Guid.NewGuid().ToString();
                session.SetString(SessionIdKey, sessionId);
            }
            
            return sessionId;
        }
    }
}