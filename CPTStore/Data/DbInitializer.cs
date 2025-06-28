using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using CPTStore.Models;
using System;
using System.Collections.Generic;
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

                // Thêm dữ liệu mẫu cho mã giảm giá thành viên
                if (!context.MembershipDiscounts.Any())
                {
                    var membershipDiscounts = new List<MembershipDiscount>
                    {
                        new MembershipDiscount
                        {
                            MembershipLevel = MembershipLevel.Silver,
                            Code = "SILVER10",
                            Description = "Giảm giá 10% cho thành viên Silver",
                            DiscountType = DiscountType.Percentage,
                            Value = 10,
                            MinimumOrderAmount = 0,
                            MaximumDiscountAmount = 100000,
                            IsActive = true,
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now
                        },
                        new MembershipDiscount
                        {
                            MembershipLevel = MembershipLevel.Gold,
                            Code = "GOLD15",
                            Description = "Giảm giá 15% cho thành viên Gold",
                            DiscountType = DiscountType.Percentage,
                            Value = 15,
                            MinimumOrderAmount = 0,
                            MaximumDiscountAmount = 200000,
                            IsActive = true,
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now
                        },
                        new MembershipDiscount
                        {
                            MembershipLevel = MembershipLevel.Diamond,
                            Code = "DIAMOND20",
                            Description = "Giảm giá 20% cho thành viên Diamond",
                            DiscountType = DiscountType.Percentage,
                            Value = 20,
                            MinimumOrderAmount = 0,
                            MaximumDiscountAmount = 500000,
                            IsActive = true,
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now
                        },
                        new MembershipDiscount
                        {
                            MembershipLevel = MembershipLevel.Diamond,
                            Code = "DIAMONDFIXED50K",
                            Description = "Giảm giá cố định 50.000đ cho thành viên Diamond",
                            DiscountType = DiscountType.FixedAmount,
                            Value = 50000,
                            MinimumOrderAmount = 200000,
                            MaximumDiscountAmount = 50000,
                            IsActive = true,
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now
                        }
                    };

                    context.MembershipDiscounts.AddRange(membershipDiscounts);
                    await context.SaveChangesAsync();
                    logger.LogInformation("Đã thêm dữ liệu mẫu cho mã giảm giá thành viên");
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