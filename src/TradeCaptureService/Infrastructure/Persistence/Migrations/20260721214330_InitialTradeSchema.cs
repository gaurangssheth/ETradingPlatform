using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradeCaptureService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialTradeSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Trades",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Side = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    OrderType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    Notional = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    InstrumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetClass = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    NotionalCurrency = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CapturedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trades", x => x.Id);
                    table.CheckConstraint("CK_Trades_AssetClass", "[AssetClass] COLLATE Latin1_General_100_BIN2 IN ('Fx', 'Equity', 'FixedIncome')");
                    table.CheckConstraint("CK_Trades_InstrumentId_NotEmpty", "[InstrumentId] <> '00000000-0000-0000-0000-000000000000'");
                    table.CheckConstraint("CK_Trades_Notional_Positive", "[Notional] > 0");
                    table.CheckConstraint("CK_Trades_NotionalCurrency", "LEN([NotionalCurrency]) = 3 AND [NotionalCurrency] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^A-Z]%'");
                    table.CheckConstraint("CK_Trades_OrderType", "[OrderType] COLLATE Latin1_General_100_BIN2 IN ('Market', 'Limit')");
                    table.CheckConstraint("CK_Trades_Price_Positive", "[Price] > 0");
                    table.CheckConstraint("CK_Trades_Quantity_Positive", "[Quantity] > 0");
                    table.CheckConstraint("CK_Trades_Side", "[Side] COLLATE Latin1_General_100_BIN2 IN ('Buy', 'Sell')");
                    table.CheckConstraint("CK_Trades_Status", "[Status] COLLATE Latin1_General_100_BIN2 IN ('Captured', 'Cancelled', 'Amended')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Trades_InstrumentId",
                table: "Trades",
                column: "InstrumentId");

            migrationBuilder.CreateIndex(
                name: "IX_Trades_OrderId",
                table: "Trades",
                column: "OrderId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Trades");
        }
    }
}
