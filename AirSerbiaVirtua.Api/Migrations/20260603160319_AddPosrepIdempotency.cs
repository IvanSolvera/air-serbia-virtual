using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AirSerbiaVirtua.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPosrepIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add nullable first so existing rows survive...
            migrationBuilder.AddColumn<Guid>(
                name: "ClientReportId",
                table: "PositionLogs",
                type: "uuid",
                nullable: true);

            // ...backfill each existing sample with a distinct id so the unique
            // (PirepId, ClientReportId) index below does not collide. gen_random_uuid()
            // is built into PostgreSQL 13+.
            migrationBuilder.Sql(
                @"UPDATE ""PositionLogs"" SET ""ClientReportId"" = gen_random_uuid() WHERE ""ClientReportId"" IS NULL;");

            // ...then enforce NOT NULL (no DB default — the app always supplies a value).
            migrationBuilder.AlterColumn<Guid>(
                name: "ClientReportId",
                table: "PositionLogs",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PositionLogs_PirepId_ClientReportId",
                table: "PositionLogs",
                columns: new[] { "PirepId", "ClientReportId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PositionLogs_PirepId_ClientReportId",
                table: "PositionLogs");

            migrationBuilder.DropColumn(
                name: "ClientReportId",
                table: "PositionLogs");
        }
    }
}
