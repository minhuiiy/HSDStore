using Microsoft.AspNetCore.Razor.TagHelpers;
using CPTStore.Models;

namespace CPTStore.TagHelpers
{
    /// <summary>
    /// Tag helper để hiển thị mã giảm giá với định dạng nhất quán
    /// </summary>
    [HtmlTargetElement("discount-badge")]
    public class DiscountTagHelper : TagHelper
    {
        /// <summary>
        /// Mã giảm giá cần hiển thị
        /// </summary>
        [HtmlAttributeName("code")]
        public required string Code { get; set; }

        /// <summary>
        /// Loại giảm giá (Percentage hoặc FixedAmount)
        /// </summary>
        [HtmlAttributeName("type")]
        public DiscountType DiscountType { get; set; }

        /// <summary>
        /// Giá trị giảm giá
        /// </summary>
        [HtmlAttributeName("value")]
        public decimal Value { get; set; }

        /// <summary>
        /// Trạng thái của mã giảm giá (đã sử dụng hay chưa)
        /// </summary>
        [HtmlAttributeName("is-used")]
        public bool IsUsed { get; set; }

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            output.TagName = "div";
            output.Attributes.SetAttribute("class", "discount-badge");

            var statusClass = IsUsed ? "discount-used" : "discount-active";
            var statusText = IsUsed ? "Đã sử dụng" : "Có thể sử dụng";

            var discountValue = DiscountType == DiscountType.Percentage
                ? $"{Value}%"
                : $"{Value:#,##0} VNĐ";

            var content = $@"<div class=""card {statusClass}"">
                <div class=""card-header"">
                    <span class=""badge bg-primary"">{Code}</span>
                    <span class=""badge {(IsUsed ? "bg-secondary" : "bg-success")} float-end"">{statusText}</span>
                </div>
                <div class=""card-body"">
                    <h5 class=""card-title"">{discountValue}</h5>
                    <slot></slot>
                </div>
            </div>";

            output.Content.SetHtmlContent(content);
        }
    }
}