using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Matdo.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurrence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RecurrenceInterval",
                table: "Task",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RecurrenceUnit",
                table: "Task",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecurrenceInterval",
                table: "Task");

            migrationBuilder.DropColumn(
                name: "RecurrenceUnit",
                table: "Task");
        }
    }
}
