using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvestView.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHoldingPendingReceiveQuantity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "PendingReceiveQuantity",
                table: "Holdings",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PendingReceiveQuantity",
                table: "Holdings");
        }
    }
}
