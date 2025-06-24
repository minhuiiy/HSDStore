using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;

namespace CPTStore.Extensions
{
    /// <summary>
    /// Các phương thức mở rộng cho Entity Framework
    /// </summary>
    public static class EntityFrameworkExtensions
    {
        /// <summary>
        /// Cấu hình ghi log cho DbContext
        /// </summary>
        /// <param name="services">IServiceCollection</param>
        /// <param name="logLevel">Mức độ log (mặc định là Information)</param>
        /// <returns>IServiceCollection</returns>
        public static IServiceCollection AddEfLogging(this IServiceCollection services, LogLevel logLevel = LogLevel.Information)
        {
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.AddDebug();
            });

            services.AddDbContext<Data.ApplicationDbContext>((provider, options) =>
            {
                var connectionString = provider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()
                    .GetConnectionString("DefaultConnection");

                options.UseSqlServer(connectionString);
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
                options.UseLoggerFactory(GetLoggerFactory(logLevel));
            });

            return services;
        }

        /// <summary>
        /// Tạo LoggerFactory cho Entity Framework
        /// </summary>
        /// <param name="logLevel">Mức độ log</param>
        /// <returns>ILoggerFactory</returns>
        private static ILoggerFactory GetLoggerFactory(LogLevel logLevel)
        {
            return LoggerFactory.Create(builder =>
            {
                builder
                    .AddFilter((category, level) =>
                        category == DbLoggerCategory.Database.Command.Name && level >= logLevel)
                    .AddConsole();
            });
        }
    }
}