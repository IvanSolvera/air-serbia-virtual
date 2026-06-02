using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AirSerbiaVirtua.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Airports",
                columns: table => new
                {
                    Icao = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    Iata = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Country = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Lat = table.Column<double>(type: "double precision", nullable: false),
                    Lon = table.Column<double>(type: "double precision", nullable: false),
                    Elevation = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Airports", x => x.Icao);
                });

            migrationBuilder.CreateTable(
                name: "Ranks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    MinHours = table.Column<decimal>(type: "numeric(8,1)", nullable: false),
                    AllowedAircraftTypes = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ranks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Aircraft",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Registration = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Selcal = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    HubId = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Aircraft", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Aircraft_Airports_HubId",
                        column: x => x.HubId,
                        principalTable: "Airports",
                        principalColumn: "Icao",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Pilots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Callsign = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    RankId = table.Column<int>(type: "integer", nullable: false),
                    TotalHours = table.Column<decimal>(type: "numeric(9,1)", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    HubId = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    DateJoined = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pilots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pilots_Airports_HubId",
                        column: x => x.HubId,
                        principalTable: "Airports",
                        principalColumn: "Icao",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pilots_Ranks_RankId",
                        column: x => x.RankId,
                        principalTable: "Ranks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Routes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FlightNumber = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    DepIcao = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    ArrIcao = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    AircraftType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Distance = table.Column<int>(type: "integer", nullable: false),
                    PlannedTime = table.Column<int>(type: "integer", nullable: false),
                    Days = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Routes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Routes_Airports_ArrIcao",
                        column: x => x.ArrIcao,
                        principalTable: "Airports",
                        principalColumn: "Icao",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Routes_Airports_DepIcao",
                        column: x => x.DepIcao,
                        principalTable: "Airports",
                        principalColumn: "Icao",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Bookings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PilotId = table.Column<int>(type: "integer", nullable: false),
                    RouteId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bookings_Pilots_PilotId",
                        column: x => x.PilotId,
                        principalTable: "Pilots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Bookings_Routes_RouteId",
                        column: x => x.RouteId,
                        principalTable: "Routes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Pireps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PilotId = table.Column<int>(type: "integer", nullable: false),
                    RouteId = table.Column<int>(type: "integer", nullable: false),
                    AircraftId = table.Column<int>(type: "integer", nullable: false),
                    DepActual = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ArrActual = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    BlockMin = table.Column<int>(type: "integer", nullable: false),
                    AirMin = table.Column<int>(type: "integer", nullable: false),
                    FuelUsedKg = table.Column<int>(type: "integer", nullable: false),
                    LandingRateFpm = table.Column<int>(type: "integer", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Source = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RawJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pireps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pireps_Aircraft_AircraftId",
                        column: x => x.AircraftId,
                        principalTable: "Aircraft",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pireps_Pilots_PilotId",
                        column: x => x.PilotId,
                        principalTable: "Pilots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pireps_Routes_RouteId",
                        column: x => x.RouteId,
                        principalTable: "Routes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PositionLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PirepId = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Lat = table.Column<double>(type: "double precision", nullable: false),
                    Lon = table.Column<double>(type: "double precision", nullable: false),
                    AltFt = table.Column<int>(type: "integer", nullable: false),
                    GsKts = table.Column<int>(type: "integer", nullable: false),
                    Phase = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PositionLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PositionLogs_Pireps_PirepId",
                        column: x => x.PirepId,
                        principalTable: "Pireps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Airports",
                columns: new[] { "Icao", "Country", "Elevation", "Iata", "Lat", "Lon", "Name", "Revision" },
                values: new object[] { "LYBE", "Serbia", 335, "BEG", 44.8184, 20.3091, "Belgrade Nikola Tesla Airport", 2026 });

            migrationBuilder.InsertData(
                table: "Ranks",
                columns: new[] { "Id", "AllowedAircraftTypes", "MinHours", "Name" },
                values: new object[,]
                {
                    { 1, "[\"ATR72\",\"E195\"]", 0.0m, "Cadet" },
                    { 2, "[\"ATR72\",\"E195\",\"A319\",\"A320\",\"A320neo\"]", 50.0m, "First Officer" },
                    { 3, "[\"ATR72\",\"E195\",\"A319\",\"A320\",\"A320neo\",\"A321neo\"]", 200.0m, "Senior First Officer" },
                    { 4, "[\"ATR72\",\"E195\",\"A319\",\"A320\",\"A320neo\",\"A321neo\",\"A330\"]", 500.0m, "Captain" },
                    { 5, "[\"ATR72\",\"E195\",\"A319\",\"A320\",\"A320neo\",\"A321neo\",\"A330\"]", 1500.0m, "Senior Captain" }
                });

            migrationBuilder.InsertData(
                table: "Aircraft",
                columns: new[] { "Id", "HubId", "Registration", "Selcal", "Status", "Type" },
                values: new object[,]
                {
                    { 1, "LYBE", "YU-API", "AB-CD", "Active", "A319" },
                    { 2, "LYBE", "YU-APB", "AE-FG", "Active", "A320" },
                    { 3, "LYBE", "YU-APN", "AH-JK", "Active", "A320neo" },
                    { 4, "LYBE", "YU-APM", "AL-MP", "Active", "A321neo" },
                    { 5, "LYBE", "YU-ALP", "AQ-RS", "Active", "ATR72" },
                    { 6, "LYBE", "YU-AED", "BC-DF", "Active", "E195" },
                    { 7, "LYBE", "YU-ARA", "BG-HJ", "Active", "A330" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Aircraft_HubId",
                table: "Aircraft",
                column: "HubId");

            migrationBuilder.CreateIndex(
                name: "IX_Aircraft_Registration",
                table: "Aircraft",
                column: "Registration",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Airports_Iata",
                table: "Airports",
                column: "Iata");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_PilotId_RouteId_Date",
                table: "Bookings",
                columns: new[] { "PilotId", "RouteId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_RouteId",
                table: "Bookings",
                column: "RouteId");

            migrationBuilder.CreateIndex(
                name: "IX_Pilots_Callsign",
                table: "Pilots",
                column: "Callsign",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pilots_Email",
                table: "Pilots",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pilots_HubId",
                table: "Pilots",
                column: "HubId");

            migrationBuilder.CreateIndex(
                name: "IX_Pilots_RankId",
                table: "Pilots",
                column: "RankId");

            migrationBuilder.CreateIndex(
                name: "IX_Pireps_AircraftId",
                table: "Pireps",
                column: "AircraftId");

            migrationBuilder.CreateIndex(
                name: "IX_Pireps_PilotId",
                table: "Pireps",
                column: "PilotId");

            migrationBuilder.CreateIndex(
                name: "IX_Pireps_RouteId",
                table: "Pireps",
                column: "RouteId");

            migrationBuilder.CreateIndex(
                name: "IX_Pireps_Status",
                table: "Pireps",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PositionLogs_PirepId_Timestamp",
                table: "PositionLogs",
                columns: new[] { "PirepId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_Ranks_Name",
                table: "Ranks",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Routes_ArrIcao",
                table: "Routes",
                column: "ArrIcao");

            migrationBuilder.CreateIndex(
                name: "IX_Routes_DepIcao",
                table: "Routes",
                column: "DepIcao");

            migrationBuilder.CreateIndex(
                name: "IX_Routes_FlightNumber",
                table: "Routes",
                column: "FlightNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Bookings");
            migrationBuilder.DropTable(name: "PositionLogs");
            migrationBuilder.DropTable(name: "Pireps");
            migrationBuilder.DropTable(name: "Aircraft");
            migrationBuilder.DropTable(name: "Routes");
            migrationBuilder.DropTable(name: "Pilots");
            migrationBuilder.DropTable(name: "Ranks");
            migrationBuilder.DropTable(name: "Airports");
        }
    }
}
