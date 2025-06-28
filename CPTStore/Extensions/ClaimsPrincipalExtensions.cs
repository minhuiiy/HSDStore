using System.Security.Claims;

namespace CPTStore.Extensions
{
    /// <summary>
    /// Các phương thức mở rộng cho ClaimsPrincipal
    /// </summary>
    public static class ClaimsPrincipalExtensions
    {
        /// <summary>
        /// Lấy ID người dùng từ ClaimsPrincipal
        /// </summary>
        /// <param name="claimsPrincipal">ClaimsPrincipal</param>
        /// <returns>ID người dùng hoặc chuỗi rỗng nếu không tìm thấy</returns>
        public static string GetUserId(this ClaimsPrincipal claimsPrincipal)
        {
            if (claimsPrincipal.Identity?.IsAuthenticated == true)
            {
                string? userId = claimsPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userId))
                {
                    return userId;
                }
            }
            
            return string.Empty;
        }
    }
}