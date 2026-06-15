using NServiceBus;

namespace Contracts;

public class OrderPlaced : IEvent
{
    public Guid OrderId { get; set; }
    public string CustomerEmail { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class PaymentCompleted : IEvent
{
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaidAtUtc { get; set; }
}

public class CompletePayment : ICommand
{
    public Guid OrderId { get; set; }
}

public class ShipOrder : ICommand
{
    public Guid OrderId { get; set; }
}

