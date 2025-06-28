using System.Collections.Generic;
using System.Linq;

namespace CPTStore.Extensions
{
    public static class EnumerableExtensions
    {
        /// <summary>
        /// Kiểm tra xem một collection có null hoặc rỗng không
        /// </summary>
        /// <typeparam name="T">Kiểu của các phần tử trong collection</typeparam>
        /// <param name="source">Collection cần kiểm tra</param>
        /// <returns>True nếu collection là null hoặc rỗng, ngược lại là False</returns>
        public static bool IsNullOrEmpty<T>(this IEnumerable<T> source)
        {
            return source == null || !source.Any();
        }
    }
}