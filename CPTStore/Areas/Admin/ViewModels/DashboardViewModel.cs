using System;
using System.Collections.Generic;
using CPTStore.Models;

namespace CPTStore.Areas.Admin.ViewModels
{
    public class DashboardViewModel
    {
        public int TotalOrders { get; set; }
        public int TotalProducts { get; set; }
        public int TotalCategories { get; set; }
        public int TotalUsers { get; set; }
        
        public IEnumerable<Order> RecentOrders { get; set; } = new List<Order>();
        public IEnumerable<Product> TopSellingProducts { get; set; } = new List<Product>();
        
        public Dictionary<string, decimal> MonthlyRevenue { get; set; } = new Dictionary<string, decimal>();
    }
}