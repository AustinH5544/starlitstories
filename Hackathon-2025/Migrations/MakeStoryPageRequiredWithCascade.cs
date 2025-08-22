using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

public partial class MakeStoryPageRequiredWithCascade : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Drop existing FK (if present) to re-add with CASCADE
        migrationBuilder.DropForeignKey(
            name: "FK_StoryPages_Stories_StoryId",
            table: "StoryPages");

        // Remove orphan pages so we can make StoryId NOT NULL safely
        migrationBuilder.Sql("DELETE FROM [StoryPages] WHERE [StoryId] IS NULL");

        // Make StoryId NOT NULL
        migrationBuilder.AlterColumn<int>(
            name: "StoryId",
            table: "StoryPages",
            type: "int",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "int",
            oldNullable: true);

        // Recreate FK with CASCADE
        migrationBuilder.AddForeignKey(
            name: "FK_StoryPages_Stories_StoryId",
            table: "StoryPages",
            column: "StoryId",
            principalTable: "Stories",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }

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
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }
}