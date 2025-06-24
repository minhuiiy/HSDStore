using Microsoft.AspNetCore.Mvc.ApplicationModels;
using System;
using System.Linq;

namespace CPTStore.Areas.Admin
{
    public class AdminAreaRegistration : IApplicationModelConvention
    {
        public void Apply(ApplicationModel application)
        {
            foreach (var controller in application.Controllers)
            {
                if (controller.Attributes.Any(a =>
                        a is Microsoft.AspNetCore.Mvc.AreaAttribute areaAttr &&
                        areaAttr.RouteValue.Equals("Admin", StringComparison.OrdinalIgnoreCase)))
                {
                    controller.RouteValues["area"] = "Admin";
                }
            }
        }
    }
}