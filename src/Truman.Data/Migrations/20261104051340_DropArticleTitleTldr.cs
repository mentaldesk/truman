using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Truman.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropArticleTitleTldr : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Title",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "Tldr",
                table: "Articles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "Articles",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Tldr",
                table: "Articles",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
