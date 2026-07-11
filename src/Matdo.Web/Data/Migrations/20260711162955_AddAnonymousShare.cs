using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Matdo.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAnonymousShare : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AnonymousAuthor",
                table: "Task",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AnonymousToken",
                table: "Project",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnonymousAuthor",
                table: "Task");

            migrationBuilder.DropColumn(
                name: "AnonymousToken",
                table: "Project");
        }
    }
}
