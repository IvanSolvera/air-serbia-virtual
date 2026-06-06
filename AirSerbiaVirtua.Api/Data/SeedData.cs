using AirSerbiaVirtua.Api.Models;
using AirSerbiaVirtua.Contracts;
using Microsoft.EntityFrameworkCore;
using Route = AirSerbiaVirtua.Api.Models.Route;

namespace AirSerbiaVirtua.Api.Data;

/// <summary>
/// Static seed data applied through the model (<c>HasData</c>), so it becomes
/// part of the EF Core migration and is inserted on <c>database update</c>.
/// Seeds the primary hub LYBE and the Air Serbia mainline + regional fleet.
/// </summary>
public static class SeedData
{
    public const string HubIcao = "LYBE";

    public static void Apply(ModelBuilder b)
    {
        // ---- Primary hub + initial destination network ----------------------
        b.Entity<Airport>().HasData(
            new Airport { Icao = HubIcao, Iata = "BEG", Name = "Belgrade Nikola Tesla Airport", Country = "Serbia",  Lat = 44.8184, Lon = 20.3091, Elevation = 335,  Revision = 2026 },
            new Airport { Icao = "LOWW", Iata = "VIE", Name = "Vienna International Airport",    Country = "Austria", Lat = 48.1103, Lon = 16.5697, Elevation = 600,  Revision = 2026 },
            new Airport { Icao = "LZIB", Iata = "BTS", Name = "M. R. Štefánik Airport",          Country = "Slovakia",Lat = 48.1702, Lon = 17.2127, Elevation = 436,  Revision = 2026 },
            new Airport { Icao = "EDDF", Iata = "FRA", Name = "Frankfurt Airport",               Country = "Germany", Lat = 50.0336, Lon = 8.5705,  Elevation = 364,  Revision = 2026 },
            new Airport { Icao = "LIRF", Iata = "FCO", Name = "Rome Fiumicino Airport",          Country = "Italy",   Lat = 41.8003, Lon = 12.2389, Elevation = 13,   Revision = 2026 },
            new Airport { Icao = "LTFM", Iata = "IST", Name = "Istanbul Airport",                Country = "Türkiye", Lat = 41.2619, Lon = 28.7414, Elevation = 325,  Revision = 2026 },
            new Airport { Icao = "LSZH", Iata = "ZRH", Name = "Zürich Airport",                  Country = "Switzerland", Lat = 47.4647, Lon = 8.5492, Elevation = 1416, Revision = 2026 }
        );

        // ---- Initial route network from LYBE --------------------------------
        b.Entity<Route>().HasData(
            new Route { Id = 1, FlightNumber = "JU360", DepIcao = HubIcao, ArrIcao = "LOWW", AircraftType = "A319", Distance = 215, PlannedTime = 50,  Days = new() { 1, 2, 3, 4, 5, 6, 7 } },
            new Route { Id = 2, FlightNumber = "JU450", DepIcao = HubIcao, ArrIcao = "LZIB", AircraftType = "A319", Distance = 195, PlannedTime = 45,  Days = new() { 1, 2, 3, 4, 5 } },
            new Route { Id = 3, FlightNumber = "JU380", DepIcao = HubIcao, ArrIcao = "EDDF", AircraftType = "A320", Distance = 580, PlannedTime = 95,  Days = new() { 1, 2, 3, 4, 5, 6, 7 } },
            new Route { Id = 4, FlightNumber = "JU410", DepIcao = HubIcao, ArrIcao = "LIRF", AircraftType = "A320", Distance = 500, PlannedTime = 90,  Days = new() { 1, 2, 3, 4, 5, 6, 7 } },
            new Route { Id = 5, FlightNumber = "JU800", DepIcao = HubIcao, ArrIcao = "LTFM", AircraftType = "A320", Distance = 480, PlannedTime = 85,  Days = new() { 1, 2, 3, 4, 5, 6, 7 } },
            new Route { Id = 6, FlightNumber = "JU390", DepIcao = HubIcao, ArrIcao = "LSZH", AircraftType = "A319", Distance = 615, PlannedTime = 105, Days = new() { 1, 2, 3, 4, 5, 6, 7 } }
        );

        // ---- Ranks ----------------------------------------------------------
        b.Entity<Rank>().HasData(
            new Rank { Id = 1, Name = "Cadet", MinHours = 0m,
                AllowedAircraftTypes = new() { "ATR72", "E195" } },
            new Rank { Id = 2, Name = "First Officer", MinHours = 50m,
                AllowedAircraftTypes = new() { "ATR72", "E195", "A319", "A320", "A320neo" } },
            new Rank { Id = 3, Name = "Senior First Officer", MinHours = 200m,
                AllowedAircraftTypes = new() { "ATR72", "E195", "A319", "A320", "A320neo", "A321neo" } },
            new Rank { Id = 4, Name = "Captain", MinHours = 500m,
                AllowedAircraftTypes = new() { "ATR72", "E195", "A319", "A320", "A320neo", "A321neo", "A330" } },
            new Rank { Id = 5, Name = "Senior Captain", MinHours = 1500m,
                AllowedAircraftTypes = new() { "ATR72", "E195", "A319", "A320", "A320neo", "A321neo", "A330" } }
        );

        // ---- Fleet (all based at LYBE) --------------------------------------
        b.Entity<Aircraft>().HasData(
            new Aircraft { Id = 1, Type = "A319",    Registration = "YU-API", Selcal = "AB-CD", HubId = HubIcao, Status = AircraftStatus.Active },
            new Aircraft { Id = 2, Type = "A320",    Registration = "YU-APB", Selcal = "AE-FG", HubId = HubIcao, Status = AircraftStatus.Active },
            new Aircraft { Id = 3, Type = "A320neo", Registration = "YU-APN", Selcal = "AH-JK", HubId = HubIcao, Status = AircraftStatus.Active },
            new Aircraft { Id = 4, Type = "A321neo", Registration = "YU-APM", Selcal = "AL-MP", HubId = HubIcao, Status = AircraftStatus.Active },
            new Aircraft { Id = 5, Type = "ATR72",   Registration = "YU-ALP", Selcal = "AQ-RS", HubId = HubIcao, Status = AircraftStatus.Active },
            new Aircraft { Id = 6, Type = "E195",    Registration = "YU-AED", Selcal = "BC-DF", HubId = HubIcao, Status = AircraftStatus.Active },
            new Aircraft { Id = 7, Type = "A330",    Registration = "YU-ARA", Selcal = "BG-HJ", HubId = HubIcao, Status = AircraftStatus.Active }
        );
    }
}
