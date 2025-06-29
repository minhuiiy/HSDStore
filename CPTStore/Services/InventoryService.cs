using CPTStore.Data;
using CPTStore.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace CPTStore.Services
{
    public class InventoryService(ApplicationDbContext context, IEmailService emailService) : IInventoryService
    {
        private readonly ApplicationDbContext _context = context;
        private readonly IEmailService _emailService = emailService;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        
        #region Error Handling Helper Methods
        
        /// <summary>
        /// Xử lý lỗi DbUpdateException một cách thống nhất
        /// </summary>
        /// <param name="dbEx">Lỗi DbUpdateException</param>
        /// <param name="transaction">Giao dịch cần rollback</param>
        /// <param name="errorMessage">Thông báo lỗi</param>
        /// <param name="attemptSync">Có thử đồng bộ hóa tồn kho không</param>
        /// <returns>Task</returns>
        private async Task HandleDbUpdateExceptionAsync(DbUpdateException dbEx, IDbContextTransaction? transaction, string errorMessage, bool attemptSync = true)
        {
            // Hủy giao dịch nếu có lỗi cơ sở dữ liệu và transaction không null
            if (transaction != null)
            {
                await transaction.RollbackAsync();
            }
            
            // Ghi log lỗi
            Console.WriteLine(errorMessage);
            Console.WriteLine($"Chi tiết lỗi: {dbEx.Message}");
            if (dbEx.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {dbEx.InnerException.Message}");
            }
            Console.WriteLine($"Stack Trace: {dbEx.StackTrace}");
            
            // Thử đồng bộ hóa lại tồn kho nếu cần
            if (attemptSync)
            {
                Console.WriteLine("Đang thử đồng bộ hóa lại tồn kho...");
                await SynchronizeProductStockAsync();
            }
        }
        
        /// <summary>
        /// Xử lý lỗi Exception một cách thống nhất
        /// </summary>
        /// <param name="ex">Lỗi Exception</param>
        /// <param name="transaction">Giao dịch cần rollback</param>
        /// <param name="errorMessage">Thông báo lỗi</param>
        /// <returns>Task</returns>
        private async Task HandleExceptionAsync(Exception ex, IDbContextTransaction? transaction, string errorMessage)
        {
            // Hủy giao dịch nếu có lỗi và transaction không null
            if (transaction != null)
            {
                await transaction.RollbackAsync();
            }
            
            // Ghi log lỗi
            Console.WriteLine(errorMessage);
            Console.WriteLine($"Chi tiết lỗi: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
        }
        
        #endregion
    
    /// <summary>
    /// Phương thức tạo inventory từ product
    /// </summary>
    /// <param name="product">Sản phẩm cần tạo inventory</param>
    /// <returns>Inventory đã tạo hoặc null nếu có lỗi</returns>
    private async Task<Inventory?> CreateInventoryFromProductAsync(Product product)
    {
        try
        {
            // Kiểm tra xem đã có giao dịch hiện tại chưa
            bool hasExistingTransaction = _context.Database.CurrentTransaction != null;
            IDbContextTransaction? transaction = null;
            
            if (!hasExistingTransaction)
            {
                // Chỉ tạo giao dịch mới nếu chưa có giao dịch hiện tại
                transaction = await _context.Database.BeginTransactionAsync();
            }
            try
            {
                // Kiểm tra xem inventory đã tồn tại chưa (để tránh tạo trùng lặp)
                var existingInventory = await _context.Inventories
                    .FirstOrDefaultAsync(i => i.ProductId == product.Id);
                    
                if (existingInventory != null)
                {
                    Console.WriteLine($"Inventory đã tồn tại cho sản phẩm ID: {product.Id}, Tên: {product.Name}, sử dụng inventory hiện có");
                    
                    // Kiểm tra nếu có sự không nhất quán giữa Product.Stock và Inventory.Quantity
                    if (product.Stock != existingInventory.Quantity)
                    {
                        Console.WriteLine($"Phát hiện không nhất quán cho sản phẩm ID: {product.Id}, Tên: {product.Name ?? "không xác định"}");
                        Console.WriteLine($"Product.Stock: {product.Stock}, Inventory.Quantity: {existingInventory.Quantity}");
                        
                        // Sử dụng giá trị lớn hơn để đảm bảo không bị lỗi hết hàng
                        int newStock = Math.Max(product.Stock, existingInventory.Quantity);
                        
                        // Cập nhật cả hai giá trị
                        product.Stock = newStock;
                        existingInventory.Quantity = newStock;
                        existingInventory.UpdatedAt = DateTime.UtcNow;
                        
                        await _context.SaveChangesAsync();
                        Console.WriteLine($"Đã sửa thành: Product.Stock = {product.Stock}, Inventory.Quantity = {existingInventory.Quantity}");
                    }
                    
                    return existingInventory;
                }
                
                // Tạo mới inventory với các giá trị mặc định phù hợp
                var inventory = new Inventory
                {
                    ProductId = product.Id,
                    Quantity = product.Stock,
                    MinimumStockLevel = 5,
                    MaximumStockLevel = 100,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    LastRestockDate = DateTime.UtcNow
                };
                
                _context.Inventories.Add(inventory);
                await _context.SaveChangesAsync();
                
                // Hoàn tất giao dịch
                if (transaction != null)
                {
                    if (transaction != null)
                        if (transaction != null)
                        await transaction.CommitAsync();
                }
                Console.WriteLine($"Đã tạo mới inventory cho sản phẩm ID: {product.Id}, Tên: {product.Name ?? "không xác định"}, Số lượng: {product.Stock}");
                
                return inventory;
            }
            catch (DbUpdateException dbEx)
            {
                string errorMessage = $"Lỗi cơ sở dữ liệu khi tạo inventory từ sản phẩm ID: {product.Id}, Tên: {product.Name ?? "không xác định"}";
                await HandleDbUpdateExceptionAsync(dbEx, transaction, errorMessage, false);
                return null;
            }
            catch (Exception ex)
            {
                string errorMessage = $"Lỗi khi tạo inventory từ sản phẩm ID: {product.Id}, Tên: {product.Name ?? "không xác định"}";
                await HandleExceptionAsync(ex, transaction, errorMessage);
                return null;
            }
        }
        catch (Exception ex)
        {
            string errorMessage = $"Lỗi ngoại lệ khi tạo inventory từ sản phẩm ID: {product.Id}, Tên: {product.Name ?? "không xác định"}";
            await HandleExceptionAsync(ex, null, errorMessage);
            return null;
        }
    }

    /// <summary>
    /// Gửi thông báo cho các sản phẩm có tồn kho thấp
    /// </summary>
    /// <returns>Task hoàn thành</returns>
    public async Task SendLowStockNotificationsAsync()
    {
        int successCount = 0;
        int failureCount = 0;
        
        try
        {
            var lowStockItems = await _context.Inventories
                .Where(i => i.Quantity <= i.LowStockThreshold)
                .ToListAsync();
                
            if (!lowStockItems.Any())
            {
                Console.WriteLine("Không có mặt hàng nào cần gửi thông báo tồn kho thấp.");
                return;
            }
            
            foreach (var item in lowStockItems)
            {
                try
                {
                    // Gửi email thông báo
                    await _emailService.SendLowStockNotificationAsync(item.ProductId);
                    
                    // Cập nhật trạng thái đã gửi thông báo (nếu cần)
                    // Không thể sử dụng LowStockNotificationSent vì không có trong model
                    await _context.SaveChangesAsync();
                    successCount++;
                }
                catch (Exception ex)
                {
                    string errorMessage = $"Lỗi khi gửi thông báo cho sản phẩm {item.Product?.Name ?? "Unknown"}";
                    Console.WriteLine($"{errorMessage}: {ex.Message}");
                    failureCount++;
                }
            }
            
            Console.WriteLine($"Đã gửi thành công {successCount} thông báo, thất bại {failureCount} thông báo.");
        }
        catch (Exception ex)
        {
            string errorMessage = $"Lỗi khi gửi thông báo tồn kho thấp";
            await HandleExceptionAsync(ex, null, errorMessage);
        }
    }
    
    /// <summary>
    /// Đồng bộ hóa số lượng tồn kho giữa bảng Product và Inventory
    /// </summary>
    /// <returns>Số lượng mục đã đồng bộ hoặc -1 nếu có lỗi</returns>
    public async Task<int> SynchronizeProductStockAsync()
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            Console.WriteLine("Bắt đầu đồng bộ hóa tồn kho với bảng sản phẩm...");
            
            // Lấy tất cả sản phẩm và tồn kho
            var products = await _context.Products.ToListAsync();
            var inventories = await _context.Inventories.ToListAsync();
            
            int syncCount = 0;
            // Đồng bộ số lượng tồn kho từ bảng Inventory sang bảng Product
            foreach (var product in products)
            {
                var inventory = inventories.FirstOrDefault(i => i.ProductId == product.Id);
                
                if (inventory != null)
                {
                    // Cập nhật số lượng tồn kho trong bảng Product
                    if (product.Stock != inventory.Quantity)
                    {
                        product.Stock = inventory.Quantity;
                        syncCount++;
                    }
                }
                else
                {
                    // Tạo mới inventory nếu chưa có
                    var newInventory = new Inventory
                    {
                        ProductId = product.Id,
                        Quantity = product.Stock,
                        MinimumStockLevel = 10,
                        MaximumStockLevel = 100,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        LastRestockDate = DateTime.UtcNow
                    };
                    
                    await _context.Inventories.AddAsync(newInventory);
                    syncCount++;
                }
            }
            
            // Lưu thay đổi
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            
            Console.WriteLine($"Đồng bộ hóa tồn kho thành công! Đã đồng bộ {syncCount} mục.");
            return syncCount;
        }
        catch (DbUpdateException dbEx)
        {
            string errorMessage = $"Lỗi cập nhật cơ sở dữ liệu khi đồng bộ hóa tồn kho";
            await HandleDbUpdateExceptionAsync(dbEx, transaction, errorMessage, false);
            return -1;
        }
        catch (Exception ex)
        {
            string errorMessage = $"Lỗi khi đồng bộ hóa tồn kho";
            await HandleExceptionAsync(ex, transaction, errorMessage);
            return -1;
        }
    }
    
    /// <summary>
    /// Kiểm tra và sửa lỗi đồng bộ hóa tồn kho giữa bảng Product và Inventory
    /// </summary>
    /// <returns>Số lượng mục đã sửa hoặc -1 nếu có lỗi</returns>
    public async Task<int> FixInventoryStockSynchronizationAsync()
    {
        // Sử dụng semaphore để đảm bảo chỉ có một tiến trình cập nhật tồn kho cùng lúc
        await _semaphore.WaitAsync();
        
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            Console.WriteLine("Bắt đầu kiểm tra và sửa lỗi đồng bộ hóa tồn kho...");
            
            // Lấy tất cả sản phẩm và tồn kho với dữ liệu mới nhất
            var products = await _context.Products.ToListAsync();
            var inventories = await _context.Inventories.ToListAsync();
            
            int fixCount = 0;
            
            // Kiểm tra và sửa lỗi đồng bộ hóa tồn kho
            foreach (var inventory in inventories)
            {
                var product = products.FirstOrDefault(p => p.Id == inventory.ProductId);
                
                if (product != null)
                {
                    // Kiểm tra nếu có sự không nhất quán giữa Product.Stock và Inventory.Quantity
                    if (product.Stock != inventory.Quantity)
                    {
                        Console.WriteLine($"Phát hiện không nhất quán cho sản phẩm ID: {product.Id}, Tên: {product.Name}");
                        Console.WriteLine($"Product.Stock: {product.Stock}, Inventory.Quantity: {inventory.Quantity}");
                        
                        // Sử dụng giá trị lớn hơn để đảm bảo không bị lỗi hết hàng
                        int newStock = Math.Max(product.Stock, inventory.Quantity);
                        
                        // Cập nhật cả hai giá trị
                        product.Stock = newStock;
                        inventory.Quantity = newStock;
                        inventory.UpdatedAt = DateTime.UtcNow;
                        
                        Console.WriteLine($"Đã sửa thành: Product.Stock = {product.Stock}, Inventory.Quantity = {inventory.Quantity}");
                        fixCount++;
                    }
                }
            }
            
            // Kiểm tra các sản phẩm không có trong bảng Inventory
            foreach (var product in products)
            {
                var inventory = inventories.FirstOrDefault(i => i.ProductId == product.Id);
                
                if (inventory == null)
                {
                    Console.WriteLine($"Sản phẩm ID: {product.Id}, Tên: {product.Name} không có trong bảng Inventory");
                    
                    // Tạo mới inventory
                    var newInventory = new Inventory
                    {
                        ProductId = product.Id,
                        Quantity = product.Stock,
                        MinimumStockLevel = 10,
                        MaximumStockLevel = 100,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        LastRestockDate = DateTime.UtcNow
                    };
                    
                    await _context.Inventories.AddAsync(newInventory);
                    Console.WriteLine($"Đã tạo mới inventory với số lượng: {newInventory.Quantity}");
                    fixCount++;
                }
            }
            
            // Lưu thay đổi
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            
            Console.WriteLine($"Đã sửa lỗi đồng bộ hóa tồn kho thành công! Đã sửa {fixCount} mục.");
            return fixCount;
        }
        catch (DbUpdateException dbEx)
        {
            string errorMessage = $"Lỗi cơ sở dữ liệu khi sửa lỗi đồng bộ hóa tồn kho";
            await HandleDbUpdateExceptionAsync(dbEx, transaction, errorMessage, false);
            return -1;
        }
        catch (Exception ex)
        {
            string errorMessage = $"Lỗi khi sửa lỗi đồng bộ hóa tồn kho";
            await HandleExceptionAsync(ex, transaction, errorMessage);
            return -1;
        }
        finally
        {
            // Giải phóng semaphore trong mọi trường hợp
            _semaphore.Release();
        }
    }
    
    /// <summary>
    /// Giảm số lượng tồn kho của sản phẩm
    /// </summary>
    /// <param name="productId">ID của sản phẩm</param>
    /// <param name="quantity">Số lượng cần giảm</param>
    /// <returns>True nếu giảm thành công, False nếu có lỗi</returns>
    public async Task<bool> DeductStockAsync(int productId, int quantity)
    {
        // Sử dụng semaphore để đảm bảo chỉ có một tiến trình cập nhật tồn kho cùng lúc
        await _semaphore.WaitAsync();
        
        try
        {
            // Kiểm tra xem đã có giao dịch hiện tại chưa
            bool hasExistingTransaction = _context.Database.CurrentTransaction != null;
            IDbContextTransaction? transaction = null;
            
            if (!hasExistingTransaction)
            {
                // Chỉ tạo giao dịch mới nếu chưa có giao dịch hiện tại
                transaction = await _context.Database.BeginTransactionAsync();
            }
            else
            {
                // Sử dụng giao dịch hiện tại
                transaction = _context.Database.CurrentTransaction;
            }
            try
            {
                // Kiểm tra sản phẩm tồn tại
                var product = await _context.Products.FindAsync(productId);
                if (product == null)
                {
                    Console.WriteLine($"Không tìm thấy sản phẩm ID: {productId}, không thể giảm tồn kho");
                    if (transaction != null)
                    {
                        await transaction.RollbackAsync();
                    }
                    return false;
                }
                
                // Kiểm tra và lấy inventory
                var inventory = await _context.Inventories
                    .FirstOrDefaultAsync(i => i.ProductId == productId);
                    
                if (inventory == null)
                {
                    // Tạo mới inventory nếu chưa có
                    inventory = await CreateInventoryFromProductAsync(product);
                    if (inventory == null)
                    {
                        Console.WriteLine($"Không thể tạo mới inventory cho sản phẩm ID: {productId}");
                        if (transaction != null)
                        {
                            if (transaction != null)
                                if (transaction != null)
                                await transaction.RollbackAsync();
                        }
                        return false;
                    }
                }
                
                // Kiểm tra nếu có sự không nhất quán giữa Product.Stock và Inventory.Quantity
                if (product.Stock != inventory.Quantity)
                {
                    Console.WriteLine($"Phát hiện không nhất quán cho sản phẩm ID: {product.Id}, Tên: {product.Name}");
                    Console.WriteLine($"Product.Stock: {product.Stock}, Inventory.Quantity: {inventory.Quantity}");
                    
                    // Sử dụng giá trị lớn hơn để đảm bảo không bị lỗi hết hàng
                    int newStock = Math.Max(product.Stock, inventory.Quantity);
                    
                    // Cập nhật cả hai giá trị
                    product.Stock = newStock;
                    inventory.Quantity = newStock;
                    inventory.UpdatedAt = DateTime.UtcNow;
                    
                    Console.WriteLine($"Đã sửa thành: Product.Stock = {product.Stock}, Inventory.Quantity = {inventory.Quantity}");
                    await _context.SaveChangesAsync();
                }
                
                // Kiểm tra số lượng tồn kho đủ để giảm không
                if (inventory.Quantity < quantity)
                {
                    Console.WriteLine($"Số lượng tồn kho không đủ. Hiện có: {inventory.Quantity}, Cần giảm: {quantity}");
                    if (transaction != null)
                    {
                        await transaction.RollbackAsync();
                    }
                    return false;
                }
                
                // Giảm số lượng tồn kho
                inventory.Quantity -= quantity;
                inventory.UpdatedAt = DateTime.UtcNow;
                
                // Cập nhật số lượng trong bảng Product
                product.Stock = inventory.Quantity;
                
                // Lưu thay đổi
                await _context.SaveChangesAsync();
                
                // Hoàn tất giao dịch nếu chúng ta đã tạo giao dịch mới
                if (!hasExistingTransaction && transaction != null)
                {
                    try
                    {
                        await transaction.CommitAsync();
                        Console.WriteLine($"Đã hoàn tất giao dịch tăng tồn kho cho sản phẩm ID: {productId}");
                    }
                    catch (InvalidOperationException ex) when (ex.Message.Contains("has completed"))
                    {
                        // Giao dịch đã được commit, có thể do phương thức gọi đã commit
                        Console.WriteLine($"Giao dịch đã được commit trước đó, bỏ qua: {ex.Message}");
                    }
                }
                
                Console.WriteLine($"Đã giảm {quantity} đơn vị từ tồn kho sản phẩm ID: {productId}, Còn lại: {inventory.Quantity}");
                return true;
            }
            catch (DbUpdateException dbEx)
            {
                string errorMessage = $"Lỗi cơ sở dữ liệu khi giảm tồn kho sản phẩm ID: {productId}";
                await HandleDbUpdateExceptionAsync(dbEx, transaction, errorMessage);
                return false;
            }
            catch (Exception ex)
            {
                string errorMessage = $"Lỗi khi giảm tồn kho sản phẩm ID: {productId}";
                await HandleExceptionAsync(ex, transaction, errorMessage);
                return false;
            }
        }
        finally
        {
            // Giải phóng semaphore
            _semaphore.Release();
        }
    }
    
    /// <summary>
    /// Tăng số lượng tồn kho của sản phẩm
    /// </summary>
    /// <param name="productId">ID của sản phẩm</param>
    /// <param name="quantity">Số lượng cần tăng</param>
    /// <returns>True nếu tăng thành công, False nếu có lỗi</returns>
    public async Task<bool> RestockAsync(int productId, int quantity)
    {
        // Sử dụng semaphore để đảm bảo chỉ có một tiến trình cập nhật tồn kho cùng lúc
        await _semaphore.WaitAsync();
        
        try
        {
            // Kiểm tra xem đã có giao dịch hiện tại chưa
            bool hasExistingTransaction = _context.Database.CurrentTransaction != null;
            IDbContextTransaction? transaction = null;
            
            if (!hasExistingTransaction)
            {
                // Chỉ tạo giao dịch mới nếu chưa có giao dịch hiện tại
                transaction = await _context.Database.BeginTransactionAsync();
            }
            else
            {
                // Sử dụng giao dịch hiện tại
                transaction = _context.Database.CurrentTransaction;
            }
            try
            {
                // Kiểm tra sản phẩm tồn tại
                var product = await _context.Products.FindAsync(productId);
                if (product == null)
                {
                    Console.WriteLine($"Không tìm thấy sản phẩm ID: {productId}, không thể tăng tồn kho");
                    if (transaction != null)
                    {
                        await transaction.RollbackAsync();
                    }
                    return false;
                }
                
                // Kiểm tra và lấy inventory
                var inventory = await _context.Inventories
                    .FirstOrDefaultAsync(i => i.ProductId == productId);
                    
                if (inventory == null)
                {
                    // Tạo mới inventory nếu chưa có
                    inventory = await CreateInventoryFromProductAsync(product);
                    if (inventory == null)
                    {
                        Console.WriteLine($"Không thể tạo mới inventory cho sản phẩm ID: {productId}");
                        if (transaction != null)
                        {
                            await transaction.RollbackAsync();
                        }
                        return false;
                    }
                }
                
                // Kiểm tra nếu có sự không nhất quán giữa Product.Stock và Inventory.Quantity
                if (product.Stock != inventory.Quantity)
                {
                    Console.WriteLine($"Phát hiện không nhất quán cho sản phẩm ID: {product.Id}, Tên: {product.Name}");
                    Console.WriteLine($"Product.Stock: {product.Stock}, Inventory.Quantity: {inventory.Quantity}");
                    
                    // Sử dụng giá trị lớn hơn để đảm bảo không bị lỗi hết hàng
                    int newStock = Math.Max(product.Stock, inventory.Quantity);
                    
                    // Cập nhật cả hai giá trị
                    product.Stock = newStock;
                    inventory.Quantity = newStock;
                    inventory.UpdatedAt = DateTime.UtcNow;
                    
                    Console.WriteLine($"Đã sửa thành: Product.Stock = {product.Stock}, Inventory.Quantity = {inventory.Quantity}");
                    await _context.SaveChangesAsync();
                }
                
                // Tăng số lượng tồn kho
                inventory.Quantity += quantity;
                inventory.UpdatedAt = DateTime.UtcNow;
                inventory.LastRestockDate = DateTime.UtcNow;
                // Không thể sử dụng LowStockNotificationSent vì không có trong model
                
                // Cập nhật số lượng trong bảng Product
                product.Stock = inventory.Quantity;
                
                // Lưu thay đổi
                await _context.SaveChangesAsync();
                
                // Hoàn tất giao dịch nếu chúng ta đã tạo giao dịch mới
                if (!hasExistingTransaction && transaction != null)
                {
                    try
                    {
                        await transaction.CommitAsync();
                        Console.WriteLine($"Đã hoàn tất giao dịch tăng tồn kho cho sản phẩm ID: {productId}");
                    }
                    catch (InvalidOperationException ex) when (ex.Message.Contains("has completed"))
                    {
                        // Giao dịch đã được commit, có thể do phương thức gọi đã commit
                        Console.WriteLine($"Giao dịch đã được commit trước đó, bỏ qua: {ex.Message}");
                    }
                }
                
                Console.WriteLine($"Đã tăng {quantity} đơn vị vào tồn kho sản phẩm ID: {productId}, Hiện có: {inventory.Quantity}");
                return true;
            }
            catch (DbUpdateException dbEx)
            {
                Console.WriteLine($"Lỗi cơ sở dữ liệu khi tăng tồn kho sản phẩm ID: {productId}");
                Console.WriteLine($"Chi tiết lỗi: {dbEx.Message}");
                if (dbEx.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {dbEx.InnerException.Message}");
                }
                
                // Rollback giao dịch nếu chúng ta đã tạo giao dịch mới
                if (!hasExistingTransaction && transaction != null)
                {
                    try
                    {
                        await transaction.RollbackAsync();
                        Console.WriteLine($"Đã rollback giao dịch tăng tồn kho cho sản phẩm ID: {productId}");
                    }
                    catch (InvalidOperationException ioe) when (ioe.Message.Contains("has completed"))
                    {
                        // Giao dịch đã được commit hoặc rollback, có thể do phương thức gọi đã xử lý
                        Console.WriteLine($"Giao dịch đã được xử lý trước đó, bỏ qua rollback: {ioe.Message}");
                    }
                    catch (Exception rollbackEx)
                    {
                        Console.WriteLine($"Lỗi khi rollback giao dịch tăng tồn kho cho sản phẩm ID: {productId}");
                        Console.WriteLine($"Chi tiết lỗi: {rollbackEx.Message}");
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi tăng tồn kho sản phẩm ID: {productId}");
                Console.WriteLine($"Chi tiết lỗi: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                
                // Rollback giao dịch nếu chúng ta đã tạo giao dịch mới
                if (!hasExistingTransaction && transaction != null)
                {
                    try
                    {
                        await transaction.RollbackAsync();
                        Console.WriteLine($"Đã rollback giao dịch tăng tồn kho cho sản phẩm ID: {productId}");
                    }
                    catch (InvalidOperationException ioe) when (ioe.Message.Contains("has completed"))
                    {
                        // Giao dịch đã được commit hoặc rollback, có thể do phương thức gọi đã xử lý
                        Console.WriteLine($"Giao dịch đã được xử lý trước đó, bỏ qua rollback: {ioe.Message}");
                    }
                    catch (Exception rollbackEx)
                    {
                        Console.WriteLine($"Lỗi khi rollback giao dịch tăng tồn kho cho sản phẩm ID: {productId}");
                        Console.WriteLine($"Chi tiết lỗi: {rollbackEx.Message}");
                    }
                }
                return false;
            }
        }
        finally
        {
            // Giải phóng semaphore
            _semaphore.Release();
        }
    }
    
    /// <summary>
    /// Tạo mới bản ghi tồn kho cho sản phẩm
    /// </summary>
    /// <param name="inventory">Thông tin tồn kho cần tạo</param>
    /// <returns>True nếu tạo thành công, False nếu có lỗi</returns>
    public async Task<bool> CreateInventoryAsync(Inventory inventory)
    {
        try
        {
            // Sử dụng giao dịch để đảm bảo tính nhất quán
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Kiểm tra xem inventory đã tồn tại chưa
                var existingInventory = await _context.Inventories
                    .FirstOrDefaultAsync(i => i.ProductId == inventory.ProductId);
                    
                if (existingInventory != null)
                {
                    Console.WriteLine($"Inventory đã tồn tại cho sản phẩm ID: {inventory.ProductId}, không thể tạo mới");
                    await transaction.RollbackAsync();
                    return false;
                }
                
                // Kiểm tra sản phẩm tồn tại
                var product = await _context.Products.FindAsync(inventory.ProductId);
                if (product == null)
                {
                    Console.WriteLine($"Không tìm thấy sản phẩm ID: {inventory.ProductId}, không thể tạo inventory");
                    await transaction.RollbackAsync();
                    return false;
                }
                
                // Thiết lập các giá trị mặc định nếu chưa có
                inventory.CreatedAt = DateTime.UtcNow;
                inventory.UpdatedAt = DateTime.UtcNow;
                if (inventory.LastRestockDate == default)
                {
                    inventory.LastRestockDate = DateTime.UtcNow;
                }
                
                // Thêm inventory mới
                _context.Inventories.Add(inventory);
                await _context.SaveChangesAsync();
                
                // Đồng bộ với bảng Product
                product.Stock = inventory.Quantity;
                await _context.SaveChangesAsync();
                
                // Hoàn tất giao dịch
                await transaction.CommitAsync();
                
                Console.WriteLine($"Đã tạo mới inventory thành công cho sản phẩm ID: {inventory.ProductId}, Số lượng: {inventory.Quantity}");
                return true;
            }
            catch (DbUpdateException dbEx)
            {
                string errorMessage = $"Lỗi cơ sở dữ liệu khi tạo mới inventory cho sản phẩm ID: {inventory.ProductId}";
                await HandleDbUpdateExceptionAsync(dbEx, transaction, errorMessage);
                return false;
            }
            catch (Exception ex)
            {
                string errorMessage = $"Lỗi khi tạo mới inventory cho sản phẩm ID: {inventory.ProductId}";
                await HandleExceptionAsync(ex, transaction, errorMessage);
                return false;
            }
        }
        catch (Exception ex)
        {
            string errorMessage = $"Lỗi ngoại lệ khi tạo mới inventory cho sản phẩm ID: {inventory.ProductId}";
            Console.WriteLine(errorMessage);
            Console.WriteLine($"Chi tiết lỗi: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            return false;
        }
    }
    
    /// <summary>
    /// Cập nhật thông tin tồn kho
    /// </summary>
    /// <param name="inventory">Thông tin tồn kho cần cập nhật</param>
    /// <returns>Task hoàn thành</returns>
    public async Task UpdateInventoryAsync(Inventory inventory)
    {
        try
        {
            // Sử dụng giao dịch để đảm bảo tính nhất quán
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Kiểm tra xem inventory có tồn tại không
                var existingInventory = await _context.Inventories.FindAsync(inventory.Id);
                if (existingInventory == null)
                {
                    Console.WriteLine($"Không tìm thấy inventory với ID: {inventory.Id}, không thể cập nhật");
                    await transaction.RollbackAsync();
                    return;
                }
                
                // Cập nhật thông tin
                existingInventory.Quantity = inventory.Quantity;
                existingInventory.MinimumStockLevel = inventory.MinimumStockLevel;
                existingInventory.MaximumStockLevel = inventory.MaximumStockLevel;
                existingInventory.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                
                // Đồng bộ với bảng Product
                var product = await _context.Products.FindAsync(existingInventory.ProductId);
                if (product != null && product.Stock != existingInventory.Quantity)
                {
                    product.Stock = existingInventory.Quantity;
                    await _context.SaveChangesAsync();
                    Console.WriteLine($"Đã đồng bộ số lượng tồn kho với sản phẩm ID: {product.Id}, Số lượng: {existingInventory.Quantity}");
                }
                
                // Hoàn tất giao dịch
                await transaction.CommitAsync();
                Console.WriteLine($"Đã cập nhật inventory thành công cho sản phẩm ID: {existingInventory.ProductId}, Số lượng: {existingInventory.Quantity}");
            }
            catch (DbUpdateException dbEx)
            {
                string errorMessage = $"Lỗi cơ sở dữ liệu khi cập nhật inventory ID: {inventory.Id}";
                await HandleDbUpdateExceptionAsync(dbEx, transaction, errorMessage);
            }
            catch (Exception ex)
            {
                string errorMessage = $"Lỗi khi cập nhật inventory ID: {inventory.Id}";
                await HandleExceptionAsync(ex, transaction, errorMessage);
            }
        }
        catch (Exception ex)
        {
            string errorMessage = $"Lỗi ngoại lệ khi cập nhật inventory ID: {inventory.Id}";
            Console.WriteLine(errorMessage);
            Console.WriteLine($"Chi tiết lỗi: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
        }
    }

        /// <summary>
        /// Lấy thông tin tồn kho theo ID sản phẩm
        /// </summary>
        /// <param name="productId">ID của sản phẩm</param>
        /// <returns>Thông tin tồn kho hoặc null nếu không tìm thấy</returns>
        public async Task<Inventory?> GetInventoryByProductIdAsync(int productId)
        {
            try
            {
                // Kiểm tra xem đã có giao dịch hiện tại chưa
                bool hasExistingTransaction = _context.Database.CurrentTransaction != null;
                IDbContextTransaction? transaction = null;
                
                if (!hasExistingTransaction)
                {
                    // Chỉ tạo giao dịch mới nếu chưa có giao dịch hiện tại
                    transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
                }
                try
                {
                    var inventory = await _context.Inventories
                        .Include(i => i.Product)
                        .FirstOrDefaultAsync(i => i.ProductId == productId);
                    
                    if (inventory == null)
                    {
                        // Nếu không tìm thấy inventory, kiểm tra trong bảng Product
                        var product = await _context.Products
                            .AsNoTracking()
                            .FirstOrDefaultAsync(p => p.Id == productId);
                        
                        if (product == null)
                        {
                            Console.WriteLine($"Không tìm thấy sản phẩm với ID: {productId}");
                            await transaction.RollbackAsync();
                            return null;
                        }
                        
                        // Tạo mới inventory từ thông tin sản phẩm
                        inventory = await CreateInventoryFromProductAsync(product);
                        if (inventory == null)
                        {
                            Console.WriteLine($"Không thể tạo inventory cho sản phẩm ID: {productId}");
                            await transaction.RollbackAsync();
                            return null;
                        }
                    }
                    
                    await transaction.CommitAsync();
                    return inventory; // This could return null, which is expected behavior
                }
                catch (DbUpdateException dbEx)
                {
                    string errorMessage = $"Lỗi cơ sở dữ liệu khi lấy thông tin tồn kho cho sản phẩm ID: {productId}";
                    await HandleDbUpdateExceptionAsync(dbEx, transaction, errorMessage);
                    
                    // Thử lấy lại inventory sau khi đồng bộ
                    var syncedInventory = await _context.Inventories
                        .Include(i => i.Product)
                        .FirstOrDefaultAsync(i => i.ProductId == productId);
                    if (syncedInventory != null)
                    {
                        Console.WriteLine($"Đã tìm thấy inventory sau khi đồng bộ cho sản phẩm ID: {productId}");
                        return syncedInventory;
                    }
                    
                    return null!;
                }
                catch (Exception ex)
                {
                    string errorMessage = $"Lỗi khi lấy thông tin tồn kho cho sản phẩm ID: {productId}";
                    await HandleExceptionAsync(ex, transaction, errorMessage);
                    return null!;
                }
            }
            catch (Exception ex)
            {
                string errorMessage = $"Lỗi ngoại lệ khi lấy thông tin tồn kho cho sản phẩm ID: {productId}";
                Console.WriteLine(errorMessage);
                Console.WriteLine($"Chi tiết lỗi: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                return null!;
            }
        }

        /// <summary>
        /// Lấy thông tin tồn kho theo ID
        /// </summary>
        /// <param name="id">ID của tồn kho cần lấy</param>
        /// <returns>Thông tin tồn kho nếu tìm thấy, null nếu không tìm thấy hoặc có lỗi</returns>
        public async Task<Inventory?> GetInventoryByIdAsync(int id)
        {
            try
            {
                // Kiểm tra xem đã có giao dịch hiện tại chưa
                bool hasExistingTransaction = _context.Database.CurrentTransaction != null;
                IDbContextTransaction? transaction = null;
                
                if (!hasExistingTransaction)
                {
                    // Chỉ tạo giao dịch mới nếu chưa có giao dịch hiện tại
                    transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
                }
                else
                {
                    // Sử dụng giao dịch hiện tại
                    transaction = _context.Database.CurrentTransaction;
                }
                try
                {
                    var inventory = await _context.Inventories
                        .Include(i => i.Product)
                        .FirstOrDefaultAsync(i => i.Id == id);
                    
                    await transaction.CommitAsync();
                    
                    if (inventory != null)
                    {
                        Console.WriteLine($"Đã tìm thấy tồn kho ID {id} cho sản phẩm {inventory.Product?.Name ?? "không xác định"}");
                    }
                    else
                    {
                        Console.WriteLine($"Không tìm thấy tồn kho với ID {id}");
                    }
                    
                    return inventory;
                }
                catch (DbUpdateException dbEx)
                {
                    string errorMessage = $"Lỗi cơ sở dữ liệu khi lấy thông tin tồn kho ID {id}";
                    await HandleDbUpdateExceptionAsync(dbEx, transaction, errorMessage);
                    
                    // Thử đồng bộ hóa lại tồn kho
                    Console.WriteLine("Đang thử đồng bộ hóa lại tồn kho...");
                    await SynchronizeProductStockAsync();
                    
                    return null;
                }
                catch (Exception ex)
                {
                    string errorMessage = $"Lỗi khi lấy thông tin tồn kho ID {id}";
                    await HandleExceptionAsync(ex, transaction, errorMessage);
                    return null;
                }
            }
            catch (Exception ex)
            {
                string errorMessage = $"Lỗi ngoại lệ khi lấy thông tin tồn kho ID {id}";
                await HandleExceptionAsync(ex, null, errorMessage);
                return null;
            }
        }

        /// <summary>
        /// Lấy tất cả thông tin tồn kho
        /// </summary>
        /// <returns>Danh sách tất cả các tồn kho trong hệ thống</returns>
        public async Task<IEnumerable<Inventory>> GetAllInventoriesAsync()
        {
            try
            {
                // Sử dụng giao dịch chỉ đọc để đảm bảo tính nhất quán
                await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
                try
                {
                    var inventories = await _context.Inventories
                        .Include(i => i.Product)
                        .ToListAsync();
                    
                    await transaction.CommitAsync();
                    Console.WriteLine($"Đã lấy thành công {inventories.Count} bản ghi tồn kho");
                    return inventories;
                }
                catch (DbUpdateException dbEx)
                {
                    string errorMessage = $"Lỗi cơ sở dữ liệu khi lấy danh sách tồn kho";
                    await HandleDbUpdateExceptionAsync(dbEx, transaction, errorMessage);
                    return [];
                }
                catch (Exception ex)
                {
                    string errorMessage = $"Lỗi khi lấy danh sách tồn kho";
                    await HandleExceptionAsync(ex, transaction, errorMessage);
                    return [];
                }
            }
            catch (Exception ex)
            {
                string errorMessage = $"Lỗi ngoại lệ khi lấy danh sách tồn kho";
                await HandleExceptionAsync(ex, null, errorMessage);
                return [];
            }
        }

        /// <summary>
        /// Lấy danh sách các sản phẩm có tồn kho thấp
        /// </summary>
        /// <returns>Danh sách các sản phẩm có tồn kho thấp hơn hoặc bằng mức tối thiểu</returns>
        public async Task<IEnumerable<Inventory>> GetLowStockInventoriesAsync()
        {
            try
            {
                // Sử dụng giao dịch chỉ đọc để đảm bảo tính nhất quán
                await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
                try
                {
                    var inventories = await _context.Inventories
                        .Include(i => i.Product)
                        .Where(i => i.Quantity <= i.MinimumStockLevel)
                        .ToListAsync();
                    
                    await transaction.CommitAsync();
                    Console.WriteLine($"Đã tìm thấy {inventories.Count} sản phẩm có tồn kho thấp");
                    return inventories;
                }
                catch (DbUpdateException dbEx)
                {
                    string errorMessage = $"Lỗi cơ sở dữ liệu khi lấy danh sách tồn kho thấp";
                    await HandleDbUpdateExceptionAsync(dbEx, transaction, errorMessage);
                    return [];
                }
                catch (Exception ex)
                {
                    string errorMessage = $"Lỗi khi lấy danh sách tồn kho thấp";
                    await HandleExceptionAsync(ex, transaction, errorMessage);
                    return [];
                }
            }
            catch (Exception ex)
            {
                string errorMessage = $"Lỗi ngoại lệ khi lấy danh sách tồn kho thấp";
                await HandleExceptionAsync(ex, null, errorMessage);
                return [];
            }
        }

    /// <summary>
    /// Lấy số lượng tồn kho của sản phẩm
    /// </summary>
    /// <param name="productId">ID của sản phẩm</param>
    /// <returns>Số lượng tồn kho, 0 nếu không tìm thấy hoặc có lỗi</returns>
    public async Task<int> GetProductStockAsync(int productId)
    {
        try
        {
            // Sử dụng giao dịch chỉ đọc để đảm bảo tính nhất quán
            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
            try
            {
                var inventory = await _context.Inventories
                    .FirstOrDefaultAsync(i => i.ProductId == productId);

                if (inventory != null)
                {
                    Console.WriteLine($"Đã tìm thấy tồn kho cho sản phẩm ID {productId}: {inventory.Quantity}");
                    await transaction.CommitAsync();
                    return inventory.Quantity;
                }

                // Nếu không tìm thấy inventory, kiểm tra trong bảng Product
                Console.WriteLine($"Không tìm thấy tồn kho cho sản phẩm ID {productId}, kiểm tra trong bảng Product");
                var product = await _context.Products
                    .FirstOrDefaultAsync(p => p.Id == productId);

                if (product != null)
                {
                    Console.WriteLine($"Đã tìm thấy sản phẩm ID {productId} với số lượng: {product.Stock}");
                    
                    // Tạo mới inventory từ thông tin sản phẩm
                    await CreateInventoryFromProductAsync(product);
                    
                    await transaction.CommitAsync();
                    return product.Stock;
                }

                Console.WriteLine($"Không tìm thấy sản phẩm ID {productId}");
                await transaction.CommitAsync();
                return 0;
            }
            catch (DbUpdateException dbEx)
            {
                string errorMessage = $"Lỗi cơ sở dữ liệu khi lấy số lượng tồn kho cho sản phẩm ID {productId}";
                await HandleDbUpdateExceptionAsync(dbEx, transaction, errorMessage);
                return 0;
            }
            catch (Exception ex)
            {
                string errorMessage = $"Lỗi khi lấy số lượng tồn kho cho sản phẩm ID {productId}";
                await HandleExceptionAsync(ex, transaction, errorMessage);
                return 0;
            }
        }
        catch (Exception ex)
        {
            string errorMessage = $"Lỗi ngoại lệ khi lấy số lượng tồn kho cho sản phẩm ID {productId}";
            await HandleExceptionAsync(ex, null, errorMessage);
            return 0;
        }
    }

        // Đối tượng khóa đã được định nghĩa ở trên
        // private static readonly SemaphoreSlim _semaphore = new(1, 1);

        /// <summary>
        /// Kiểm tra xem sản phẩm có đủ số lượng tồn kho không
        /// </summary>
        /// <param name="productId">ID của sản phẩm</param>
        /// <param name="quantity">Số lượng cần kiểm tra, mặc định là 1</param>
        /// <returns>True nếu có đủ tồn kho, False nếu không đủ hoặc có lỗi</returns>
        public async Task<bool> IsInStockAsync(int productId, int quantity = 1)
        {
            // Sử dụng khóa để đảm bảo chỉ có một luồng có thể kiểm tra tồn kho tại một thời điểm
            await _semaphore.WaitAsync();
            
            try
            {
                // Kiểm tra xem đã có giao dịch hiện tại chưa
                var currentTransaction = _context.Database.CurrentTransaction;
                // Chỉ tạo giao dịch mới nếu chưa có giao dịch hiện tại
                await using var transaction = currentTransaction ?? await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
                try
                {
                    // Kiểm tra cả hai bảng để đảm bảo tính nhất quán
                    var product = await _context.Products
                        .AsNoTracking()
                        .FirstOrDefaultAsync(p => p.Id == productId);
                    
                    var inventory = await _context.Inventories
                        .AsNoTracking()
                        .FirstOrDefaultAsync(i => i.ProductId == productId);
                    
                    // Nếu không tìm thấy sản phẩm
                    if (product == null)
                    {
                        Console.WriteLine($"Không tìm thấy sản phẩm với ID: {productId}");
                        if (transaction != null)
                        {
                            await transaction.RollbackAsync();
                        }
                        return false;
                    }
                    
                    // Nếu không tìm thấy inventory, tạo mới từ thông tin sản phẩm
                    if (inventory == null)
                    {
                        // Kiểm tra số lượng tồn kho trong bảng Product
                        if (product.Stock < quantity)
                        {
                            Console.WriteLine($"Sản phẩm ID: {productId} không đủ số lượng trong kho. Yêu cầu: {quantity}, Hiện có: {product.Stock}");
                            if (transaction != null)
                            {
                                await transaction.RollbackAsync();
                            }
                            return false;
                        }
                        
                        // Tạo inventory mới dựa trên thông tin từ Product
                        var newInventory = await CreateInventoryFromProductAsync(product);
                        if (newInventory == null)
                        {
                            Console.WriteLine($"Không thể tạo inventory mới cho sản phẩm ID: {productId}");
                            if (transaction != null)
                            {
                                await transaction.RollbackAsync();
                            }
                            return false;
                        }
                        
                        Console.WriteLine($"Đã tạo inventory mới cho sản phẩm ID: {productId} với số lượng: {newInventory.Quantity}");
                        if (transaction != null)
                        {
                            await transaction.CommitAsync();
                        }
                        return newInventory.Quantity >= quantity;
                    }
                    
                    // Kiểm tra nếu có sự không nhất quán giữa Product.Stock và Inventory.Quantity
                    if (product.Stock != inventory.Quantity)
                    {
                        Console.WriteLine($"Phát hiện không nhất quán cho sản phẩm ID: {product.Id}, Tên: {product.Name}");
                        Console.WriteLine($"Product.Stock: {product.Stock}, Inventory.Quantity: {inventory.Quantity}");
                        
                        // Sử dụng giá trị lớn hơn để đảm bảo không bị lỗi hết hàng
                        int newStock = Math.Max(product.Stock, inventory.Quantity);
                        
                        // Cập nhật cả hai giá trị
                        product.Stock = newStock;
                        inventory.Quantity = newStock;
                        inventory.UpdatedAt = DateTime.UtcNow;
                        
                        await _context.SaveChangesAsync();
                        Console.WriteLine($"Đã sửa thành: Product.Stock = {product.Stock}, Inventory.Quantity = {inventory.Quantity}");
                    }
                    
                    // Kiểm tra số lượng tồn kho sau khi đã đồng bộ
                    bool isInStock = inventory.Quantity >= quantity;
                    
                    if (!isInStock)
                    {
                        Console.WriteLine($"Sản phẩm ID: {productId} không đủ số lượng trong kho. Yêu cầu: {quantity}, Hiện có: {inventory.Quantity}");
                    }
                    
                    if (transaction != null)
                    {
                        await transaction.CommitAsync();
                    }
                    return isInStock;
                }
                catch (DbUpdateException dbEx)
                {
                    string errorMessage = $"Lỗi cơ sở dữ liệu khi kiểm tra tồn kho cho sản phẩm ID: {productId}";
                    await HandleDbUpdateExceptionAsync(dbEx, transaction, errorMessage);
                    return false;
                }
                catch (Exception ex)
                {
                    string errorMessage = $"Lỗi khi kiểm tra tồn kho cho sản phẩm ID: {productId}";
                    await HandleExceptionAsync(ex, transaction, errorMessage);
                    return false;
                }
            }
            catch (Exception ex)
            {
                string errorMessage = $"Lỗi ngoại lệ khi kiểm tra tồn kho cho sản phẩm ID: {productId}";
                await HandleExceptionAsync(ex, null, errorMessage);
                return false;
            }
            finally
            {
                // Giải phóng semaphore
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Cập nhật số lượng tồn kho của sản phẩm
        /// </summary>
        /// <param name="productId">ID của sản phẩm</param>
        /// <param name="quantity">Số lượng mới</param>
        /// <returns>True nếu cập nhật thành công, False nếu có lỗi</returns>
        public async Task<bool> UpdateStockAsync(int productId, int quantity)
        {
            try
            {
                // Kiểm tra xem đã có giao dịch hiện tại chưa
                var currentTransaction = _context.Database.CurrentTransaction;
                // Chỉ tạo giao dịch mới nếu chưa có giao dịch hiện tại
                await using var transaction = currentTransaction ?? await _context.Database.BeginTransactionAsync();
                try
                {
                    // Sử dụng khóa để đảm bảo chỉ có một luồng có thể cập nhật tồn kho tại một thời điểm
                    await _semaphore.WaitAsync();
                    try
                    {
                        // Lấy thông tin tồn kho mới nhất từ cơ sở dữ liệu
                        var inventory = await _context.Inventories
                            .Include(i => i.Product)
                            .FirstOrDefaultAsync(i => i.ProductId == productId);
                            
                        // Nếu không tìm thấy inventory, kiểm tra trong bảng Product
                        if (inventory == null)
                        {
                            var product = await _context.Products.FindAsync(productId);
                            if (product == null)
                            {
                                await transaction.RollbackAsync();
                                return false;
                            }
                            
                            // Tạo inventory mới dựa trên thông tin từ Product
                            inventory = await CreateInventoryFromProductAsync(product);
                        }

                        if (inventory != null)
                        {
                            // Kiểm tra nếu có sự không nhất quán giữa Product.Stock và Inventory.Quantity
                            if (inventory.Product != null && inventory.Product.Stock != inventory.Quantity)
                            {
                                Console.WriteLine($"Phát hiện không nhất quán cho sản phẩm ID: {inventory.ProductId}, Tên: {inventory.Product.Name}");
                                Console.WriteLine($"Product.Stock: {inventory.Product.Stock}, Inventory.Quantity: {inventory.Quantity}");
                            }
                            
                            // Cập nhật số lượng tồn kho mới
                            inventory.Quantity = quantity;
                            inventory.UpdatedAt = DateTime.UtcNow;
                            
                            // Cập nhật LastStockOutDate nếu hết hàng
                            if (quantity == 0)
                            {
                                inventory.LastStockOutDate = DateTime.UtcNow;
                            }
                            // Cập nhật LastRestockDate nếu tăng số lượng
                            else if (quantity > inventory.Quantity)
                            {
                                inventory.LastRestockDate = DateTime.UtcNow;
                            }
                        }
                        else
                        {
                            await transaction.RollbackAsync();
                            Console.WriteLine("Không thể cập nhật tồn kho vì inventory là null");
                            return false;
                        }

                        // Đồng bộ hóa với bảng Product
                        if (inventory.Product != null)
                        {
                            inventory.Product.Stock = quantity;
                            Console.WriteLine($"Đã cập nhật Product.Stock = {quantity} cho sản phẩm ID: {productId}");
                        }
                        else
                        {
                            var product = await _context.Products.FindAsync(productId);
                            if (product != null)
                            {
                                product.Stock = quantity;
                                Console.WriteLine($"Đã cập nhật Product.Stock = {quantity} cho sản phẩm ID: {productId}");
                            }
                        }

                        _context.Inventories.Update(inventory);
                        await _context.SaveChangesAsync();
                        
                        // Hoàn tất giao dịch
                        await transaction.CommitAsync();

                        // Kiểm tra nếu tồn kho thấp thì gửi thông báo
                        if (inventory.Quantity <= inventory.MinimumStockLevel)
                        {
                            await _emailService.SendLowStockNotificationAsync(productId);
                        }

                        return true;
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                }
                catch (DbUpdateException dbEx)
                {
                    string errorMessage = $"Lỗi cơ sở dữ liệu khi cập nhật tồn kho cho sản phẩm ID: {productId}";
                    await HandleDbUpdateExceptionAsync(dbEx, transaction, errorMessage);
                    return false;
                }
                catch (Exception ex)
                {
                    string errorMessage = $"Lỗi khi cập nhật tồn kho cho sản phẩm ID: {productId}";
                    await HandleExceptionAsync(ex, transaction, errorMessage);
                    return false;
                }
                finally
                {
                    // Đảm bảo luôn giải phóng khóa
                    _semaphore.Release();
                }
            }
            catch (Exception ex)
            {
                string errorMessage = $"Lỗi ngoại lệ khi cập nhật tồn kho cho sản phẩm ID: {productId}";
                await HandleExceptionAsync(ex, null, errorMessage);
                return false;
            }
        }
        // End of commented out method

        // Duplicate method - commented out to avoid CS0111 error
        /*
        public async Task<bool> DeductStockAsync(int productId, int quantity)
        {
            try
            {
                // Sử dụng giao dịch để đảm bảo tính nhất quán
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // Sử dụng khóa để đảm bảo chỉ có một luồng có thể cập nhật tồn kho tại một thời điểm
                    await _semaphore.WaitAsync();
                    
                    // Lấy thông tin tồn kho mới nhất từ cơ sở dữ liệu (không sử dụng AsNoTracking để có thể cập nhật)
                    var inventory = await _context.Inventories
                        .Include(i => i.Product)
                        .FirstOrDefaultAsync(i => i.ProductId == productId);
                        
                    // Nếu không tìm thấy inventory, kiểm tra trong bảng Product
                    if (inventory == null)
                    {
                        var product = await _context.Products.FindAsync(productId);
                        if (product == null)
                        {
                            Console.WriteLine($"Không tìm thấy sản phẩm với ID: {productId}");
                            return false;
                        }
                        
                        if (product.Stock < quantity)
                        {
                            Console.WriteLine($"Sản phẩm ID: {productId} không đủ số lượng trong kho. Yêu cầu: {quantity}, Hiện có: {product.Stock}");
                            return false;
                        }
                        
                        // Tạo inventory mới dựa trên thông tin từ Product
                        inventory = await CreateInventoryFromProductAsync(product);
                        if (inventory == null)
                        {
                            Console.WriteLine($"Không thể tạo inventory mới cho sản phẩm ID: {productId}");
                            return false;
                        }
                    }
                    
                    // Kiểm tra lại số lượng tồn kho sau khi đã tạo hoặc lấy inventory
                    if (inventory.Quantity < quantity)
                    {
                        Console.WriteLine($"Sản phẩm ID: {productId} không đủ số lượng trong kho. Yêu cầu: {quantity}, Hiện có: {inventory.Quantity}");
                        return false;
                    }

                    // Cập nhật số lượng tồn kho
                    inventory.Quantity -= quantity;
                    inventory.UpdatedAt = DateTime.UtcNow;
                    
                    // Cập nhật LastStockOutDate nếu số lượng giảm xuống 0
                    if (inventory.Quantity == 0)
                    {
                        inventory.LastStockOutDate = DateTime.UtcNow;
                    }

                    // Đồng bộ hóa với bảng Product
                    if (inventory.Product != null)
                    {
                        inventory.Product.Stock = inventory.Quantity;
                    }
                    else
                    {
                        var product = await _context.Products.FindAsync(productId);
                        if (product != null)
                        {
                            product.Stock = inventory.Quantity;
                        }
                    }

                    _context.Inventories.Update(inventory);
                    await _context.SaveChangesAsync();

                    // Hoàn tất giao dịch
                    await transaction.CommitAsync();

                    // Kiểm tra nếu tồn kho thấp thì gửi thông báo
                    if (inventory.Quantity <= inventory.MinimumStockLevel)
                    {
                        await _emailService.SendLowStockNotificationAsync(productId);
                    }

                    Console.WriteLine($"Đã cập nhật tồn kho thành công cho sản phẩm ID: {productId}. Số lượng còn lại: {inventory.Quantity}");
                    return true;
                }
                catch (DbUpdateException dbEx)
                {
                    // Hủy giao dịch nếu có lỗi cơ sở dữ liệu
                    await transaction.RollbackAsync();
                    
                    // Log lỗi cơ sở dữ liệu chi tiết
                    Console.WriteLine($"Lỗi cơ sở dữ liệu khi cập nhật tồn kho cho sản phẩm ID: {productId}, Số lượng: {quantity}");
                    Console.WriteLine($"Chi tiết lỗi: {dbEx.Message}");
                    if (dbEx.InnerException != null)
                    {
                        Console.WriteLine($"Inner Exception: {dbEx.InnerException.Message}");
                    }
                    Console.WriteLine($"Stack Trace: {dbEx.StackTrace}");
                    
                    // Thử đồng bộ hóa dữ liệu tồn kho
                    try
                    {
                        await SynchronizeProductStockAsync();
                        Console.WriteLine($"Đã thực hiện đồng bộ hóa tồn kho sau lỗi cập nhật cho sản phẩm ID: {productId}");
                    }
                    catch (Exception syncEx)
                    {
                        Console.WriteLine($"Không thể đồng bộ hóa tồn kho sau lỗi: {syncEx.Message}");
                    }
                    
                    return false;
                }
                catch (Exception ex)
                {
                    // Hủy giao dịch nếu có lỗi
                    await transaction.RollbackAsync();
                    
                    // Log lỗi chi tiết hơn
                    Console.WriteLine($"Lỗi khi cập nhật tồn kho cho sản phẩm ID: {productId}, Số lượng: {quantity}");
                    Console.WriteLine($"Chi tiết lỗi: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                    }
                    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                    return false;
                }
                finally
                {
                    // Đảm bảo luôn giải phóng khóa
                    _semaphore.Release();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi ngoại lệ khi cập nhật tồn kho cho sản phẩm ID: {productId}");
                Console.WriteLine($"Chi tiết lỗi: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                return false;
            }
        }
        // End of commented out method

        // Duplicate method - commented out to avoid CS0111 error
        /*
        public async Task<bool> RestockAsync(int productId, int quantity)
        {
            try
            {
                // Sử dụng giao dịch để đảm bảo tính nhất quán
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var inventory = await _context.Inventories
                        .FirstOrDefaultAsync(i => i.ProductId == productId);
                    
                    if (inventory == null)
                    {
                        // Nếu không tìm thấy inventory, tạo mới từ sản phẩm
                        var productItem = await _context.Products.FindAsync(productId);
                        if (productItem == null)
                        {
                            Console.WriteLine($"Không tìm thấy sản phẩm với ID: {productId}");
                            return false;
                        }
                        
                        inventory = await CreateInventoryFromProductAsync(productItem);
                        if (inventory == null)
                        {
                            Console.WriteLine($"Không thể tạo inventory cho sản phẩm ID: {productId}");
                            return false;
                        }
                    }
                    
                    // Cộng thêm số lượng tồn kho
                    inventory.Quantity += quantity;
                    inventory.UpdatedAt = DateTime.UtcNow;
                    inventory.LastRestockDate = DateTime.UtcNow;
                    
                    // Cập nhật số lượng trong bảng Product
                    var product = await _context.Products.FindAsync(productId);
                    if (product != null)
                    {
                        product.Stock = inventory.Quantity;
                    }
                    
                    await _context.SaveChangesAsync();
                    
                    // Hoàn tất giao dịch
                    await transaction.CommitAsync();
                    Console.WriteLine($"Đã thêm {quantity} đơn vị vào tồn kho của sản phẩm ID: {productId}. Hiện có: {inventory.Quantity}");
                    return true;
                }
                catch (DbUpdateException dbEx)
                {
                    // Hủy giao dịch nếu có lỗi cơ sở dữ liệu
                    await transaction.RollbackAsync();
                    Console.WriteLine($"Lỗi cơ sở dữ liệu khi thêm tồn kho cho sản phẩm ID: {productId}");
                    Console.WriteLine($"Chi tiết lỗi: {dbEx.Message}");
                    if (dbEx.InnerException != null)
                    {
                        Console.WriteLine($"Inner Exception: {dbEx.InnerException.Message}");
                    }
                    Console.WriteLine($"Stack Trace: {dbEx.StackTrace}");
                    
                    // Thử đồng bộ hóa lại tồn kho
                    Console.WriteLine("Đang thử đồng bộ hóa lại tồn kho...");
                    await SynchronizeProductStockAsync();
                    
                    return false;
                }
                catch (Exception ex)
                {
                    // Hủy giao dịch nếu có lỗi
                    await transaction.RollbackAsync();
                    Console.WriteLine($"Lỗi khi thêm tồn kho cho sản phẩm ID: {productId}");
                    Console.WriteLine($"Chi tiết lỗi: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                    }
                    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi ngoại lệ khi thêm tồn kho cho sản phẩm ID: {productId}");
                Console.WriteLine($"Chi tiết lỗi: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                return false;
            }
        }
        */

        /* Duplicate method - commented out to avoid CS0111 error
        public async Task<bool> CreateInventoryAsync(Inventory inventory)
        {
            try
            {
                // Sử dụng giao dịch để đảm bảo tính nhất quán
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try 
                {
                    // Method body commented out to avoid duplicate method definition
                }
            }
        }
        */

        // Phương thức UpdateInventoryAsync đã được định nghĩa ở trên - Đã xóa để tránh trùng lặp
        /*
        public async Task UpdateInventoryAsync(Inventory inventory)
        {
            try
            {
                // Sử dụng giao dịch để đảm bảo tính nhất quán
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
        */
        // Phương thức đã được định nghĩa ở trên - Đã xóa để tránh trùng lặp

        // Duplicate method - commented out to avoid CS0111 error
        /*
        public async Task SendLowStockNotificationsAsync()
        {
            try
            {
                Console.WriteLine("Bắt đầu gửi thông báo tồn kho thấp...");
                var lowStockItems = await GetLowStockInventoriesAsync();
                
                if (!lowStockItems.Any())
                {
                    Console.WriteLine("Không có sản phẩm nào có tồn kho thấp cần thông báo");
                    return;
                }
                
                int successCount = 0;
                int failCount = 0;
                
                foreach (var item in lowStockItems)
                {
                    try
                    {
                        await _emailService.SendLowStockNotificationAsync(item.ProductId);
                        Console.WriteLine($"Đã gửi thông báo tồn kho thấp cho sản phẩm ID: {item.ProductId}, Tên: {item.Product?.Name}, Số lượng: {item.Quantity}");
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Lỗi khi gửi thông báo tồn kho thấp cho sản phẩm ID: {item.ProductId}");
                        Console.WriteLine($"Chi tiết lỗi: {ex.Message}");
                        if (ex.InnerException != null)
                        {
                            Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                        }
                        Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                        failCount++;
                    }
                }
                
                Console.WriteLine($"Hoàn tất gửi thông báo tồn kho thấp. Thành công: {successCount}, Thất bại: {failCount}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi khi gửi thông báo tồn kho thấp");
                Console.WriteLine($"Chi tiết lỗi: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
        }
        */
        
        /// <summary>
        /// Tạo mới inventory từ thông tin sản phẩm
        /// </summary>
        /// <param name="product">Thông tin sản phẩm</param>
        /// <returns>Đối tượng Inventory mới được tạo</returns>
        // Phương thức đã được định nghĩa ở trên - Đã xóa để tránh trùng lặp

        // Phương thức SynchronizeProductStockAsync đã được định nghĩa ở trên - Đã xóa để tránh trùng lặp
        
        // Duplicate method - commented out to avoid CS0111 error
        /*
        public async Task<bool> DeductStockAsync(int productId, int quantity)
        {
            try
            {
                // Sử dụng giao dịch để đảm bảo tính nhất quán
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // Sử dụng khóa để đảm bảo chỉ có một luồng có thể cập nhật tồn kho tại một thời điểm
                    await _semaphore.WaitAsync();
                    
                    // Lấy thông tin tồn kho mới nhất từ cơ sở dữ liệu (không sử dụng AsNoTracking để có thể cập nhật)
                    var inventory = await _context.Inventories
                        .Include(i => i.Product)
                        .FirstOrDefaultAsync(i => i.ProductId == productId);
                        
                    // Nếu không tìm thấy inventory, kiểm tra trong bảng Product
                    if (inventory == null)
                    {
                        var product = await _context.Products
                            .FirstOrDefaultAsync(p => p.Id == productId);
                        
                        if (product == null)
                        {
                            Console.WriteLine($"Không tìm thấy sản phẩm với ID: {productId}");
                            await transaction.RollbackAsync();
                            _semaphore.Release();
                            return false;
                        }
                        
                        // Kiểm tra số lượng tồn kho trong bảng Product
                        if (product.Stock < quantity)
                        {
                            Console.WriteLine($"Sản phẩm ID: {productId} không đủ số lượng trong kho. Yêu cầu: {quantity}, Hiện có: {product.Stock}");
                            await transaction.RollbackAsync();
                            _semaphore.Release();
                            return false;
                        }
                        
                        // Tạo inventory mới dựa trên thông tin từ Product
                        inventory = await CreateInventoryFromProductAsync(product);
                        if (inventory == null)
                        {
                            Console.WriteLine($"Không thể tạo inventory mới cho sản phẩm ID: {productId}");
                            await transaction.RollbackAsync();
                            _semaphore.Release();
                            return false;
                        }
                    }
                    
                    // Kiểm tra số lượng tồn kho
                    if (inventory.Quantity < quantity)
                    {
                        Console.WriteLine($"Sản phẩm ID: {productId} không đủ số lượng trong kho. Yêu cầu: {quantity}, Hiện có: {inventory.Quantity}");
                        await transaction.RollbackAsync();
                        _semaphore.Release();
                        return false;
                    }
                    
                    // Trừ số lượng tồn kho
                    inventory.Quantity -= quantity;
                    inventory.UpdatedAt = DateTime.UtcNow;
                    
                    // Cập nhật số lượng trong bảng Product
                    var product = await _context.Products.FindAsync(productId);
                    if (product != null)
                    {
                        product.Stock = inventory.Quantity;
                    }
                    
                    await _context.SaveChangesAsync();
                    
                    // Hoàn tất giao dịch
                    await transaction.CommitAsync();
                    Console.WriteLine($"Đã trừ {quantity} đơn vị từ tồn kho của sản phẩm ID: {productId}. Còn lại: {inventory.Quantity}");
                    _semaphore.Release();
                    return true;
                }
                catch (DbUpdateException dbEx)
                {
                    // Hủy giao dịch nếu có lỗi cơ sở dữ liệu
                    await transaction.RollbackAsync();
                    Console.WriteLine($"Lỗi cơ sở dữ liệu khi trừ tồn kho cho sản phẩm ID: {productId}");
                    Console.WriteLine($"Chi tiết lỗi: {dbEx.Message}");
                    if (dbEx.InnerException != null)
                    {
                        Console.WriteLine($"Inner Exception: {dbEx.InnerException.Message}");
                    }
                    Console.WriteLine($"Stack Trace: {dbEx.StackTrace}");
                    _semaphore.Release();
                    return false;
                }
                catch (Exception ex)
                {
                    // Hủy giao dịch nếu có lỗi
                    await transaction.RollbackAsync();
                    Console.WriteLine($"Lỗi khi trừ tồn kho cho sản phẩm ID: {productId}");
                    Console.WriteLine($"Chi tiết lỗi: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                    }
                    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                    _semaphore.Release();
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi ngoại lệ khi trừ tồn kho cho sản phẩm ID: {productId}");
                Console.WriteLine($"Chi tiết lỗi: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                return false;
            }
        }
        // End of commented out method
        
        // Duplicate method - commented out to avoid CS0111 error
        /*
        public async Task<bool> RestockAsync(int productId, int quantity)
        {
            try
            {
                // Sử dụng giao dịch để đảm bảo tính nhất quán
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // Sử dụng khóa để đảm bảo chỉ có một luồng có thể cập nhật tồn kho tại một thời điểm
                    await _semaphore.WaitAsync();
                    
                    // Lấy thông tin tồn kho mới nhất từ cơ sở dữ liệu (không sử dụng AsNoTracking để có thể cập nhật)
                    var inventory = await _context.Inventories
                        .Include(i => i.Product)
                        .FirstOrDefaultAsync(i => i.ProductId == productId);
                    
                    // Nếu không tìm thấy inventory, kiểm tra trong bảng Product
                    if (inventory == null)
                    {
                        var productItem = await _context.Products
                            .FirstOrDefaultAsync(p => p.Id == productId);
                        
                        if (productItem == null)
                        {
                            Console.WriteLine($"Không tìm thấy sản phẩm với ID: {productId}");
                            await transaction.RollbackAsync();
                            _semaphore.Release();
                            return false;
                        }
                        
                        inventory = await CreateInventoryFromProductAsync(productItem);
                        if (inventory == null)
                        {
                            Console.WriteLine($"Không thể tạo inventory cho sản phẩm ID: {productId}");
                            _semaphore.Release();
                            return false;
                        }
                    }
                    
                    // Cộng thêm số lượng tồn kho
                    inventory.Quantity += quantity;
                    inventory.UpdatedAt = DateTime.UtcNow;
                    inventory.LastRestockDate = DateTime.UtcNow;
                    
                    // Cập nhật số lượng trong bảng Product
                    var product = await _context.Products.FindAsync(productId);
                    if (product != null)
                    {
                        product.Stock = inventory.Quantity;
                    }
                    
                    await _context.SaveChangesAsync();
                    
                    // Hoàn tất giao dịch
                    await transaction.CommitAsync();
                    Console.WriteLine($"Đã thêm {quantity} đơn vị vào tồn kho của sản phẩm ID: {productId}. Hiện có: {inventory.Quantity}");
                    _semaphore.Release();
                    return true;
                }
                catch (DbUpdateException dbEx)
                {
                    // Hủy giao dịch nếu có lỗi cơ sở dữ liệu
                    await transaction.RollbackAsync();
                    Console.WriteLine($"Lỗi cơ sở dữ liệu khi thêm tồn kho cho sản phẩm ID: {productId}");
                    Console.WriteLine($"Chi tiết lỗi: {dbEx.Message}");
                    if (dbEx.InnerException != null)
                    {
                        Console.WriteLine($"Inner Exception: {dbEx.InnerException.Message}");
                    }
                    Console.WriteLine($"Stack Trace: {dbEx.StackTrace}");
                    _semaphore.Release();
                    return false;
                }
                catch (Exception ex)
                {
                    // Hủy giao dịch nếu có lỗi
                    await transaction.RollbackAsync();
                    Console.WriteLine($"Lỗi khi thêm tồn kho cho sản phẩm ID: {productId}");
                    Console.WriteLine($"Chi tiết lỗi: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                    }
                    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                    _semaphore.Release();
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi ngoại lệ khi thêm tồn kho cho sản phẩm ID: {productId}");
                Console.WriteLine($"Chi tiết lỗi: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                return false;
            }
        }
        
        /* Duplicate method - commented out to avoid CS0111 error
        public async Task<bool> CreateInventoryAsync(Inventory inventory)
        {
            try
            {
                // Sử dụng giao dịch để đảm bảo tính nhất quán
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // Kiểm tra xem inventory đã tồn tại chưa (để tránh tạo trùng lặp)
                    // Method body commented out to avoid duplicate method definition
                }
            }
        }
        */
        
        // Phương thức đã được định nghĩa ở trên - Đã xóa để tránh trùng lặp
    }
}