using AirSerbiaVirtua.Api.Data;
using AirSerbiaVirtua.Api.Models;
using AirSerbiaVirtua.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace AirSerbiaVirtua.Tests.Api;

/// <summary>
/// Inserts an isolated pilot + route + aircraft graph per call (unique ICAOs,
/// callsigns, tails via a counter) so tests never share or reset state.
/// </summary>
public static class TestData
{
    private static int _seq;

    public static async Task<(Pilot Pilot, Route Route, Aircraft Aircraft)> SeedAsync(
        bool admin = false)
    {
        var n = Interlocked.Increment(ref _seq);
        using var scope = ApiTestHost.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var dep = new Airport { Icao = $"ZA{n:00}", Name = "Test Dep", Country = "RS" };
        var arr = new Airport { Icao = $"ZB{n:00}", Name = "Test Arr", Country = "AT" };
        var rank = new Rank { Name = $"Rank {n}", MinHours = 0, AllowedAircraftTypes = ["A319"] };
        db.AddRange(dep, arr, rank);
        await db.SaveChangesAsync();

        var pilot = new Pilot
        {
            Callsign = $"TST{n:000}", Name = "Test Pilot", Email = $"t{n}@asv.test",
            PasswordHash = "not-a-real-hash", RankId = rank.Id,
            Status = PilotStatus.Active, IsAdmin = admin,
            HubId = dep.Icao, DateJoined = DateTimeOffset.UtcNow
        };
        var aircraft = new Aircraft
        {
            Type = "A319", Registration = $"YU-T{n:00}",
            HubId = dep.Icao, Status = AircraftStatus.Active
        };
        var route = new Route
        {
            FlightNumber = $"JU9{n:00}", DepIcao = dep.Icao, ArrIcao = arr.Icao,
            AircraftType = "A319", Distance = 300, PlannedTime = 55,
            Days = [1, 2, 3, 4, 5, 6, 7]
        };
        db.AddRange(pilot, aircraft, route);
        await db.SaveChangesAsync();
        return (pilot, route, aircraft);
    }

    public static async Task<Booking> SeedBookingAsync(
        int pilotId, int routeId, BookingStatus status = BookingStatus.Open,
        DateOnly? date = null, DateTimeOffset? dispatchReadyAtUtc = null)
    {
        using var scope = ApiTestHost.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var booking = new Booking
        {
            PilotId = pilotId, RouteId = routeId, Status = status,
            Date = date ?? DateOnly.FromDateTime(DateTime.UtcNow),
            DispatchReadyAtUtc = dispatchReadyAtUtc
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();
        return booking;
    }
}
