using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace CPTStore.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public abstract class AdminControllerBase : Controller
    {
        // Các phương thức và thuộc tính chung cho tất cả các controller trong khu vực Admin
    }
}