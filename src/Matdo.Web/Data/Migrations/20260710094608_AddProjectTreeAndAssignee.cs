using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Matdo.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectTreeAndAssignee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "AssigneeId",
                table: "Task",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ParentProjectId",
                table: "Project",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Task_AssigneeId",
                table: "Task",
                column: "AssigneeId");

            migrationBuilder.CreateIndex(
                name: "IX_Project_ParentProjectId",
                table: "Project",
                column: "ParentProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_Project_Project_ParentProjectId",
                table: "Project",
                column: "ParentProjectId",
                principalTable: "Project",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Task_User_AssigneeId",
                table: "Task",
                column: "AssigneeId",
                principalTable: "User",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Project_Project_ParentProjectId",
                table: "Project");

            migrationBuilder.DropForeignKey(
                name: "FK_Task_User_AssigneeId",
                table: "Task");

            migrationBuilder.DropIndex(
                name: "IX_Task_AssigneeId",
                table: "Task");

            migrationBuilder.DropIndex(
                name: "IX_Project_ParentProjectId",
                table: "Project");

            migrationBuilder.DropColumn(
                name: "AssigneeId",
                table: "Task");

            migrationBuilder.DropColumn(
                name: "ParentProjectId",
                table: "Project");
        }
    }
}
