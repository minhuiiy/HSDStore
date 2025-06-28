using System;
using System.Collections.Generic;

namespace CPTStore.Models.Generated;

public partial class Order
{
    public int Id { get; set; }

    public string? UserId { get; set; }

    public string OrderNumber { get; set; } = null!;

    public string OrderReference { get; set; } = null!;

    public DateTime OrderDate { get; set; }

    public int Status { get; set; }

    public int PaymentMethod { get; set; }

    public int PaymentStatus { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal DiscountAmount { get; set; }

    public string? DiscountCode { get; set; }

    public string CustomerName { get; set; } = null!;

    public string PhoneNumber { get; set; } = null!;

    public string Address { get; set; } = null!;

    public string? City { get; set; }

    public string? District { get; set; }

    public string? Ward { get; set; }

    public string? PostalCode { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? Notes { get; set; }

    public string? TransactionId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public decimal ShippingFee { get; set; }
}
