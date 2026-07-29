using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeCaptureService.Domain;

namespace TradeCaptureService.ReferenceData
{
    public sealed record InstrumentReferenceData
    {
        public Guid InstrumentId { get; init; }

        public string Symbol { get; init; } = null!;

        public AssetClass AssetClass { get; init; }

        public bool IsTradable { get; init; }
    }
}
