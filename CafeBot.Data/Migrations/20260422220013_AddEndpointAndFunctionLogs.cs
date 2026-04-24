using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEndpointAndFunctionLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Configurations",
                keyColumn: "Id",
                keyValue: 1,
                column: "LastUpdated",
                value: new DateTime(2026, 4, 22, 22, 0, 12, 729, DateTimeKind.Utc).AddTicks(7187));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Configurations",
                keyColumn: "Id",
                keyValue: 1,
                column: "LastUpdated",
                value: new DateTime(2026, 4, 22, 21, 59, 39, 775, DateTimeKind.Utc).AddTicks(262));
        }
    }
}
