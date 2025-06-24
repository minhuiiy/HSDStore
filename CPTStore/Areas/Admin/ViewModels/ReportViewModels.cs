using System;
using System.Collections.Generic;

namespace CPTStore.Areas.Admin.ViewModels
{
    public class SalesReportViewModel
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal AverageOrderValue { get; set; }
        public required List<DailySalesData> DailySales { get; set; }
    }

    public class DailySalesData
    {
        public DateTime Date { get; set; }
        public int OrderCount { get; set; }
        public decimal Revenue { get; set; }
    }

    public class ProductReportViewModel
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public required IEnumerable<TopSellingProductData> TopSellingProducts { get; set; }
    }

    public class TopSellingProductData
    {
        public int ProductId { get; set; }
        public required string ProductName { get; set; }
        public int QuantitySold { get; set; }
        public decimal Revenue { get; set; }
    }

    public class CustomerReportViewModel
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public required IEnumerable<TopCustomerData> TopCustomers { get; set; }
    }

    public class TopCustomerData
    {
        public required string UserId { get; set; }
        public required string CustomerName { get; set; }
        public required string Email { get; set; }
        public int OrderCount { get; set; }
        public decimal TotalSpent { get; set; }
    }
}