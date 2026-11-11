using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Truman.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRssItemFeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FeedId",
                table: "RssItems",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RssItems_FeedId",
                table: "RssItems",
                column: "FeedId");

            migrationBuilder.AddForeignKey(
                name: "FK_RssItems_Feeds_FeedId",
                table: "RssItems",
                column: "FeedId",
                principalTable: "Feeds",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RssItems_Feeds_FeedId",
                table: "RssItems");

            migrationBuilder.DropIndex(
                name: "IX_RssItems_FeedId",
                table: "RssItems");

            migrationBuilder.DropColumn(
                name: "FeedId",
                table: "RssItems");
        }
    }
}
