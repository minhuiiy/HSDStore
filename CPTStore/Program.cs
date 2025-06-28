using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CPTStore.Data;
using CPTStore.Models;
using CPTStore.Models.Generated;
using CPTStore.Services;
using CPTStore.Services.Interfaces;
using CPTStore.Repositories;
using CPTStore.Hubs;
using CPTStore.Mappings;
using CPTStore.Middleware;
using CPTStore.Filters;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace CPTStore
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

// Cấu hình EF + Identity
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Đăng ký CptstoreDbContext (generated context)
builder.Services.AddDbContext<CPTStore.Models.Generated.CptstoreDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options => {
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
    options.User.RequireUniqueEmail = true;
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// Cấu hình xác thực Cookie
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
    });

builder.Services.AddControllersWithViews(options =>
{
    options.Conventions.Add(new CPTStore.Areas.Admin.AdminAreaRegistration());
    options.Filters.Add<CPTStore.Filters.CartItemCountFilter>();
});

// Cấu hình session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2); // Tăng thời gian timeout lên 2 giờ
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    // Đảm bảo session ID được tạo và lưu trữ ngay khi session được tạo
    options.Cookie.Name = "CPTStore.Session";
});

// Cấu hình AutoMapper
builder.Services.AddAutoMapper(typeof(MappingProfile));

// Đăng ký các repository
builder.Services.AddScoped<CPTStore.Repositories.Interfaces.ICartItemRepository, CPTStore.Repositories.CartItemRepository>();
builder.Services.AddScoped<CPTStore.Repositories.Interfaces.IOrderRepository, CPTStore.Repositories.OrderRepository>();

// Đăng ký các dịch vụ
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IDiscountService, DiscountService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IPdfService, PdfService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IRecommendationService, RecommendationService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IMembershipService, MembershipService>();
builder.Services.AddScoped<ISavedDiscountService, SavedDiscountService>();
builder.Services.AddScoped<ISettingsService, SettingsService>();

// Đăng ký dịch vụ nền tự động xác nhận đơn hàng
builder.Services.AddHostedService<CPTStore.Services.BackgroundServices.OrderConfirmationService>();

// Nếu có dùng SignalR
builder.Services.AddSignalR();

var app = builder.Build();

// Middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    // Sử dụng middleware xử lý ngoại lệ tùy chỉnh
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Đăng ký middleware xử lý ngoại lệ toàn cục
app.UseMiddleware<CPTStore.Middleware.ExceptionHandlingMiddleware>();

// Quan trọng: Xác thực và phân quyền
app.UseAuthentication();
app.UseAuthorization();

// Sử dụng Session
app.UseSession();

// Đảm bảo session ID được tạo và lưu trữ ngay lập tức
app.UseSessionPersistence();

// Định tuyến Area
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

// Định tuyến MVC
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Cấu hình SignalR Hub
app.MapHub<ChatHub>("/chatHub");

// Khởi tạo dữ liệu
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    
    try
    {
        logger.LogInformation("Đang khởi tạo dữ liệu...");
        await DbInitializer.InitializeAsync(services, logger);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Lỗi khi khởi tạo dữ liệu");
    }
}

app.Run();
        }
    }
}
