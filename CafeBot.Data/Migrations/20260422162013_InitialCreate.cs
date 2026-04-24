using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActivityLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    Details = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Configurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SessionId = table.Column<string>(type: "TEXT", nullable: true),
                    ConnectionStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetGroupId = table.Column<string>(type: "TEXT", nullable: true),
                    TargetGroupName = table.Column<string>(type: "TEXT", nullable: true),
                    PriorityListJson = table.Column<string>(type: "TEXT", nullable: false),
                    SystemStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Configurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProcessedMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MessageId = table.Column<string>(type: "TEXT", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SelectedHour = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessedMessages", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Configurations",
                columns: new[] { "Id", "ConnectionStatus", "LastUpdated", "PriorityListJson", "SessionId", "SystemStatus", "TargetGroupId", "TargetGroupName" },
                values: new object[] { 1, 0, new DateTime(2026, 4, 22, 16, 20, 10, 711, DateTimeKind.Utc).AddTicks(3662), "[]", null, 0, null, null });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLog_Timestamp",
                table: "ActivityLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedMessage_MessageId",
                table: "ProcessedMessages",
                column: "MessageId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityLogs");

            migrationBuilder.DropTable(
                name: "Configurations");

            migrationBuilder.DropTable(
                name: "ProcessedMessages");
        }
    }
}
