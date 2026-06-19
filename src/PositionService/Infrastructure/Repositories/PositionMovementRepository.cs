using PositionService.Domain;
using PositionService.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PositionService.Infrastructure.Repositories
{
    public class PositionMovementRepository : GenericRepository<PositionMovement>, IPositionMovementRepository
    {
        private readonly ILogger<PositionMovementRepository> _logger;

        public PositionMovementRepository(PositionDbContext positionDbContext, ILogger<PositionMovementRepository> logger) : base(positionDbContext)       {
            this._logger = logger;
        }
    }
}
