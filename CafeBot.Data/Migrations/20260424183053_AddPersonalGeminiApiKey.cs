using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonalGeminiApiKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PersonalGeminiApiKey",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PersonalGeminiApiKey",
                table: "AspNetUsers");
        }
    }
}
