using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hackathon_2025.Migrations
{
    /// <inheritdoc />
    public partial class AddPortableBillingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BillingCustomerRef",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingProvider",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingSubscriptionRef",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CurrentPeriodEndUtc",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlanKey",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PlanStatus",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BillingCustomerRef",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "BillingProvider",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "BillingSubscriptionRef",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CurrentPeriodEndUtc",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PlanKey",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PlanStatus",
                table: "Users");
        }
    }
}
