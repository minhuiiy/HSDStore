using System.Text.RegularExpressions;

namespace CPTStore.Services.Helpers
{
    public static class TemplateHelper
    {
        private static readonly string TemplateDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Services", "Templates");

        /// <summary>
        /// Đọc nội dung của template từ file
        /// </summary>
        /// <param name="templateName">Tên file template (không bao gồm đường dẫn)</param>
        /// <returns>Nội dung của template</returns>
        public static async Task<string> GetTemplateContentAsync(string templateName)
        {
            string templatePath = Path.Combine(TemplateDirectory, templateName);
            if (!File.Exists(templatePath))
            {
                throw new FileNotFoundException($"Template không tồn tại: {templateName}");
            }

            return await File.ReadAllTextAsync(templatePath);
        }

        /// <summary>
        /// Thay thế các placeholder trong template với giá trị thực tế
        /// </summary>
        /// <param name="template">Nội dung template</param>
        /// <param name="replacements">Dictionary chứa các cặp key-value để thay thế</param>
        /// <returns>Nội dung template sau khi đã thay thế</returns>
        public static string ReplaceTemplateValues(string template, Dictionary<string, string> replacements)
        {
            foreach (var replacement in replacements)
            {
                template = template.Replace($"{{{{{replacement.Key}}}}}", replacement.Value);
            }

            // Xóa các placeholder còn lại (nếu có)
            template = Regex.Replace(template, @"\{\{[^\}]+\}\}", string.Empty);

            return template;
        }

        /// <summary>
        /// Thay thế một section trong template với nội dung được cung cấp
        /// </summary>
        /// <param name="template">Nội dung template</param>
        /// <param name="sectionName">Tên của section cần thay thế</param>
        /// <param name="content">Nội dung thay thế</param>
        /// <returns>Nội dung template sau khi đã thay thế section</returns>
        public static string ReplaceSection(string template, string sectionName, string content)
        {
            return template.Replace($"{{{{{sectionName}}}}}", content);
        }

        /// <summary>
        /// Xóa một section trong template nếu không cần thiết
        /// </summary>
        /// <param name="template">Nội dung template</param>
        /// <param name="sectionName">Tên của section cần xóa</param>
        /// <returns>Nội dung template sau khi đã xóa section</returns>
        public static string RemoveSection(string template, string sectionName)
        {
            return template.Replace($"{{{{{sectionName}}}}}", string.Empty);
        }
    }
}