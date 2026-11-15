using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Truman.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRssItemImageUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "RssItems",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "RssItems");
        }
    }
}
