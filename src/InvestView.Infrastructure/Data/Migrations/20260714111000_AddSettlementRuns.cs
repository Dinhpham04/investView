using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvestView.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSettlementRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SettlementRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TriggeredByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DueLotCount = table.Column<int>(type: "int", nullable: false),
                    SettledLotCount = table.Column<int>(type: "int", nullable: false),
                    FailedLotCount = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SettlementRuns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SettlementRuns_StartedAt",
                table: "SettlementRuns",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SettlementRuns_TriggeredByUserId",
                table: "SettlementRuns",
                column: "TriggeredByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SettlementRuns");
        }
    }
}
