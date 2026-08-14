using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PositionService.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PositionService.Tests.Infrastructure.Persistence
{
    internal class SqlitePositionMovementCheckConstraintConfiguration :
        IEntityTypeConfiguration<PositionMovement>
    {
        public void Configure(EntityTypeBuilder<PositionMovement> entity)
        {
            entity.ToTable("PositionMovements", table =>
            {
                table.HasCheckConstraint(
                    "CK_PositionMovements_AssetClass",
                    "[AssetClass] IN ('Fx', 'Equity', 'FixedIncome')");

                table.HasCheckConstraint(
                    "CK_PositionMovements_InstrumentId_NotEmpty",
                    "[InstrumentId] <> '00000000-0000-0000-0000-000000000000'");

                table.HasCheckConstraint(
                    "CK_PositionMovements_PnlCurrency",
                    "length([PnlCurrency]) = 3 AND " +
                    "[PnlCurrency] NOT GLOB '*[^A-Z]*'");
            });
        }
    }
}
