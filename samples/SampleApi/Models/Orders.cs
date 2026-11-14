using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SampleApi.Models;

public enum OrderStatus { Pending, Paid, Shipped }

public class Address
{
    [Required] public string Street { get; set; } = "";
    public string City { get; set; } = "";
    public string? PostCode { get; set; }
}

public class OrderLine
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
}

/// <summary>Payload used to place a new order.</summary>
public class CreateOrderRequest
{
    /// <summary>The customer placing the order.</summary>
    public required Guid CustomerId { get; set; }
    public List<OrderLine> Lines { get; set; } = new();
    public Address ShippingAddress { get; set; } = new();
    [JsonPropertyName("note")] public string? CustomerNote { get; set; }
}

public record UpdateOrderStatusRequest(OrderStatus Status, string? Reason = null);

public record OrderDto(Guid Id, OrderStatus Status, decimal Total);

public record PagedQuery(int Page = 1, int PageSize = 20);
