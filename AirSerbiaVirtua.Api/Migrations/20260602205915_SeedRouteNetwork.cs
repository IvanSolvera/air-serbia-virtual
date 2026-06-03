using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AirSerbiaVirtua.Api.Migrations
{
    /// <inheritdoc />
    public partial class SeedRouteNetwork : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Airports",
                columns: new[] { "Icao", "Country", "Elevation", "Iata", "Lat", "Lon", "Name", "Revision" },
                values: new object[,]
                {
                    { "EDDF", "Germany", 364, "FRA", 50.0336, 8.5704999999999991, "Frankfurt Airport", 2026 },
                    { "LIRF", "Italy", 13, "FCO", 41.8003, 12.238899999999999, "Rome Fiumicino Airport", 2026 },
                    { "LOWW", "Austria", 600, "VIE", 48.110300000000002, 16.569700000000001, "Vienna International Airport", 2026 },
                    { "LSZH", "Switzerland", 1416, "ZRH", 47.464700000000001, 8.5492000000000008, "Zürich Airport", 2026 },
                    { "LTFM", "Türkiye", 325, "IST", 41.261899999999997, 28.741399999999999, "Istanbul Airport", 2026 },
                    { "LZIB", "Slovakia", 436, "BTS", 48.170200000000001, 17.212700000000002, "M. R. Štefánik Airport", 2026 }
                });

            migrationBuilder.InsertData(
                table: "Routes",
                columns: new[] { "Id", "AircraftType", "ArrIcao", "Days", "DepIcao", "Distance", "FlightNumber", "PlannedTime" },
                values: new object[,]
                {
                    { 1, "A319", "LOWW", "[1,2,3,4,5,6,7]", "LYBE", 215, "JU360", 50 },
                    { 2, "A319", "LZIB", "[1,2,3,4,5]", "LYBE", 195, "JU450", 45 },
                    { 3, "A320", "EDDF", "[1,2,3,4,5,6,7]", "LYBE", 580, "JU380", 95 },
                    { 4, "A320", "LIRF", "[1,2,3,4,5,6,7]", "LYBE", 500, "JU410", 90 },
                    { 5, "A320", "LTFM", "[1,2,3,4,5,6,7]", "LYBE", 480, "JU800", 85 },
                    { 6, "A319", "LSZH", "[1,2,3,4,5,6,7]", "LYBE", 615, "JU390", 105 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Routes",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Routes",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Routes",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Routes",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Routes",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Routes",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "Airports",
                keyColumn: "Icao",
                keyValue: "EDDF");

            migrationBuilder.DeleteData(
                table: "Airports",
                keyColumn: "Icao",
                keyValue: "LIRF");

            migrationBuilder.DeleteData(
                table: "Airports",
                keyColumn: "Icao",
                keyValue: "LOWW");

            migrationBuilder.DeleteData(
                table: "Airports",
                keyColumn: "Icao",
                keyValue: "LSZH");

            migrationBuilder.DeleteData(
                table: "Airports",
                keyColumn: "Icao",
                keyValue: "LTFM");

            migrationBuilder.DeleteData(
                table: "Airports",
                keyColumn: "Icao",
                keyValue: "LZIB");
        }
    }
}
