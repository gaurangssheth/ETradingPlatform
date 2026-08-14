using Microsoft.EntityFrameworkCore;
using PositionService.Domain;
using PositionService.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PositionService.Tests.Infrastructure.Persistence
{
    internal class SqlitePositionDbContext : PositionDbContext
    {
        public SqlitePositionDbContext(
            DbContextOptions<PositionDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            var positionEntityType = modelBuilder.Model.FindEntityType(typeof(Position));

            positionEntityType?.RemoveCheckConstraint(
                "CK_Positions_AssetClass");

            positionEntityType?.RemoveCheckConstraint(
                "CK_Positions_InstrumentId_NotEmpty");

            positionEntityType?.RemoveCheckConstraint(
                "CK_Positions_PnlCurrency");

            modelBuilder.ApplyConfiguration(
                new SqlitePositionCheckConstraintConfiguration());

            var positionMovementEntityType = modelBuilder.Model.FindEntityType(typeof(PositionMovement));

            positionMovementEntityType?.RemoveCheckConstraint(
                "CK_PositionMovements_AssetClass");

            positionMovementEntityType?.RemoveCheckConstraint(
                "CK_PositionMovements_InstrumentId_NotEmpty");

            positionMovementEntityType?.RemoveCheckConstraint(
                "CK_PositionMovements_PnlCurrency");

            modelBuilder.ApplyConfiguration(
                new SqlitePositionMovementCheckConstraintConfiguration());
        }
    }
}
