using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Matdo.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EmailConfirmToken",
                table: "User",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EmailConfirmed",
                table: "User",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "FailedLoginCount",
                table: "User",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockoutUntilUtc",
                table: "User",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordResetExpiresUtc",
                table: "User",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PasswordResetToken",
                table: "User",
                type: "uuid",
                nullable: true);

            // Bestandsnutzer gelten als bestätigt – sonst würde nach dem Update niemand mehr
            // als „verifiziert" gelten und alle bekämen den Bestätigungs-Hinweis.
            migrationBuilder.Sql("UPDATE \"User\" SET \"EmailConfirmed\" = true;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmailConfirmToken",
                table: "User");

            migrationBuilder.DropColumn(
                name: "EmailConfirmed",
                table: "User");

            migrationBuilder.DropColumn(
                name: "FailedLoginCount",
                table: "User");

            migrationBuilder.DropColumn(
                name: "LockoutUntilUtc",
                table: "User");

            migrationBuilder.DropColumn(
                name: "PasswordResetExpiresUtc",
                table: "User");

            migrationBuilder.DropColumn(
                name: "PasswordResetToken",
                table: "User");
        }
    }
}
