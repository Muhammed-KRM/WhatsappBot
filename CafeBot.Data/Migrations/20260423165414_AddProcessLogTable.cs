using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessLogTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcessLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TraceId = table.Column<string>(type: "TEXT", nullable: false),
                    StepOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    StepName = table.Column<string>(type: "TEXT", nullable: false),
                    FunctionName = table.Column<string>(type: "TEXT", nullable: false),
                    InputData = table.Column<string>(type: "TEXT", nullable: true),
                    OutputData = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    DurationMs = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessLogs", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "Configurations",
                keyColumn: "Id",
                keyValue: 1,
                column: "LastUpdated",
                value: new DateTime(2026, 4, 23, 16, 54, 13, 601, DateTimeKind.Utc).AddTicks(1555));

            migrationBuilder.CreateIndex(
                name: "IX_ProcessLog_CreatedAt",
                table: "ProcessLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessLog_TraceId",
                table: "ProcessLogs",
                column: "TraceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcessLogs");

            migrationBuilder.UpdateData(
                table: "Configurations",
                keyColumn: "Id",
                keyValue: 1,
                column: "LastUpdated",
                value: new DateTime(2026, 4, 22, 22, 0, 12, 729, DateTimeKind.Utc).AddTicks(7187));
        }
    }
}
