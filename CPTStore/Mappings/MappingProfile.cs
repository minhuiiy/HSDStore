using AutoMapper;
using CPTStore.Models;
using CPTStore.ViewModels;
using CPTStore.Areas.Admin.ViewModels;
using System;

namespace CPTStore.Mappings
{
    /// <summary>
    /// Lớp cấu hình AutoMapper để ánh xạ giữa các đối tượng trong ứng dụng
    /// </summary>
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // Ánh xạ từ CartItem sang CartItemViewModel
            CreateMap<CartItem, CartItemViewModel>()
                .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.Product != null ? src.Product.Name : "Sản phẩm không xác định"))
                .ForMember(dest => dest.ImageUrl, opt => opt.MapFrom(src => src.Product != null ? src.Product.ImageUrl : "/images/no-image.png"))
                .ForMember(dest => dest.Price, opt => opt.MapFrom(src => src.Product != null ? src.Product.Price : src.UnitPrice))
                .ForMember(dest => dest.TotalPrice, opt => opt.MapFrom(src => (src.Product != null ? src.Product.Price : src.UnitPrice) * src.Quantity));

            // Ánh xạ từ Product sang ProductViewModel
            CreateMap<Product, ProductViewModel>()
                .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category != null ? src.Category.Name : "Không có danh mục"));
            CreateMap<Product, ProductViewModel>().ReverseMap();

            // Ánh xạ từ Category sang CategoryViewModel
            CreateMap<Category, CategoryViewModel>();
            CreateMap<Category, CategoryViewModel>().ReverseMap();
            CreateMap<OrderConfirmationSettings, OrderConfirmationSettingsViewModel>().ReverseMap();

            // Ánh xạ từ Order sang OrderViewModel
            CreateMap<Order, OrderViewModel>()
                .ForMember(dest => dest.OrderStatusName, opt => opt.MapFrom(src => src.Status.ToString()))
                .ForMember(dest => dest.PaymentMethodName, opt => opt.MapFrom(src => src.PaymentMethod.ToString()));

            // Ánh xạ từ OrderItem sang OrderItemViewModel
            CreateMap<OrderItem, OrderItemViewModel>()
                .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.Product != null ? src.Product.Name : "Sản phẩm không xác định"))
                .ForMember(dest => dest.ImageUrl, opt => opt.MapFrom(src => src.Product != null ? src.Product.ImageUrl : "/images/no-image.png"));
        }
    }
}