using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hackathon_2025.Migrations
{
    /// <inheritdoc />
    public partial class AllowFiveSavedCharacters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SavedCharacters_UserId",
                table: "SavedCharacters");

            migrationBuilder.CreateIndex(
                name: "IX_SavedCharacters_UserId",
                table: "SavedCharacters",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SavedCharacters_UserId",
                table: "SavedCharacters");

            migrationBuilder.CreateIndex(
                name: "IX_SavedCharacters_UserId",
                table: "SavedCharacters",
                column: "UserId",
                unique: true);
        }
    }
}
