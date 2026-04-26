using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Truman.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFeeds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Feeds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Url = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Feeds", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Feeds_Url",
                table: "Feeds",
                column: "Url",
                unique: true);

            migrationBuilder.Sql(@"
INSERT INTO ""Feeds"" (""Url"", ""Name"", ""IsEnabled"") VALUES
  ('https://www.newscientist.com/feed/home/', 'New Scientist', TRUE),
  ('http://rss.sciam.com/basic-science', 'Scientific American', TRUE),
  ('https://devblogs.microsoft.com/dotnet/feed/', 'DevBlogs - .NET', TRUE),
  ('https://www.rnz.co.nz/rss/business.xml', 'RNZ Business', TRUE),
  ('https://www.rnz.co.nz/rss/media-technology.xml', 'RNZ Media & Tech', TRUE),
  ('https://www.rnz.co.nz/rss/world.xml', 'RNZ World', TRUE)
ON CONFLICT (""Url"") DO NOTHING;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Feeds");
        }
    }
}
