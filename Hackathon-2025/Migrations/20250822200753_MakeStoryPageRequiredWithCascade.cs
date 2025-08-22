using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hackathon_2025.Migrations
{
    /// <inheritdoc />
    public partial class MakeStoryPageRequiredWithCascade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StoryPages_Stories_StoryId",
                table: "StoryPages");

            migrationBuilder.AlterColumn<int>(
                name: "StoryId",
                table: "StoryPages",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_StoryPages_Stories_StoryId",
                table: "StoryPages",
                column: "StoryId",
                principalTable: "Stories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StoryPages_Stories_StoryId",
                table: "StoryPages");

            migrationBuilder.AlterColumn<int>(
                name: "StoryId",
                table: "StoryPages",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddForeignKey(
                name: "FK_StoryPages_Stories_StoryId",
                table: "StoryPages",
                column: "StoryId",
                principalTable: "Stories",
                principalColumn: "Id");
        }
    }
}
