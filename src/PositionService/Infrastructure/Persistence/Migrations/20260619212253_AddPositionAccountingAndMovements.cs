using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PositionService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPositionAccountingAndMovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UpdateddAt",
                table: "Positions",
                newName: "UpdatedAt");

            migrationBuilder.AddColumn<decimal>(
                name: "RealisedPnl",
                table: "Positions",
                type: "decimal(18,8)",
                precision: 18,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "UnrealisedPnl",
                table: "Positions",
                type: "decimal(18,8)",
                precision: 18,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "PositionMovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PositionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TradeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Side = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    SignedQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    PreviousNetQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    PreviousAveragePrice = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    NewNetQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    NewAveragePrice = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    RealisedPnl = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PositionMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PositionMovements_Positions_PositionId",
                        column: x => x.PositionId,
                        principalTable: "Positions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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

            migrationBuilder.CreateIndex(
                name: "IX_PositionMovements_ClientId_Symbol",
                table: "PositionMovements",
                columns: new[] { "ClientId", "Symbol" });

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

            migrationBuilder.DropColumn(
                name: "RealisedPnl",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "UnrealisedPnl",
                table: "Positions");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "Positions",
                newName: "UpdateddAt");
        }
    }
}
