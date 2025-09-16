using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hackathon_2025.Migrations
{
    /// <inheritdoc />
    public partial class AddUsernameAndIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "Users",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UsernameNormalized",
                table: "Users",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(@"
;WITH cte AS (
  SELECT
    Id,
    LOWER(LEFT(Email, CHARINDEX('@', Email + '@') - 1)) AS BaseName,
    ROW_NUMBER() OVER (
      PARTITION BY LOWER(LEFT(Email, CHARINDEX('@', Email + '@') - 1))
      ORDER BY Id
    ) AS rn
  FROM Users
)
UPDATE u
SET
  Username = CASE WHEN c.rn = 1 THEN c.BaseName ELSE CONCAT(c.BaseName, c.rn) END,
  UsernameNormalized = CASE WHEN c.rn = 1 THEN c.BaseName ELSE CONCAT(c.BaseName, c.rn) END
FROM Users u
JOIN cte c ON u.Id = c.Id
WHERE (u.UsernameNormalized IS NULL OR u.UsernameNormalized = N'');
");

            // 2) (Optional but recommended) drop default constraints so future inserts don't get ''.
            //    EF doesn’t know constraint names ahead of time; we discover and drop them:
            migrationBuilder.Sql(@"
DECLARE @df1 sysname, @df2 sysname;
SELECT @df1 = d.name
FROM sys.default_constraints d
JOIN sys.columns c ON c.default_object_id = d.object_id
WHERE d.parent_object_id = OBJECT_ID(N'Users') AND c.name = N'Username';

IF @df1 IS NOT NULL EXEC('ALTER TABLE [Users] DROP CONSTRAINT [' + @df1 + ']');

SELECT @df2 = d.name
FROM sys.default_constraints d
JOIN sys.columns c ON c.default_object_id = d.object_id
WHERE d.parent_object_id = OBJECT_ID(N'Users') AND c.name = N'UsernameNormalized';

IF @df2 IS NOT NULL EXEC('ALTER TABLE [Users] DROP CONSTRAINT [' + @df2 + ']');
");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_UsernameNormalized",
                table: "Users",
                column: "UsernameNormalized",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_UsernameNormalized",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Username",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "UsernameNormalized",
                table: "Users");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }
    }
}
