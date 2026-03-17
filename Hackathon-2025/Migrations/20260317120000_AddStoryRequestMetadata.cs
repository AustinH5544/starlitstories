using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hackathon_2025.Migrations
{
    public partial class AddStoryRequestMetadata : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RequestArtStyle",
                table: "Stories",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestCharactersJson",
                table: "Stories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestLessonLearned",
                table: "Stories",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestReadingLevel",
                table: "Stories",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestStoryLength",
                table: "Stories",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestTheme",
                table: "Stories",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequestArtStyle",
                table: "Stories");

            migrationBuilder.DropColumn(
                name: "RequestCharactersJson",
                table: "Stories");

            migrationBuilder.DropColumn(
                name: "RequestLessonLearned",
                table: "Stories");

            migrationBuilder.DropColumn(
                name: "RequestReadingLevel",
                table: "Stories");

            migrationBuilder.DropColumn(
                name: "RequestStoryLength",
                table: "Stories");

            migrationBuilder.DropColumn(
                name: "RequestTheme",
                table: "Stories");
        }
    }
}
