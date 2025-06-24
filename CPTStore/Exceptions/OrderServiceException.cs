using System;

namespace CPTStore.Exceptions
{
    /// <summary>
    /// Lớp ngoại lệ tùy chỉnh cho các lỗi liên quan đến OrderService
    /// </summary>
    public class OrderServiceException : Exception
    {
        /// <summary>
        /// Mã lỗi
        /// </summary>
        public OrderErrorCode ErrorCode { get; }

        /// <summary>
        /// Constructor với thông báo lỗi
        /// </summary>
        /// <param name="message">Thông báo lỗi</param>
        public OrderServiceException(string message) : base(message)
        {
            ErrorCode = OrderErrorCode.GeneralError;
        }

        /// <summary>
        /// Constructor với thông báo lỗi và mã lỗi
        /// </summary>
        /// <param name="message">Thông báo lỗi</param>
        /// <param name="errorCode">Mã lỗi</param>
        public OrderServiceException(string message, OrderErrorCode errorCode) : base(message)
        {
            ErrorCode = errorCode;
        }

        /// <summary>
        /// Constructor với thông báo lỗi, mã lỗi và ngoại lệ gốc
        /// </summary>
        /// <param name="message">Thông báo lỗi</param>
        /// <param name="errorCode">Mã lỗi</param>
        /// <param name="innerException">Ngoại lệ gốc</param>
        public OrderServiceException(string message, OrderErrorCode errorCode, Exception innerException) : base(message, innerException)
        {
            ErrorCode = errorCode;
        }
    }

    /// <summary>
    /// Enum định nghĩa các mã lỗi cho OrderService
    /// </summary>
    public enum OrderErrorCode
    {
        /// <summary>
        /// Lỗi chung
        /// </summary>
        GeneralError = 0,

        /// <summary>
        /// Không tìm thấy đơn hàng
        /// </summary>
        OrderNotFound = 1,

        /// <summary>
        /// Giỏ hàng trống
        /// </summary>
        EmptyCart = 2,

        /// <summary>
        /// Sản phẩm không đủ số lượng trong kho
        /// </summary>
        InsufficientInventory = 3,

        /// <summary>
        /// Không thể hủy đơn hàng
        /// </summary>
        CannotCancelOrder = 4,

        /// <summary>
        /// Lỗi thanh toán
        /// </summary>
        PaymentError = 5,

        /// <summary>
        /// Lỗi cơ sở dữ liệu
        /// </summary>
        DatabaseError = 6,

        /// <summary>
        /// Lỗi gửi email
        /// </summary>
        EmailError = 7
    }
}