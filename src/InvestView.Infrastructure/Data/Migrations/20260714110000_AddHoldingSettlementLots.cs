using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvestView.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHoldingSettlementLots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HoldingSettlementLots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    BoardId = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    SourceOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceExecutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Quantity = table.Column<long>(type: "bigint", nullable: false),
                    RemainingQuantity = table.Column<long>(type: "bigint", nullable: false),
                    TradeDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SettlementDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AvailableFromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SettledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HoldingSettlementLots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HoldingSettlementLots_Orders_SourceOrderId",
                        column: x => x.SourceOrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HoldingSettlementLots_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HoldingSettlementLots_SourceExecutionId",
                table: "HoldingSettlementLots",
                column: "SourceExecutionId");

            migrationBuilder.CreateIndex(
                name: "IX_HoldingSettlementLots_SourceOrderId",
                table: "HoldingSettlementLots",
                column: "SourceOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_HoldingSettlementLots_UserId_AvailableFromDate_Status",
                table: "HoldingSettlementLots",
                columns: new[] { "UserId", "AvailableFromDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_HoldingSettlementLots_UserId_BoardId_Symbol_Status",
                table: "HoldingSettlementLots",
                columns: new[] { "UserId", "BoardId", "Symbol", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HoldingSettlementLots");
        }
    }
}
