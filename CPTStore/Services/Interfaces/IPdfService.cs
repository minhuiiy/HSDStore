namespace CPTStore.Services
{
    public interface IPdfService
    {
        Task<byte[]> GenerateInvoicePdfAsync(int orderId);
        Task<byte[]> GenerateOrderConfirmationPdfAsync(int orderId);
        Task<byte[]> GenerateProductCatalogPdfAsync(int? categoryId = null);
    }
}