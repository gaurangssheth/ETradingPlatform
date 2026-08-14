using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PositionService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialPositionSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Positions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    InstrumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetClass = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NetQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    AveragePrice = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    PnlCurrency = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                    RealisedPnl = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    UnrealisedPnl = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Positions", x => x.Id);
                    table.CheckConstraint("CK_Positions_AssetClass", "[AssetClass] COLLATE Latin1_General_100_BIN2 IN ('Fx', 'Equity', 'FixedIncome')");
                    table.CheckConstraint("CK_Positions_InstrumentId_NotEmpty", "[InstrumentId] <> '00000000-0000-0000-0000-000000000000'");
                    table.CheckConstraint("CK_Positions_PnlCurrency", "LEN([PnlCurrency]) = 3 AND [PnlCurrency] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z]%'");
                });

            migrationBuilder.CreateTable(
                name: "ProcessedTrades",
                columns: table => new
                {
                    TradeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessedTrades", x => x.TradeId);
                });

            migrationBuilder.CreateTable(
                name: "PositionMovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PositionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TradeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstrumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AssetClass = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Side = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    SignedQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    PreviousNetQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    PreviousAveragePrice = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    NewNetQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    NewAveragePrice = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    PnlCurrency = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                    PreviousRealisedPnl = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    RealisedPnlChange = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    NewRealisedPnl = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PositionMovements", x => x.Id);
                    table.CheckConstraint("CK_PositionMovements_AssetClass", "[AssetClass] COLLATE Latin1_General_100_BIN2 IN ('Fx', 'Equity', 'FixedIncome')");
                    table.CheckConstraint("CK_PositionMovements_InstrumentId_NotEmpty", "[InstrumentId] <> '00000000-0000-0000-0000-000000000000'");
                    table.CheckConstraint("CK_PositionMovements_PnlCurrency", "LEN([PnlCurrency]) = 3 AND [PnlCurrency] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z]%'");
                    table.ForeignKey(
                        name: "FK_PositionMovements_Positions_PositionId",
                        column: x => x.PositionId,
                        principalTable: "Positions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PositionMovements_ClientId_InstrumentId",
                table: "PositionMovements",
                columns: new[] { "ClientId", "InstrumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_PositionMovements_PositionId",
                table: "PositionMovements",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_PositionMovements_TradeId",
                table: "PositionMovements",
                column: "TradeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Positions_ClientId_InstrumentId",
                table: "Positions",
                columns: new[] { "ClientId", "InstrumentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedTrades_OrderId",
                table: "ProcessedTrades",
                column: "OrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PositionMovements");

            migrationBuilder.DropTable(
                name: "ProcessedTrades");

            migrationBuilder.DropTable(
                name: "Positions");
        }
    }
}
