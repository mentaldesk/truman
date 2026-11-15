using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Truman.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFeedVisit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FeedSeenAt",
                table: "UserProfiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NewSince",
                table: "UserProfiles",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FeedSeenAt",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "NewSince",
                table: "UserProfiles");
        }
    }
}
