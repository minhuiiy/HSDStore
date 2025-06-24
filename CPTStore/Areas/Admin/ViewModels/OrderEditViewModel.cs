using CPTStore.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;

namespace CPTStore.Areas.Admin.ViewModels
{
    public class OrderEditViewModel
    {
        public required Order Order { get; set; }
        public required IEnumerable<SelectListItem> OrderStatuses { get; set; }
    }
}