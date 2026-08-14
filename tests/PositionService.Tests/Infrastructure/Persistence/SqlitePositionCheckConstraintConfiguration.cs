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
    internal class SqlitePositionCheckConstraintConfiguration : IEntityTypeConfiguration<Position>
    {
        public void Configure(EntityTypeBuilder<Position> entity)
        {
            entity.ToTable("Positions", table =>
            {
                table.HasCheckConstraint(
                    "CK_Positions_AssetClass",
                    "[AssetClass] IN ('Fx', 'Equity', 'FixedIncome')");

                table.HasCheckConstraint(
                    "CK_Positions_InstrumentId_NotEmpty",
                    "[InstrumentId] <> '00000000-0000-0000-0000-000000000000'");

                table.HasCheckConstraint(
                    "CK_Positions_PnlCurrency",
                    "length([PnlCurrency]) = 3 AND " +
                    "[PnlCurrency] NOT GLOB '*[^A-Z]*'");
            });
        }
    }
}
