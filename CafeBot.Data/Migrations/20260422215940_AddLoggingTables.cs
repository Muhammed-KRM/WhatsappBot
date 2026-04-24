using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLoggingTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EndpointLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TraceId = table.Column<string>(type: "TEXT", nullable: true),
                    Method = table.Column<string>(type: "TEXT", nullable: false),
                    Path = table.Column<string>(type: "TEXT", nullable: false),
                    Query = table.Column<string>(type: "TEXT", nullable: true),
                    RequestBody = table.Column<string>(type: "TEXT", nullable: true),
                    ResponseBody = table.Column<string>(type: "TEXT", nullable: true),
                    StatusCode = table.Column<int>(type: "INTEGER", nullable: false),
                    IpAddress = table.Column<string>(type: "TEXT", nullable: true),
                    UserAgent = table.Column<string>(type: "TEXT", nullable: true),
                    DurationMs = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EndpointLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FunctionLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ErrorCode = table.Column<string>(type: "TEXT", nullable: false),
                    ClassName = table.Column<string>(type: "TEXT", nullable: true),
                    MethodName = table.Column<string>(type: "TEXT", nullable: true),
                    FilePath = table.Column<string>(type: "TEXT", nullable: true),
                    LineNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: false),
                    StackTrace = table.Column<string>(type: "TEXT", nullable: true),
                    InputType = table.Column<string>(type: "TEXT", nullable: true),
                    InputValue = table.Column<string>(type: "TEXT", nullable: true),
                    TraceId = table.Column<string>(type: "TEXT", nullable: true),
                    Severity = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FunctionLogs", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "Configurations",
                keyColumn: "Id",
                keyValue: 1,
                column: "LastUpdated",
                value: new DateTime(2026, 4, 22, 21, 59, 39, 775, DateTimeKind.Utc).AddTicks(262));

            migrationBuilder.CreateIndex(
                name: "IX_EndpointLog_CreatedAt",
                table: "EndpointLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_EndpointLog_StatusCode",
                table: "EndpointLogs",
                column: "StatusCode");

            migrationBuilder.CreateIndex(
                name: "IX_FunctionLog_CreatedAt",
                table: "FunctionLogs",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EndpointLogs");

            migrationBuilder.DropTable(
                name: "FunctionLogs");

            migrationBuilder.UpdateData(
                table: "Configurations",
                keyColumn: "Id",
                keyValue: 1,
                column: "LastUpdated",
                value: new DateTime(2026, 4, 22, 16, 20, 10, 711, DateTimeKind.Utc).AddTicks(3662));
        }
    }
}
