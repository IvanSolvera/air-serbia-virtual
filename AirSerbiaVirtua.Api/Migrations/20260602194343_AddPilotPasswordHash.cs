using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AirSerbiaVirtua.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPilotPasswordHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "Pilots",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PasswordUpdatedAtUtc",
                table: "Pilots",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "Pilots");

            migrationBuilder.DropColumn(
                name: "PasswordUpdatedAtUtc",
                table: "Pilots");
        }
    }
}
