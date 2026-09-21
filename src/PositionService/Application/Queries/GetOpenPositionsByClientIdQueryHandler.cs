using PositionService.Application.Positions;
using PositionService.Infrastructure.UnitOfWork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PositionService.Application.Queries
{
    public sealed class GetOpenPositionsByClientIdQueryHandler : IQueryHandler<GetOpenPositionsByClientIdQuery, IReadOnlyList<PositionSummaryReadModel>>
    {
        private readonly IUnitOfWork unitOfWork;

        public GetOpenPositionsByClientIdQueryHandler(IUnitOfWork unitOfWork)
        {
            this.unitOfWork = unitOfWork;
        }

        public async Task<IReadOnlyList<PositionSummaryReadModel>> HandleAsync(
            GetOpenPositionsByClientIdQuery query, CancellationToken cancellationToken = default)
        {
            var positions = await unitOfWork.Positions.GetOpenPositionsByClientIdAsync(query.ClientId, cancellationToken);

            return positions.Select(p => new PositionSummaryReadModel
            (
                InstrumentId: p.InstrumentId,
                Symbol: p.Symbol,
                AssetClass: p.AssetClass,
                NetQuantity: p.NetQuantity,
                AveragePrice: p.AveragePrice,
                PnlCurrency: p.PnlCurrency,
                RealisedPnl: p.RealisedPnl,
                UnrealisedPnl: p.UnrealisedPnl
            )).ToList();
        }
    }
}
