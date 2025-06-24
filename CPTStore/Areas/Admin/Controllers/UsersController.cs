using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using CPTStore.Models;
using System;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CPTStore.Areas.Admin.Controllers
{
    public class UsersController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager) : AdminControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager = userManager;
        private readonly RoleManager<IdentityRole> _roleManager = roleManager;

        // GET: Admin/Users
        public async Task<IActionResult> Index(string searchTerm, int page = 1, int pageSize = 10)
        {
            var query = _userManager.Users.AsQueryable();

            // Áp dụng tìm kiếm nếu có
            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(u => (u.UserName != null && u.UserName.Contains(searchTerm)) || 
                                        (u.Email != null && u.Email.Contains(searchTerm)) ||
                                        (u.PhoneNumber != null && u.PhoneNumber.Contains(searchTerm)));
            }

            // Tính toán phân trang
            var totalUsers = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalUsers / (double)pageSize);
            var users = await query
                .OrderBy(u => u.UserName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Lấy vai trò cho mỗi người dùng
            var userViewModels = new List<UserViewModel>();
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userViewModels.Add(new UserViewModel
                {
                    User = user,
                    Roles = roles
                });
            }

            ViewBag.SearchTerm = searchTerm;
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = totalPages;

            return View(userViewModels);
        }

        // GET: Admin/Users/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var roles = await _userManager.GetRolesAsync(user);
            var viewModel = new UserViewModel
            {
                User = user,
                Roles = roles
            };

            return View(viewModel);
        }

        // GET: Admin/Users/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var userRoles = await _userManager.GetRolesAsync(user);
            var allRoles = await _roleManager.Roles.ToListAsync();

            var viewModel = new UserEditViewModel
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber ?? string.Empty,
                FirstName = user.FirstName ?? string.Empty,
                LastName = user.LastName ?? string.Empty,
                IsActive = user.IsActive,
                UserRoles = userRoles,
                AllRoles = allRoles.Select(r => r.Name).ToList(),
                SelectedRoles = userRoles.ToList(),
                AllRolesSelectList = allRoles.Select(r => new SelectListItem
                {
                    Text = r.Name,
                    Value = r.Name,
                    Selected = r.Name != null && userRoles.Contains(r.Name)
                }).ToList()
            };

            return View(viewModel);
        }

        // POST: Admin/Users/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, UserEditViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    return NotFound();
                }

                user.UserName = model.UserName;
                user.Email = model.Email;
                user.PhoneNumber = model.PhoneNumber;
                user.FirstName = model.FirstName;
                user.LastName = model.LastName;
                user.IsActive = model.IsActive;

                var result = await _userManager.UpdateAsync(user);
                if (result.Succeeded)
                {
                    // Cập nhật vai trò
                    var userRoles = await _userManager.GetRolesAsync(user);
                    
                    // Xóa các vai trò hiện tại
                    await _userManager.RemoveFromRolesAsync(user, userRoles);
                    
                    // Thêm các vai trò mới
                    if (model.SelectedRoles != null && model.SelectedRoles.Count > 0)
                    {
                        await _userManager.AddToRolesAsync(user, model.SelectedRoles);
                    }

                    return RedirectToAction(nameof(Index));
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            // Nếu có lỗi, chuẩn bị lại dữ liệu cho view
            var allRoles = await _roleManager.Roles.ToListAsync();
            model.AllRoles = allRoles.Select(r => r.Name).ToList();
            model.AllRolesSelectList = allRoles.Select(r => new SelectListItem
            {
                Text = r.Name,
                Value = r.Name,
                Selected = model.SelectedRoles != null && r.Name != null && model.SelectedRoles.Contains(r.Name)
            }).ToList();

            return View(model);
        }
    }

    public class UserViewModel
    {
        public required ApplicationUser User { get; set; }
        public required IList<string> Roles { get; set; }
    }

    public class UserEditViewModel
    {
        public required string Id { get; set; }
        public required string UserName { get; set; }
        public required string Email { get; set; }
        public required string PhoneNumber { get; set; }
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
        public bool IsActive { get; set; }
        public required IList<string> UserRoles { get; set; }
        public required List<string?> AllRoles { get; set; }
        public required List<string> SelectedRoles { get; set; }
        public required IEnumerable<SelectListItem> AllRolesSelectList { get; set; }
    }
}