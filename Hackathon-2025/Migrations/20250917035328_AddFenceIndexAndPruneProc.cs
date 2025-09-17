using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hackathon_2025.Migrations
{
    /// <inheritdoc />
    public partial class AddFenceIndexAndPruneProc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Nonclustered index to speed up pruning by date
            migrationBuilder.CreateIndex(
                name: "IX_ProcessedWebhooks_ProcessedAtUtc",
                table: "ProcessedWebhooks",
                column: "ProcessedAtUtc");

            // Ensure the proc exists (no GO; do create-if-missing)
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.usp_PruneProcessedWebhooks', N'P') IS NULL
    EXEC('CREATE PROCEDURE dbo.usp_PruneProcessedWebhooks AS BEGIN SET NOCOUNT ON; RETURN; END');
");

            // Now define/overwrite the body
            migrationBuilder.Sql(@"
ALTER PROCEDURE dbo.usp_PruneProcessedWebhooks
    @DaysToKeep int = 90
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @cutoff datetime2 = DATEADD(day, -@DaysToKeep, SYSUTCDATETIME());

    ;WITH dead AS (
        SELECT EventId
        FROM dbo.ProcessedWebhooks WITH (READPAST)
        WHERE ProcessedAtUtc < @cutoff
    )
    DELETE PW
    FROM dbo.ProcessedWebhooks AS PW
    INNER JOIN dead AS d ON d.EventId = PW.EventId;
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop the stored procedure if present
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.usp_PruneProcessedWebhooks', N'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_PruneProcessedWebhooks;
");

            // Drop the index
            migrationBuilder.DropIndex(
                name: "IX_ProcessedWebhooks_ProcessedAtUtc",
                table: "ProcessedWebhooks");
        }
    }
}