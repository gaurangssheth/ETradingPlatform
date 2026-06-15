namespace TradingGateway.Api.Models;

public class SubmitOrderRequest
{
    public string? ClientId { get; set; }

    public string? Symbol { get; set; }

    public string? Side { get; set; }

    public decimal? Quantity { get; set; }

    public string? OrderType { get; set; }
}
