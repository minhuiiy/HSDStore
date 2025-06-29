using AutoMapper;
using CPTStore.Data;
using CPTStore.Models;
using CPTStore.Repositories.Interfaces;
using CPTStore.Services.Interfaces;
using CPTStore.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CPTStore.Services
{
    /// <summary>
    /// Triển khai các phương thức xử lý giỏ hàng
    /// </summary>
    public class CartService : ICartService
    {
        public CartService(
            ApplicationDbContext context, 
            IDiscountService discountService,
            ICartItemRepository cartItemRepository,
            IMapper mapper)
        {
            _context = context;
            _discountService = discountService;
            _cartItemRepository = cartItemRepository;
            _mapper = mapper;
        }

        private readonly ApplicationDbContext _context;
        private readonly IDiscountService _discountService;
        private readonly ICartItemRepository _cartItemRepository;
        private readonly IMapper _mapper;

        /// <summary>
        /// Lấy thông tin giỏ hàng của người dùng
        /// </summary>
        /// <param name="userId">ID của người dùng</param>
        /// <returns>Đối tượng Cart</returns>
        public async Task<Cart> GetCartAsync(string userId)
        {
            var cartItems = await _cartItemRepository.GetCartItemsByUserIdAsync(userId);
            var cart = new Cart
            {
                UserId = userId,
                CartItems = cartItems
            };
            
            // Tính phí vận chuyển (có thể thay đổi theo logic nghiệp vụ)
            cart.ShippingFee = cart.TotalAmount > 500000 ? 0 : 30000;
            
            // Lấy thông tin giảm giá nếu có
            var cartDiscount = await _context.CartDiscounts.FirstOrDefaultAsync(cd => cd.UserId == userId);
            if (cartDiscount != null)
            {
                cart.Discount = cartDiscount.DiscountAmount;
            }
            
            return cart;
        }
        
        /// <summary>
        /// Lấy tất cả các CartItem của một người dùng
        /// </summary>
        /// <param name="userId">ID của người dùng</param>
        /// <returns>Danh sách CartItemViewModel</returns>
        public async Task<List<CartItemViewModel>> GetCartItemsAsync(string userId)
        {
            var cartItems = await _cartItemRepository.GetCartItemsByUserIdAsync(userId);
            return _mapper.Map<List<CartItemViewModel>>(cartItems);
        }

        /// <summary>
        /// Lấy một CartItem theo ID
        /// </summary>
        /// <param name="id">ID của CartItem</param>
        /// <returns>CartItemViewModel nếu tìm thấy, null nếu không tìm thấy</returns>
        public async Task<CartItemViewModel?> GetCartItemAsync(int id)
        {
            var cartItem = await _cartItemRepository.GetCartItemByIdAsync(id);
            return cartItem != null ? _mapper.Map<CartItemViewModel>(cartItem) : null;
        }

        /// <summary>
        /// Lấy một CartItem theo ProductId và UserId
        /// </summary>
        /// <param name="userId">ID của người dùng</param>
        /// <param name="productId">ID của sản phẩm</param>
        /// <returns>CartItemViewModel nếu tìm thấy, null nếu không tìm thấy</returns>
        public async Task<CartItemViewModel?> GetCartItemAsync(string userId, int productId)
        {
            var cartItem = await _cartItemRepository.GetCartItemByProductIdAndUserIdAsync(productId, userId);
            return cartItem != null ? _mapper.Map<CartItemViewModel>(cartItem) : null;
        }

        /// <summary>
        /// Thêm sản phẩm vào giỏ hàng
        /// </summary>
        /// <param name="userId">ID của người dùng</param>
        /// <param name="productId">ID của sản phẩm</param>
        /// <param name="quantity">Số lượng sản phẩm</param>
        /// <returns>ID của CartItem nếu thành công, -1 nếu thất bại</returns>
        public async Task<int> AddToCartAsync(string userId, int productId, int quantity)
        {
            try
            {
                // Kiểm tra tham số đầu vào
                if (string.IsNullOrEmpty(userId))
                {
                    throw new ArgumentException("UserId không thể null hoặc rỗng", nameof(userId));
                }
                
                if (productId <= 0)
                {
                    throw new ArgumentException("ProductId phải lớn hơn 0", nameof(productId));
                }
                
                if (quantity <= 0)
                {
                    throw new ArgumentException("Quantity phải lớn hơn 0", nameof(quantity));
                }
                
                var existingItem = await _cartItemRepository.GetCartItemByProductIdAndUserIdAsync(productId, userId);

                if (existingItem != null)
                {
                    // Kiểm tra số lượng tối đa cho phép
                    if (existingItem.Quantity + quantity > 100) // Giả sử giới hạn là 100
                    {
                        throw new InvalidOperationException("Số lượng sản phẩm vượt quá giới hạn cho phép");
                    }
                    
                    existingItem.Quantity += quantity;
                    existingItem.UpdatedAt = DateTime.Now;
                    await _cartItemRepository.UpdateCartItemAsync(existingItem);
                    return existingItem.Id;
                }
                else
                {
                    var product = await _context.Products.FindAsync(productId);
                    if (product == null)
                    {
                        throw new ArgumentException("Sản phẩm không tồn tại", nameof(productId));
                    }
                    
                    // Kiểm tra sản phẩm có sẵn để bán không
                    if (!product.IsAvailable)
                    {
                        Console.WriteLine($"Sản phẩm {product.Name} (ID: {product.Id}) không có sẵn để bán (IsAvailable = false)");
                        throw new InvalidOperationException("Sản phẩm hiện không có sẵn để bán");
                    }
                    
                    // Kiểm tra lại tồn kho một lần nữa trước khi thêm vào giỏ hàng
                    var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.ProductId == productId);
                    if (inventory is null || inventory.Quantity < quantity)
                    {
                        Console.WriteLine($"Sản phẩm {product.Name} (ID: {product.Id}) không đủ số lượng trong kho. Yêu cầu: {quantity}, Tồn kho: {inventory?.Quantity ?? 0}");
                        throw new InvalidOperationException("Sản phẩm không đủ số lượng trong kho");
                    }

                    var newItem = new CartItem
                    {
                        UserId = userId,
                        ProductId = productId,
                        Quantity = quantity,
                        UnitPrice = product.Price,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };

                    await _cartItemRepository.AddCartItemAsync(newItem);
                    return newItem.Id;
                }
            }
            catch (ArgumentException ex)
            {
                // Ghi log lỗi tham số
                Console.WriteLine($"Lỗi tham số khi thêm vào giỏ hàng: {ex.Message}");
                return -1;
            }
            catch (InvalidOperationException ex)
            {
                // Ghi log lỗi nghiệp vụ
                Console.WriteLine($"Lỗi nghiệp vụ khi thêm vào giỏ hàng: {ex.Message}");
                return -1;
            }
            catch (DbUpdateException ex)
            {
                // Ghi log lỗi cập nhật database
                Console.WriteLine($"Lỗi cập nhật database khi thêm vào giỏ hàng: {ex.Message}");
                return -1;
            }
            catch (Exception ex)
            {
                // Ghi log lỗi chung
                Console.WriteLine($"Lỗi không xác định khi thêm vào giỏ hàng: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// Cập nhật số lượng sản phẩm trong giỏ hàng
        /// </summary>
        /// <param name="id">ID của CartItem</param>
        /// <param name="quantity">Số lượng mới</param>
        /// <returns>Task</returns>
        public async Task UpdateCartItemAsync(int id, int quantity)
        {
            try
            {
                if (id <= 0)
                {
                    throw new ArgumentException("ID phải lớn hơn 0", nameof(id));
                }
                
                if (quantity <= 0)
                {
                    throw new ArgumentException("Quantity phải lớn hơn 0", nameof(quantity));
                }
                
                var cartItem = await _cartItemRepository.GetCartItemByIdAsync(id);
                if (cartItem == null)
                {
                    throw new ArgumentException("Sản phẩm không tồn tại trong giỏ hàng", nameof(id));
                }
                
                // Kiểm tra số lượng tối đa cho phép
                if (quantity > 100) // Giả sử giới hạn là 100
                {
                    throw new InvalidOperationException("Số lượng sản phẩm vượt quá giới hạn cho phép");
                }

                // Kiểm tra sản phẩm có sẵn để bán không
                var product = await _context.Products.FindAsync(cartItem.ProductId);
                if (product is not null && !product.IsAvailable)
                {
                    throw new InvalidOperationException("Sản phẩm hiện không có sẵn để bán");
                }

                cartItem.Quantity = quantity;
                cartItem.UpdatedAt = DateTime.Now;
                await _cartItemRepository.UpdateCartItemAsync(cartItem);
            }
            catch (Exception ex)
            {
                // Ghi log lỗi
                Console.WriteLine($"Lỗi khi cập nhật CartItem: {ex.Message}");
                throw; // Ném lại ngoại lệ để xử lý ở tầng trên
            }
        }

        /// <summary>
        /// Xóa sản phẩm khỏi giỏ hàng
        /// </summary>
        /// <param name="id">ID của CartItem</param>
        /// <returns>Task</returns>
        public async Task RemoveFromCartAsync(int id)
        {
            var cartItem = await _cartItemRepository.GetCartItemByIdAsync(id);
            if (cartItem != null)
            {
                await _cartItemRepository.DeleteCartItemAsync(cartItem);
            }
        }

        /// <summary>
        /// Xóa tất cả sản phẩm trong giỏ hàng
        /// </summary>
        /// <param name="userId">ID của người dùng</param>
        /// <returns>Task</returns>
        public async Task ClearCartAsync(string userId)
        {
            await _cartItemRepository.ClearCartAsync(userId);
        }

        /// <summary>
        /// Tính tổng số lượng sản phẩm trong giỏ hàng
        /// </summary>
        /// <param name="userId">ID của người dùng</param>
        /// <returns>Tổng số lượng</returns>
        public async Task<int> GetCartItemsCountAsync(string userId)
        {
            var cartItems = await _cartItemRepository.GetCartItemsByUserIdAsync(userId);
            return cartItems.Sum(c => c.Quantity);
        }

        /// <summary>
        /// Tính tổng tiền giỏ hàng
        /// </summary>
        /// <param name="userId">ID của người dùng</param>
        /// <returns>Tổng tiền</returns>
        public async Task<decimal> GetCartTotalAsync(string userId)
        {
            return await _cartItemRepository.GetCartTotalAsync(userId);
        }

        public async Task<decimal> ApplyDiscountAsync(string userId, string discountCode)
        {
            var discount = await _discountService.GetDiscountByCodeAsync(discountCode);
            if (discount == null)
            {
                throw new ArgumentException("Mã giảm giá không hợp lệ");
            }

            var cartTotal = await GetCartTotalAsync(userId);
            if (!await _discountService.IsDiscountValidAsync(discountCode, cartTotal))
            {
                throw new ArgumentException("Mã giảm giá không áp dụng được cho đơn hàng này");
            }

            var discountAmount = await _discountService.CalculateDiscountAmountAsync(discountCode, cartTotal);
            
            // Lưu mã giảm giá vào session hoặc database
            // Ở đây giả định có một bảng lưu thông tin giảm giá tạm thời cho giỏ hàng
            var cartDiscount = await _context.CartDiscounts.FirstOrDefaultAsync(cd => cd.UserId == userId);
            if (cartDiscount is null)
            {
                _context.CartDiscounts.Add(new CartDiscount
                {
                    UserId = userId,
                    DiscountCode = discountCode,
                    DiscountAmount = discountAmount
                });
            }
            else
            {
                cartDiscount.DiscountCode = discountCode;
                cartDiscount.DiscountAmount = discountAmount;
                _context.Entry(cartDiscount).State = EntityState.Modified;
            }

            await _context.SaveChangesAsync();
            return discountAmount;
        }

        public async Task RemoveDiscountAsync(string userId)
        {
            var cartDiscount = await _context.CartDiscounts.FirstOrDefaultAsync(cd => cd.UserId == userId);
            if (cartDiscount != null)
            {
                _context.CartDiscounts.Remove(cartDiscount);
                await _context.SaveChangesAsync();
            }
        }
    }
}