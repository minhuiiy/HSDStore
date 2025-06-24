using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using CPTStore.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CPTStore.Data
{
    public static class DbInitializer
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider, ILogger logger)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var services = scope.ServiceProvider;
                var context = services.GetRequiredService<ApplicationDbContext>();
                var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
                var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

                // Đảm bảo cơ sở dữ liệu đã được tạo
                await context.Database.MigrateAsync();

                // Kiểm tra và tạo vai trò Admin nếu chưa tồn tại
                if (!await roleManager.RoleExistsAsync("Admin"))
                {
                    await roleManager.CreateAsync(new IdentityRole("Admin"));
                    logger.LogInformation("Đã tạo vai trò Admin");
                }

                // Kiểm tra và tạo vai trò User nếu chưa tồn tại
                if (!await roleManager.RoleExistsAsync("User"))
                {
                    await roleManager.CreateAsync(new IdentityRole("User"));
                    logger.LogInformation("Đã tạo vai trò User");
                }

                // Kiểm tra và tạo tài khoản admin mặc định nếu chưa tồn tại
                var adminEmail = "admin@cptstore.com";
                var adminUser = await userManager.FindByEmailAsync(adminEmail);

                if (adminUser == null)
                {
                    adminUser = new ApplicationUser
                    {
                        UserName = adminEmail,
                        Email = adminEmail,
                        FirstName = "Admin",
                        LastName = "CPTStore",
                        EmailConfirmed = true,
                        PhoneNumber = "0123456789",
                        IsAdmin = true,
                        IsActive = true,
                        CreatedAt = DateTime.Now
                    };

                    var result = await userManager.CreateAsync(adminUser, "Admin@123");

                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(adminUser, "Admin");
                        logger.LogInformation("Đã tạo tài khoản admin mặc định");
                    }
                    else
                    {
                        var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                        logger.LogError($"Không thể tạo tài khoản admin: {errors}");
                    }
                }
                else
                {
                    // Đảm bảo admin có vai trò Admin
                    if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
                    {
                        await userManager.AddToRoleAsync(adminUser, "Admin");
                        logger.LogInformation("Đã thêm vai trò Admin cho tài khoản admin hiện có");
                    }
                }

                logger.LogInformation("Khởi tạo dữ liệu hoàn tất");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Lỗi khi khởi tạo dữ liệu");
                throw;
            }
        }
    }
}