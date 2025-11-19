using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hackathon_2025.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeMembershipValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE [Users] SET [Membership] = 'Free'
                WHERE [Membership] IS NULL OR LTRIM(RTRIM(LOWER([Membership]))) IN ('', 'free');

                UPDATE [Users] SET [Membership] = 'Pro'
                WHERE LTRIM(RTRIM(LOWER([Membership]))) = 'pro';

                UPDATE [Users] SET [Membership] = 'Premium'
                WHERE LTRIM(RTRIM(LOWER([Membership]))) = 'premium';
                ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
