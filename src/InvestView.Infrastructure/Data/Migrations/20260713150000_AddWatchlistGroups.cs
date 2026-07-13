using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvestView.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWatchlistGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WatchlistGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchlistGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WatchlistGroups_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WatchlistGroups_UserId_Name",
                table: "WatchlistGroups",
                columns: new[] { "UserId", "Name" },
                unique: true);

            migrationBuilder.Sql(
                """
                INSERT INTO WatchlistGroups (Id, UserId, Name, CreatedAt, UpdatedAt)
                SELECT NEWID(), UserId, N'Danh mục mặc định', MIN(CreatedAt), MIN(CreatedAt)
                FROM WatchlistItems
                GROUP BY UserId
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_WatchlistItems_Users_UserId",
                table: "WatchlistItems");

            migrationBuilder.DropIndex(
                name: "IX_WatchlistItems_UserId_BoardId_Symbol",
                table: "WatchlistItems");

            migrationBuilder.AddColumn<Guid>(
                name: "GroupId",
                table: "WatchlistItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE item
                SET GroupId = [group].Id
                FROM WatchlistItems item
                INNER JOIN WatchlistGroups [group]
                    ON [group].UserId = item.UserId
                    AND [group].Name = N'Danh mục mặc định'
                """);

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "WatchlistItems");

            migrationBuilder.AlterColumn<Guid>(
                name: "GroupId",
                table: "WatchlistItems",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WatchlistItems_GroupId_BoardId_Symbol",
                table: "WatchlistItems",
                columns: new[] { "GroupId", "BoardId", "Symbol" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_WatchlistItems_WatchlistGroups_GroupId",
                table: "WatchlistItems",
                column: "GroupId",
                principalTable: "WatchlistGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WatchlistItems_WatchlistGroups_GroupId",
                table: "WatchlistItems");

            migrationBuilder.DropIndex(
                name: "IX_WatchlistItems_GroupId_BoardId_Symbol",
                table: "WatchlistItems");

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "WatchlistItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE item
                SET UserId = [group].UserId
                FROM WatchlistItems item
                INNER JOIN WatchlistGroups [group]
                    ON [group].Id = item.GroupId
                """);

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "WatchlistItems");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "WatchlistItems",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WatchlistItems_UserId_BoardId_Symbol",
                table: "WatchlistItems",
                columns: new[] { "UserId", "BoardId", "Symbol" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_WatchlistItems_Users_UserId",
                table: "WatchlistItems",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.DropTable(
                name: "WatchlistGroups");
        }
    }
}
