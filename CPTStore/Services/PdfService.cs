using CPTStore.Data;
using CPTStore.Models;
using CPTStore.Services.Helpers;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace CPTStore.Services
{
    public class PdfService(ApplicationDbContext context) : IPdfService
    {
        private readonly ApplicationDbContext _context = context;

        public async Task<byte[]> GenerateInvoicePdfAsync(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            ArgumentNullException.ThrowIfNull(order, $"Không tìm thấy đơn hàng với ID {orderId}");

            // Đọc template HTML từ file
            string templateContent = await TemplateHelper.GetTemplateContentAsync("InvoiceTemplate.html");
            
            // Chuẩn bị dữ liệu để thay thế vào template
            var replacements = PrepareInvoiceReplacements(order);
            
            // Tạo nội dung HTML cho các mục đơn hàng
            string orderItemsHtml = GenerateOrderItemsHtml(order.OrderItems);
            templateContent = TemplateHelper.ReplaceSection(templateContent, "OrderItems", orderItemsHtml);
            
            // Xử lý phần giảm giá (nếu có)
            if (order.DiscountAmount > 0)
            {
                string discountHtml = GenerateDiscountHtml(order.DiscountAmount, order.TotalAmount);
                templateContent = TemplateHelper.ReplaceSection(templateContent, "DiscountSection", discountHtml);
            }
            else
            {
                templateContent = TemplateHelper.RemoveSection(templateContent, "DiscountSection");
            }
            
            // Xử lý phần mã giao dịch (nếu có)
            if (!string.IsNullOrEmpty(order.TransactionId))
            {
                string transactionHtml = $"<p>Mã giao dịch: {order.TransactionId}</p>";
                templateContent = TemplateHelper.ReplaceSection(templateContent, "TransactionSection", transactionHtml);
            }
            else
            {
                templateContent = TemplateHelper.RemoveSection(templateContent, "TransactionSection");
            }
            
            // Thay thế các placeholder còn lại
            string htmlContent = TemplateHelper.ReplaceTemplateValues(templateContent, replacements);
            
            // Chuyển đổi HTML thành PDF sử dụng iText7
            byte[] pdfBytes = PdfGenerator.ConvertHtmlToPdfWithMetadata(
                htmlContent, 
                $"Hóa đơn - {order.OrderNumber}", 
                "CPT Store");

            return pdfBytes;
        }

        public async Task<byte[]> GenerateOrderConfirmationPdfAsync(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            ArgumentNullException.ThrowIfNull(order, $"Không tìm thấy đơn hàng với ID {orderId}");

            // Đọc template HTML từ file
            string templateContent = await TemplateHelper.GetTemplateContentAsync("OrderConfirmationTemplate.html");
            
            // Chuẩn bị dữ liệu để thay thế vào template
            var replacements = PrepareOrderConfirmationReplacements(order);
            
            // Tạo nội dung HTML cho các mục đơn hàng
            string orderItemsHtml = GenerateOrderItemsHtml(order.OrderItems);
            templateContent = TemplateHelper.ReplaceSection(templateContent, "OrderItems", orderItemsHtml);
            
            // Xử lý phần giảm giá (nếu có)
            if (order.DiscountAmount > 0)
            {
                string discountHtml = GenerateDiscountHtml(order.DiscountAmount, order.TotalAmount);
                templateContent = TemplateHelper.ReplaceSection(templateContent, "DiscountSection", discountHtml);
            }
            else
            {
                templateContent = TemplateHelper.RemoveSection(templateContent, "DiscountSection");
            }
            
            // Thay thế các placeholder còn lại
            string htmlContent = TemplateHelper.ReplaceTemplateValues(templateContent, replacements);
            
            // Chuyển đổi HTML thành PDF sử dụng iText7
            byte[] pdfBytes = PdfGenerator.ConvertHtmlToPdfWithMetadata(
                htmlContent, 
                $"Xác nhận đơn hàng - {order.OrderNumber}", 
                "CPT Store");

            return pdfBytes;
        }

        public async Task<byte[]> GenerateProductCatalogPdfAsync(int? categoryId = null)
        {
            IQueryable<Product> productsQuery = _context.Products
                .Include(p => p.Category);

            if (categoryId.HasValue)
            {
                productsQuery = productsQuery.Where(p => p.CategoryId == categoryId.Value);
            }

            var products = await productsQuery.ToListAsync();

            // Đọc template HTML từ file
            string templateContent = await TemplateHelper.GetTemplateContentAsync("ProductCatalogTemplate.html");
            
            // Chuẩn bị dữ liệu để thay thế vào template
            var replacements = new Dictionary<string, string>
            {
                { "CurrentDate", DateTime.Now.ToString("dd/MM/yyyy") },
                { "CurrentYear", DateTime.Now.Year.ToString() }
            };
            
            // Tạo nội dung HTML cho các sản phẩm
            string productItemsHtml = GenerateProductItemsHtml(products);
            templateContent = TemplateHelper.ReplaceSection(templateContent, "ProductItems", productItemsHtml);
            
            // Thay thế các placeholder còn lại
            string htmlContent = TemplateHelper.ReplaceTemplateValues(templateContent, replacements);
            
            // Chuyển đổi HTML thành PDF sử dụng iText7
            string title = categoryId.HasValue 
                ? $"Danh mục sản phẩm - {products.FirstOrDefault()?.Category?.Name ?? "Không xác định"}"
                : "Danh mục sản phẩm";
                
            byte[] pdfBytes = PdfGenerator.ConvertHtmlToPdfWithMetadata(
                htmlContent, 
                title, 
                "CPT Store");

            return pdfBytes;
        }

        public async Task<byte[]> GenerateShippingLabelPdfAsync(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            ArgumentNullException.ThrowIfNull(order, $"Không tìm thấy đơn hàng với ID {orderId}");

            // Đọc template HTML từ file
            string templateContent = await TemplateHelper.GetTemplateContentAsync("ShippingLabelTemplate.html");
            
            // Chuẩn bị dữ liệu để thay thế vào template
            var replacements = PrepareShippingLabelReplacements(order);
            
            // Xử lý phần COD (nếu có)
            if (order.PaymentMethod == PaymentMethod.COD)
            {
                string codHtml = $"<div>Số tiền thu hộ: {order.TotalAmount:N0} VNĐ</div>";
                templateContent = TemplateHelper.ReplaceSection(templateContent, "CODSection", codHtml);
            }
            else
            {
                templateContent = TemplateHelper.RemoveSection(templateContent, "CODSection");
            }
            
            // Thay thế các placeholder còn lại
            string htmlContent = TemplateHelper.ReplaceTemplateValues(templateContent, replacements);
            
            // Chuyển đổi HTML thành PDF sử dụng iText7
            byte[] pdfBytes = PdfGenerator.ConvertHtmlToPdfWithMetadata(
                htmlContent, 
                $"Phiếu giao hàng - {order.OrderNumber}", 
                "CPT Store");

            return pdfBytes;
        }

        #region Private Methods

        private Dictionary<string, string> PrepareInvoiceReplacements(Order order)
        {
            return new Dictionary<string, string>
            {
                { "OrderNumber", order.OrderNumber },
                { "CurrentDate", DateTime.Now.ToString("dd/MM/yyyy") },
                { "CurrentDateTime", DateTime.Now.ToString("dd/MM/yyyy HH:mm") },
                { "PaymentMethod", order.PaymentMethod.ToString() },
                { "PaymentStatus", order.PaymentStatus.ToString() },
                { "CustomerName", order.CustomerName },
                { "CustomerPhone", order.CustomerPhone },
                { "CustomerAddress", order.CustomerAddress },
                { "TotalAmount", order.TotalAmount.ToString("N0") }
            };
        }

        private Dictionary<string, string> PrepareOrderConfirmationReplacements(Order order)
        {
            return new Dictionary<string, string>
            {
                { "OrderNumber", order.OrderNumber },
                { "OrderDate", order.CreatedAt.ToString("dd/MM/yyyy HH:mm") },
                { "OrderStatus", order.Status.ToString() },
                { "CurrentDateTime", DateTime.Now.ToString("dd/MM/yyyy HH:mm") },
                { "PaymentMethod", order.PaymentMethod.ToString() },
                { "PaymentStatus", order.PaymentStatus.ToString() },
                { "CustomerName", order.CustomerName },
                { "CustomerPhone", order.CustomerPhone },
                { "CustomerAddress", order.CustomerAddress },
                { "TotalAmount", order.TotalAmount.ToString("N0") }
            };
        }

        private Dictionary<string, string> PrepareShippingLabelReplacements(Order order)
        {
            var itemCount = order.OrderItems != null ? order.OrderItems.Count() : 0;
            string paymentMethodText = order.PaymentMethod == PaymentMethod.COD 
                ? "Thanh toán khi nhận hàng (COD)" 
                : "Đã thanh toán";

            return new Dictionary<string, string>
            {
                { "OrderNumber", order.OrderNumber },
                { "OrderDate", order.CreatedAt.ToString("dd/MM/yyyy") },
                { "CustomerName", order.CustomerName },
                { "CustomerPhone", order.CustomerPhone },
                { "CustomerAddress", order.CustomerAddress },
                { "ItemCount", itemCount.ToString() },
                { "PaymentMethodText", paymentMethodText },
                { "CurrentDateTime", DateTime.Now.ToString("dd/MM/yyyy HH:mm") }
            };
        }

        private string GenerateOrderItemsHtml(IEnumerable<OrderItem> orderItems)
        {
            var sb = new StringBuilder();
            int index = 1;

            foreach (var item in orderItems)
            {
                sb.AppendLine("        <tr>");
                sb.AppendLine($"            <td>{index++}</td>");
                sb.AppendLine($"            <td>{item.Product?.Name ?? "Không xác định"}</td>");
                sb.AppendLine($"            <td>{item.Price:N0} VNĐ</td>");
                sb.AppendLine($"            <td>{item.Quantity}</td>");
                sb.AppendLine($"            <td>{item.Price * item.Quantity:N0} VNĐ</td>");
                sb.AppendLine("        </tr>");
            }

            return sb.ToString();
        }

        private string GenerateProductItemsHtml(List<Product> products)
        {
            var sb = new StringBuilder();

            foreach (var product in products)
            {
                sb.AppendLine("        <div class='product-card'>");
                sb.AppendLine($"            <div class='product-name'>{product.Name}</div>");
                sb.AppendLine($"            <div class='product-category'>Danh mục: {product.Category?.Name ?? "Không có danh mục"}</div>");
                sb.AppendLine($"            <div class='product-price'>{product.Price:N0} VNĐ</div>");
                sb.AppendLine($"            <div class='product-description'>{product.ShortDescription}</div>");
                sb.AppendLine($"            <div>SKU: {product.SKU}</div>");
                sb.AppendLine("        </div>");
            }

            return sb.ToString();
        }

        private string GenerateDiscountHtml(decimal discountAmount, decimal totalAmount)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("        <tr>");
            sb.AppendLine("            <td colspan='4' style='text-align: right;'>Giảm giá:</td>");
            sb.AppendLine($"            <td>{discountAmount:N0} VNĐ</td>");
            sb.AppendLine("        </tr>");
            sb.AppendLine("        <tr class='total-row'>");
            sb.AppendLine("            <td colspan='4' style='text-align: right;'>Thành tiền:</td>");
            sb.AppendLine($"            <td>{(totalAmount - discountAmount):N0} VNĐ</td>");
            sb.AppendLine("        </tr>");
            
            return sb.ToString();
        }

        #endregion
    }
}