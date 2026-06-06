using System.Text.Json;
using AirSerbiaVirtua.Contracts;
using FluentAssertions;

namespace AirSerbiaVirtua.Tests.Unit.Contracts;

/// <summary>
/// Golden-JSON guards: serializing the canonical records with web defaults must
/// produce exactly what the API emitted before convergence (camelCase keys,
/// enums as numbers). If one of these fails, a client protocol just broke.
/// </summary>
public class WireShapeTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static readonly DateTimeOffset T = new(2026, 6, 6, 14, 32, 0, TimeSpan.Zero);

    [Test]
    public void BookingInfo_WithDispatchFlag_SerializesToExpectedJson()
    {
        var b = new BookingInfo(7, 3, "JU360", "LYBE", "LOWW", "A319", 55,
            new DateOnly(2026, 6, 6), BookingStatus.Open, T);
        JsonSerializer.Serialize(b, Web).Should().Be(
            "{\"id\":7,\"routeId\":3,\"flightNumber\":\"JU360\",\"depIcao\":\"LYBE\"," +
            "\"arrIcao\":\"LOWW\",\"aircraftType\":\"A319\",\"plannedMinutes\":55," +
            "\"date\":\"2026-06-06\",\"status\":0,\"dispatchReadyAtUtc\":\"2026-06-06T14:32:00+00:00\"}");
    }

    [Test]
    public void BookingInfo_WithoutDispatchFlag_SerializesNull()
    {
        var b = new BookingInfo(7, 3, "JU360", "LYBE", "LOWW", "A319", 55,
            new DateOnly(2026, 6, 6), BookingStatus.Confirmed);
        JsonSerializer.Serialize(b, Web).Should().Contain("\"status\":1")
            .And.Contain("\"dispatchReadyAtUtc\":null");
    }

    [Test]
    public void PilotProfile_SerializesStatusAsNumber()
    {
        var p = new PilotProfile(1, "ASL001", "Test Pilot", "t@asv.test",
            2, "First Officer", 12.5m, PilotStatus.Active, "LYBE", T, false);
        var json = JsonSerializer.Serialize(p, Web);
        json.Should().Contain("\"status\":1").And.Contain("\"callsign\":\"ASL001\"")
            .And.Contain("\"isAdmin\":false").And.Contain("\"totalHours\":12.5");
    }

    [Test]
    public void RouteInfo_SerializesToExpectedJson()
    {
        var r = new RouteInfo(3, "JU360", "LYBE", "LOWW", "A319", 300, 55, [1, 2, 3]);
        JsonSerializer.Serialize(r, Web).Should().Be(
            "{\"id\":3,\"flightNumber\":\"JU360\",\"depIcao\":\"LYBE\",\"arrIcao\":\"LOWW\"," +
            "\"aircraftType\":\"A319\",\"distanceNm\":300,\"plannedMinutes\":55,\"days\":[1,2,3]}");
    }

    [Test]
    public void PirepListItem_SerializesStatusAsNumber()
    {
        var p = new PirepListItem(9, "JU360", "LYBE", "LOWW", "A319", "YU-API",
            T, T.AddMinutes(55), 55, 48, 2100, -180, 92, PirepStatus.Accepted);
        JsonSerializer.Serialize(p, Web).Should()
            .Contain("\"status\":1").And.Contain("\"landingRateFpm\":-180");
    }

    [Test]
    public void PositionReport_SerializesPhaseAsNumber()
    {
        var id = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var pr = new PositionReport(id, T, 44.8, 20.3, 35000, 447, FlightPhase.Cruise);
        JsonSerializer.Serialize(pr, Web).Should()
            .Contain("\"clientReportId\":\"11111111-2222-3333-4444-555555555555\"")
            .And.Contain("\"phase\":5");
    }

    [Test]
    public void FlightStartResponse_RoundTrips()
    {
        var json = "{\"flightSessionId\":12,\"routeId\":3,\"aircraftId\":4," +
                   "\"aircraftRegistration\":\"YU-API\",\"startedAtUtc\":\"2026-06-06T14:32:00+00:00\"}";
        var r = JsonSerializer.Deserialize<FlightStartResponse>(json, Web)!;
        r.Should().Be(new FlightStartResponse(12, 3, 4, "YU-API", T));
    }

    [Test]
    public void LoginResponse_RoundTrips()
    {
        var tokens = new AuthTokens("at", T, "rt", T.AddDays(7));
        var pilot = new PilotProfile(1, "ASL001", "Test Pilot", "t@asv.test",
            2, "First Officer", 12.5m, PilotStatus.Active, "LYBE", T, true);
        var login = new LoginResponse(tokens, pilot);
        var roundTripped = JsonSerializer.Deserialize<LoginResponse>(
            JsonSerializer.Serialize(login, Web), Web);
        roundTripped.Should().Be(login);
    }

    [Test]
    // Ordinals are frozen because they are the wire protocol (JSON numbers) consumed by all
    // clients (ACARS desktop, Blazor WASM, mobile). The database stores enum names, not
    // numbers (HasConversion<string>()), so reordering would not break the DB — it would
    // break the wire protocol. Do not renumber or reorder these enums.
    public void EnumOrdinals_AreFrozen_WireProtocolContract()
    {
        ((int)BookingStatus.Open).Should().Be(0);
        ((int)BookingStatus.Confirmed).Should().Be(1);
        ((int)BookingStatus.Flown).Should().Be(2);
        ((int)BookingStatus.Cancelled).Should().Be(3);
        ((int)BookingStatus.Expired).Should().Be(4);
        ((int)PirepStatus.Aborted).Should().Be(4);
        ((int)PilotStatus.Banned).Should().Be(4);
        ((int)FlightPhase.Shutdown).Should().Be(10);
        ((int)AircraftStatus.Retired).Should().Be(4);
    }
}
