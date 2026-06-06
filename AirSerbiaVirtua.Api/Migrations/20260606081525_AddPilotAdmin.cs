using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AirSerbiaVirtua.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPilotAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAdmin",
                table: "Pilots",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Bootstrap admin: the founding pilot becomes the first administrator
            // and is promoted to Active — Pending pilots can no longer sign in, so
            // somebody must be able to approve them.
            migrationBuilder.Sql(
                """
                UPDATE "Pilots"
                SET "IsAdmin" = TRUE, "Status" = 1
                WHERE "Callsign" = 'ASL001';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAdmin",
                table: "Pilots");
        }
    }
}
