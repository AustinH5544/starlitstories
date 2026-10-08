using Hackathon_2025.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hackathon_2025.Migrations
{
    /// <summary>Records which credit a story draft reserved, so abandoned drafts refund the right one.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261008120000_AddStoryReservedFromAddOn")]
    public partial class AddStoryReservedFromAddOn : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ReservedFromAddOn",
                table: "Stories",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReservedFromAddOn",
                table: "Stories");
        }
    }
}
