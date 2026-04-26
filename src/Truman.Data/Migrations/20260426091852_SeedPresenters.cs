using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Truman.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedPresenters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
INSERT INTO ""Presenters"" (""Label"", ""PresenterStyle"") VALUES
  ('Default', 'A British news presenter'),
  ('Jimmy', 'Jimmy Carr'),
  ('Ricky', 'Ricky Gervais')
ON CONFLICT (""PresenterStyle"") DO NOTHING;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Seed-only migration - no destructive rollback to preserve admin-edited rows.
        }
    }
}
