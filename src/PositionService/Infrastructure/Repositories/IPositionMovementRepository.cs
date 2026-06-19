using PositionService.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PositionService.Infrastructure.Repositories
{
    public interface IPositionMovementRepository : IGenericRepository<PositionMovement>
    {
    }
}
