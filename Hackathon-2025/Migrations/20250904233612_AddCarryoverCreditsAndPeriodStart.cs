using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hackathon_2025.Migrations
{
    /// <inheritdoc />
    public partial class AddCarryoverCreditsAndPeriodStart : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AddOnBalance",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AddOnSpentThisPeriod",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "CurrentPeriodStartUtc",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE Users
                SET CurrentPeriodStartUtc =
                    CASE
                      WHEN LastReset IS NOT NULL THEN DATEFROMPARTS(YEAR(LastReset), MONTH(LastReset), 1)
                      ELSE NULL
                    END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AddOnBalance",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AddOnSpentThisPeriod",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CurrentPeriodStartUtc",
                table: "Users");
        }
    }
}