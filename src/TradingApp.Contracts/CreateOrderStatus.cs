namespace Contracts;

public sealed class CreateOrderStatus
{
    public Guid OrderId { get; set; }
    public OrderStatus OrderStatus { get; set; }
}