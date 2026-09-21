namespace TradingGateway.Api.ClientModels
{
    public sealed record PositionSummaryResponse(
        Guid InstrumentId,
        string Symbol,
        string AssetClass,
        decimal NetQuantity,
        decimal AveragePrice,
        string PnlCurrency,
        decimal RealisedPnl,
        decimal UnrealisedPnl,
        decimal TotalPnl
    );
}
