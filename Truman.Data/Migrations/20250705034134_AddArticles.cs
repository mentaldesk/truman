using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Truman.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddArticles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Articles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Link = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Tldr = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Sentiment = table.Column<int>(type: "integer", nullable: false),
                    Tags = table.Column<string[]>(type: "jsonb", nullable: false),
                    Freedom = table.Column<int>(type: "integer", nullable: false),
                    Independence = table.Column<int>(type: "integer", nullable: false),
                    SelfRespect = table.Column<int>(type: "integer", nullable: false),
                    SelfActualization = table.Column<int>(type: "integer", nullable: false),
                    Creativity = table.Column<int>(type: "integer", nullable: false),
                    Honesty = table.Column<int>(type: "integer", nullable: false),
                    Compassion = table.Column<int>(type: "integer", nullable: false),
                    Loyalty = table.Column<int>(type: "integer", nullable: false),
                    Justice = table.Column<int>(type: "integer", nullable: false),
                    Responsibility = table.Column<int>(type: "integer", nullable: false),
                    Security = table.Column<int>(type: "integer", nullable: false),
                    Equality = table.Column<int>(type: "integer", nullable: false),
                    Tradition = table.Column<int>(type: "integer", nullable: false),
                    Obedience = table.Column<int>(type: "integer", nullable: false),
                    Success = table.Column<int>(type: "integer", nullable: false),
                    Ambition = table.Column<int>(type: "integer", nullable: false),
                    Discipline = table.Column<int>(type: "integer", nullable: false),
                    Knowledge = table.Column<int>(type: "integer", nullable: false),
                    OpenMindedness = table.Column<int>(type: "integer", nullable: false),
                    PeaceOfMind = table.Column<int>(type: "integer", nullable: false),
                    Pleasure = table.Column<int>(type: "integer", nullable: false),
                    Connection = table.Column<int>(type: "integer", nullable: false),
                    Adventure = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RssItemId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Articles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Articles_RssItems_RssItemId",
                        column: x => x.RssItemId,
                        principalTable: "RssItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Articles_Link",
                table: "Articles",
                column: "Link",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Articles_RssItemId",
                table: "Articles",
                column: "RssItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Articles");
        }
    }
}
