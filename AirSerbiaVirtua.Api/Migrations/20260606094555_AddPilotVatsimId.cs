using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AirSerbiaVirtua.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPilotVatsimId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VatsimId",
                table: "Pilots",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VatsimId",
                table: "Pilots");
        }
    }
}
