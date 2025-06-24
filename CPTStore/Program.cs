using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CPTStore.Data;
using CPTStore.Models;
using CPTStore.Services;
using CPTStore.Services.Interfaces;
using CPTStore.Repositories;
using CPTStore.Repositories.Interfaces;
using CPTStore.Extensions;
using CPTStore.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Cấu hình EF + Identity
if (builder.Environment.IsDevelopment())
{
    // Trong môi trường phát triển, sử dụng ghi log chi tiết
    builder.Services.AddEfLogging(LogLevel.Information);
}
else
{
    // Trong môi trường sản xuất, không ghi log chi tiết
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
}

// Cấu hình Identity sẽ được thêm ở phía dưới

// Đăng ký các Repository
builder.Services.AddScoped<CPTStore.Repositories.Interfaces.ICartItemRepository, CPTStore.Repositories.CartItemRepository>();
builder.Services.AddScoped<CPTStore.Repositories.Interfaces.IOrderRepository, CPTStore.Repositories.OrderRepository>();

// Đăng ký các Service
builder.Services.AddScoped<CPTStore.Services.Interfaces.ICartService, CPTStore.Services.CartService>();

// Hoàn tất cấu hình Identity
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
    
// Đăng ký AutoMapper
builder.Services.AddAutoMapper(typeof(CPTStore.Mappings.MappingProfile));

// Cấu hình xác thực Cookie
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
    });

builder.Services.AddControllersWithViews(options => {
    options.Conventions.Add(new CPTStore.Areas.Admin.AdminAreaRegistration());
});

// Cấu hình session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Đăng ký các dịch vụ
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IOrderService, OrderServiceV2>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IDiscountService, DiscountService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IPdfService, PdfService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IRecommendationService, RecommendationService>();
builder.Services.AddScoped<IChatService, ChatService>();

// Nếu có dùng SignalR
builder.Services.AddSignalR();

var app = builder.Build();

// Middleware pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    // Trong môi trường phát triển, sử dụng middleware xử lý ngoại lệ tùy chỉnh
    app.UseMiddleware<CPTStore.Middleware.ExceptionHandlingMiddleware>();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Quan trọng: Xác thực và phân quyền
app.UseAuthentication();
app.UseAuthorization();

// Sử dụng Session
app.UseSession();

// Định tuyến MVC
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Cấu hình SignalR Hub
app.MapHub<ChatHub>("/chatHub");

// Khởi tạo dữ liệu ban đầu
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>() as ILogger;
    
    try
    {
        logger.LogInformation("Bắt đầu khởi tạo dữ liệu ban đầu");
        await CPTStore.Data.DbInitializer.InitializeAsync(app.Services, logger);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Lỗi khi khởi tạo dữ liệu ban đầu");
    }
}

app.Run();
