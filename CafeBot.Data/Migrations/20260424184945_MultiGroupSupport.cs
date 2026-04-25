using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class MultiGroupSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TargetGroupId",
                table: "Configurations");

            migrationBuilder.DropColumn(
                name: "TargetGroupName",
                table: "Configurations");

            migrationBuilder.AddColumn<string>(
                name: "TargetGroupIdsJson",
                table: "Configurations",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TargetGroupNamesJson",
                table: "Configurations",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TargetGroupIdsJson",
                table: "Configurations");

            migrationBuilder.DropColumn(
                name: "TargetGroupNamesJson",
                table: "Configurations");

            migrationBuilder.AddColumn<string>(
                name: "TargetGroupId",
                table: "Configurations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetGroupName",
                table: "Configurations",
                type: "TEXT",
                nullable: true);
        }
    }
}
