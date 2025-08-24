using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Truman.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPresenterContentsToArticle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add the new column as nullable
            migrationBuilder.AddColumn<string>(
                name: "PresenterContents",
                table: "Articles",
                type: "jsonb",
                nullable: true);

            // 2. Populate the new column for all existing rows
            migrationBuilder.Sql(
                @"UPDATE ""Articles"" 
                  SET ""PresenterContents"" = jsonb_build_object('John Cleese', ""Content"")"
            );

            // 3. Alter the column to be NOT NULL
            migrationBuilder.AlterColumn<string>(
                name: "PresenterContents",
                table: "Articles",
                type: "jsonb",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldNullable: true);

            // 4. Drop the old Content column
            migrationBuilder.DropColumn(
                name: "Content",
                table: "Articles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PresenterContents",
                table: "Articles");

            migrationBuilder.AddColumn<string>(
                name: "Content",
                table: "Articles",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
