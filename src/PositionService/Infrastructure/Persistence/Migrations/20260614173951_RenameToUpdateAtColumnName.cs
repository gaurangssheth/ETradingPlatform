using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PositionService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameToUpdateAtColumnName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UpdateddAt",
                table: "Positions",
                newName: "UpdatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "Positions",
                newName: "UpdateddAt");
        }
    }
}
