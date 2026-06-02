using AirSerbiaVirtua.Api.Models;
using Microsoft.EntityFrameworkCore;

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
        // ---- Primary hub: Belgrade Nikola Tesla Airport ---------------------
        b.Entity<Airport>().HasData(new Airport
        {
            Icao = HubIcao,
            Iata = "BEG",
            Name = "Belgrade Nikola Tesla Airport",
            Country = "Serbia",
            Lat = 44.8184,
            Lon = 20.3091,
            Elevation = 335,
            Revision = 2026
        });

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
