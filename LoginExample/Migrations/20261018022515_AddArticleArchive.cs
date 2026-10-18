using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoginExample.Migrations
{
    /// <inheritdoc />
    public partial class AddArticleArchive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "Articles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Articles",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Articles");
        }
    }
}
