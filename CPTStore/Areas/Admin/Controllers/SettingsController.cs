using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using System.Linq;
using CPTStore.Services.Interfaces;
using CPTStore.Models;
using CPTStore.Areas.Admin.ViewModels;
using AutoMapper;

namespace CPTStore.Areas.Admin.Controllers
{
    public class SettingsController(IWebHostEnvironment webHostEnvironment, IConfiguration configuration, ISettingsService settingsService, IMapper mapper) : AdminControllerBase
    {
        private readonly IWebHostEnvironment _webHostEnvironment = webHostEnvironment;
        private readonly IConfiguration _configuration = configuration;
        private readonly ISettingsService _settingsService = settingsService;
        private readonly IMapper _mapper = mapper;

        // GET: Admin/Settings
        public IActionResult Index()
        {
            var settings = new SettingsViewModel
            {
                // Cài đặt chung
                StoreName = _configuration["StoreSettings:StoreName"] ?? "CPT Store",
                StoreAddress = _configuration["StoreSettings:StoreAddress"] ?? string.Empty,
                StorePhone = _configuration["StoreSettings:StorePhone"] ?? string.Empty,
                StoreEmail = _configuration["StoreSettings:StoreEmail"] ?? string.Empty,
                
                // Cài đặt email
                SmtpServer = _configuration["EmailSettings:SmtpServer"] ?? string.Empty,
                SmtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587"),
                SmtpUsername = _configuration["EmailSettings:Username"] ?? string.Empty,
                SmtpPassword = _configuration["EmailSettings:Password"] ?? string.Empty,
                SmtpEnableSsl = bool.Parse(_configuration["EmailSettings:EnableSsl"] ?? "true")
            };

            return View(settings);
        }

        // POST: Admin/Settings/SaveGeneral
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveGeneral(SettingsViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Cập nhật cấu hình trong appsettings.json
                    var appSettingsPath = Path.Combine(_webHostEnvironment.ContentRootPath, "appsettings.json");
                    var json = await System.IO.File.ReadAllTextAsync(appSettingsPath);
                    var jsonObj = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(json);

                    // Cập nhật cài đặt cửa hàng
                    var storeSettings = new Dictionary<string, string>
                    {
                        { "Name", model.StoreName ?? string.Empty },
                        { "Email", model.StoreEmail ?? string.Empty },
                        { "Phone", model.StorePhone ?? string.Empty },
                        { "Address", model.StoreAddress ?? string.Empty },
                        { "CurrencySymbol", model.CurrencySymbol ?? string.Empty },
                        { "DefaultPageSize", model.DefaultPageSize.ToString() },
                        { "EnableReviews", model.EnableReviews.ToString() },
                        { "EnableWishlist", model.EnableWishlist.ToString() },
                        { "EnableCompare", model.EnableCompare.ToString() }
                    };

                    if (jsonObj != null)
                    {
                        jsonObj["StoreSettings"] = storeSettings;
                    }

                    var updatedJson = Newtonsoft.Json.JsonConvert.SerializeObject(jsonObj, Newtonsoft.Json.Formatting.Indented);
                    await System.IO.File.WriteAllTextAsync(appSettingsPath, updatedJson);

                    TempData["SuccessMessage"] = "Cài đặt chung đã được lưu thành công.";
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(string.Empty, $"Lỗi khi lưu cài đặt: {ex.Message}");
                    return View("Index", model);
                }
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: Admin/Settings/SaveEmail
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveEmail(SettingsViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Cập nhật cấu hình trong appsettings.json
                    var appSettingsPath = Path.Combine(_webHostEnvironment.ContentRootPath, "appsettings.json");
                    var json = await System.IO.File.ReadAllTextAsync(appSettingsPath);
                    var jsonObj = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(json);

                    // Cập nhật cài đặt email
                    var emailSettings = new Dictionary<string, string>
                    {
                        { "SmtpServer", model.SmtpServer ?? string.Empty },
                        { "SmtpPort", model.SmtpPort.ToString() },
                        { "Username", model.SmtpUsername ?? string.Empty },
                        { "Password", model.SmtpPassword ?? string.Empty },
                        { "EnableSsl", model.SmtpEnableSsl.ToString() }
                    };

                    if (jsonObj != null)
                    {
                        jsonObj["EmailSettings"] = emailSettings;
                    }

                    var updatedJson = Newtonsoft.Json.JsonConvert.SerializeObject(jsonObj, Newtonsoft.Json.Formatting.Indented);
                    await System.IO.File.WriteAllTextAsync(appSettingsPath, updatedJson);

                    TempData["SuccessMessage"] = "Cài đặt email đã được lưu thành công.";
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(string.Empty, $"Lỗi khi lưu cài đặt: {ex.Message}");
                    return View("Index", model);
                }
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Admin/Settings/Backup
        public IActionResult Backup()
        {
            var backupDir = Path.Combine(_webHostEnvironment.ContentRootPath, "Backups");
            var backupFiles = Directory.Exists(backupDir)
                ? Directory.GetFiles(backupDir, "*.bak")
                    .OrderByDescending(f => new FileInfo(f).CreationTime)
                    .ToList()
                : new List<string>();

            ViewBag.BackupFiles = backupFiles;
            return View();
        }

        // POST: Admin/Settings/CreateBackup
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateBackup()
        {
            try
            {
                // Tạo tên file backup với timestamp
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var backupFileName = $"backup_{timestamp}.bak";
                var backupPath = Path.Combine(_webHostEnvironment.ContentRootPath, "Backups", backupFileName);

                // Đảm bảo thư mục Backups tồn tại
                Directory.CreateDirectory(Path.Combine(_webHostEnvironment.ContentRootPath, "Backups"));

                // Giả lập tạo backup (trong thực tế sẽ gọi SQL Server để tạo backup)
                // Ví dụ: await _dbContext.Database.ExecuteSqlRawAsync($"BACKUP DATABASE [CPTStore] TO DISK = '{backupPath}'");

                // Tạo file giả lập
                System.IO.File.WriteAllText(backupPath, $"Backup created at {DateTime.Now}");

                TempData["SuccessMessage"] = $"Đã tạo bản sao lưu thành công: {backupFileName}";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi tạo bản sao lưu: {ex.Message}";
            }

            return RedirectToAction(nameof(Backup));
        }

        // GET: Admin/Settings/OrderConfirmation
        public async Task<IActionResult> OrderConfirmation()
        {
            var settings = await _settingsService.GetOrderConfirmationSettingsAsync();
            var viewModel = _mapper.Map<OrderConfirmationSettingsViewModel>(settings);
            return View(viewModel);
        }

        // POST: Admin/Settings/OrderConfirmation
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OrderConfirmation(OrderConfirmationSettingsViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var settings = _mapper.Map<OrderConfirmationSettings>(viewModel);
                    await _settingsService.SaveOrderConfirmationSettingsAsync(settings);
                    TempData["SuccessMessage"] = "Cài đặt tự động xác nhận đơn hàng đã được lưu thành công.";
                    return RedirectToAction(nameof(OrderConfirmation));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(string.Empty, $"Lỗi khi lưu cài đặt: {ex.Message}");
                }
            }

            return View(viewModel);
        }

        public IActionResult Restore()
        {
            var backupFiles = Directory.GetFiles(Path.Combine(_webHostEnvironment.ContentRootPath, "Backups"), "*.bak")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.CreationTime)
                .Select(f => f.Name)
                .ToList();

            return View(backupFiles);
        }

        // POST: Admin/Settings/RestoreBackup
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RestoreBackup(string backupFile)
        {
            try
            {
                if (string.IsNullOrEmpty(backupFile))
                {
                    throw new ArgumentException("Vui lòng chọn file backup để khôi phục.");
                }

                var backupDir = Path.Combine(_webHostEnvironment.ContentRootPath, "Backups");
                var backupPath = Path.Combine(backupDir, backupFile);

                if (!System.IO.File.Exists(backupPath))
                {
                    throw new FileNotFoundException("File backup không tồn tại.");
                }

                // Giả lập khôi phục backup (trong thực tế sẽ gọi SQL Server để khôi phục)
                // Ví dụ: await _dbContext.Database.ExecuteSqlRawAsync($"RESTORE DATABASE [CPTStore] FROM DISK = '{backupPath}'");

                TempData["SuccessMessage"] = $"Đã khôi phục dữ liệu từ bản sao lưu: {backupFile}";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi khôi phục dữ liệu: {ex.Message}";
            }

            return RedirectToAction(nameof(Restore));
        }
    }

    public class SettingsViewModel
    {
        // Cài đặt chung
        public string? StoreName { get; set; }
        public string? StoreEmail { get; set; }
        public string? StorePhone { get; set; }
        public string? StoreAddress { get; set; }
        public string? CurrencySymbol { get; set; }
        public int DefaultPageSize { get; set; }
        public bool EnableReviews { get; set; }
        public bool EnableWishlist { get; set; }
        public bool EnableCompare { get; set; }

        // Cài đặt email
        public string? SmtpServer { get; set; }
        public int SmtpPort { get; set; }
        public string? SmtpUsername { get; set; }
        public string? SmtpPassword { get; set; }
        public bool SmtpEnableSsl { get; set; }
    }
}