using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hackathon_2025.Migrations
{
    /// <inheritdoc />
    public partial class AddStorybookPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SuperStoriesGenerated",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SuperStoriesGenerated",
                table: "Users");
        }
    }
}
