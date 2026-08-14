using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingApp.Contracts.Shared;

namespace PositionService.Domain
{
    public class PositionMovement
    {
        public Guid Id { get; set; }

        public Guid PositionId { get; set; }

        public Guid TradeId { get; set; }

        public Guid OrderId { get; set; }

        public Guid InstrumentId { get; set; }

        public string ClientId { get; set; } = null!;

        public string Symbol { get; set; } = null!;

        public AssetClass AssetClass { get; set; }

        public OrderSide Side { get; set; }

        public decimal Quantity { get; set; }

        public decimal SignedQuantity { get; set; }

        public decimal Price { get; set; }

        public decimal PreviousNetQuantity { get; set; }

        public decimal PreviousAveragePrice { get; set; }

        public decimal NewNetQuantity { get; set; }

        public decimal NewAveragePrice { get; set; }

        public CurrencyCode PnlCurrency { get; set; }

        public decimal PreviousRealisedPnl { get; set; }

        public decimal RealisedPnlChange { get; set; }

        public decimal NewRealisedPnl { get; set; }

        public string CorrelationId { get; set; } = null!;

        public DateTimeOffset CreatedAt { get; set; }

        public Position Position { get; set; } = null!;
    }
}
