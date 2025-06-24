using iText.Html2pdf;
 using iText.Kernel.Pdf;
 using iText.Layout;
 using System.Text;

namespace CPTStore.Services.Helpers
{
    public static class PdfGenerator
    {
        /// <summary>
        /// Chuyển đổi nội dung HTML thành PDF sử dụng iText7
        /// </summary>
        /// <param name="htmlContent">Nội dung HTML cần chuyển đổi</param>
        /// <returns>Mảng byte của file PDF</returns>
        public static byte[] ConvertHtmlToPdf(string htmlContent)
        {
            using var memoryStream = new MemoryStream();
            
            // Tạo đối tượng PdfWriter để ghi vào MemoryStream
            using var writer = new PdfWriter(memoryStream);
            
            // Tạo đối tượng PdfDocument
            using var pdf = new PdfDocument(writer);
            
            // Chuyển đổi HTML thành PDF sử dụng ConverterProperties
            var converterProperties = new ConverterProperties();
            using var htmlStream = new MemoryStream(Encoding.UTF8.GetBytes(htmlContent));
            HtmlConverter.ConvertToPdf(htmlStream, pdf, converterProperties);
            
            // Đóng tài liệu PDF
            pdf.Close();
            
            // Trả về mảng byte của PDF
            return memoryStream.ToArray();
        }

        /// <summary>
        /// Chuyển đổi nội dung HTML thành PDF với các tùy chọn bổ sung
        /// </summary>
        /// <param name="htmlContent">Nội dung HTML cần chuyển đổi</param>
        /// <param name="title">Tiêu đề của tài liệu PDF</param>
        /// <param name="author">Tác giả của tài liệu PDF</param>
        /// <returns>Mảng byte của file PDF</returns>
        public static byte[] ConvertHtmlToPdfWithMetadata(string htmlContent, string title, string author)
        {
            using var memoryStream = new MemoryStream();
            
            // Tạo đối tượng PdfWriter để ghi vào MemoryStream
            using var writer = new PdfWriter(memoryStream);
            
            // Tạo đối tượng PdfDocument với các tùy chọn
            var pdfDocument = new PdfDocument(writer);
            
            // Thiết lập metadata
            var documentInfo = pdfDocument.GetDocumentInfo();
            documentInfo.SetTitle(title);
            documentInfo.SetAuthor(author);
            documentInfo.SetCreator("CPT Store PDF Generator");
            
            // Chuyển đổi HTML thành PDF sử dụng ConverterProperties
            var converterProperties = new ConverterProperties();
            using var htmlStream = new MemoryStream(Encoding.UTF8.GetBytes(htmlContent));
            HtmlConverter.ConvertToPdf(htmlStream, pdfDocument, converterProperties);
            
            // Đóng tài liệu PDF
            pdfDocument.Close();
            
            // Trả về mảng byte của PDF
            return memoryStream.ToArray();
        }

        /// <summary>
        /// Phương thức dự phòng để mô phỏng chuyển đổi HTML sang PDF khi không thể sử dụng iText7
        /// </summary>
        /// <param name="htmlContent">Nội dung HTML</param>
        /// <returns>Mảng byte của nội dung HTML</returns>
        public static byte[] SimulateHtmlToPdfConversion(string htmlContent)
        {
            // Phương thức này chỉ để dự phòng, trả về nội dung HTML dưới dạng byte array
            return Encoding.UTF8.GetBytes(htmlContent);
        }
    }
}