using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AirSerbiaVirtua.Contracts;
using FluentAssertions;

namespace AirSerbiaVirtua.Tests.Api;

/// <summary>
/// Locks the pre-W4 wire format of the endpoints every client already uses.
/// Asserts on raw JSON keys + numeric enum values, not on deserialized types
/// (deserializing through Contracts would mask a drift in both directions).
/// </summary>
public class WireCompatTests
{
    [Test]
    public async Task Routes_List_EmitsCamelCaseKeys()
    {
        var (pilot, route, _) = await TestData.SeedAsync();
        var client = ApiTestHost.CreateClientFor(pilot);

        var json = await client.GetStringAsync($"api/v1/routes?hub={route.DepIcao}");

        json.Should().Contain("\"flightNumber\"").And.Contain("\"depIcao\"")
            .And.Contain("\"distanceNm\"").And.Contain("\"plannedMinutes\"")
            .And.Contain("\"days\"");
        json.Should().NotContain("\"FlightNumber\"");   // PascalCase would mean options drift
    }

    [Test]
    public async Task Bookings_Mine_EmitsNumericStatus()
    {
        var (pilot, route, _) = await TestData.SeedAsync();
        await TestData.SeedBookingAsync(pilot.Id, route.Id);
        var client = ApiTestHost.CreateClientFor(pilot);

        var json = await client.GetStringAsync("api/v1/bookings/mine");

        using var doc = JsonDocument.Parse(json);
        var first = doc.RootElement.EnumerateArray().First();
        first.GetProperty("status").ValueKind.Should().Be(JsonValueKind.Number);
        first.GetProperty("status").GetInt32().Should().Be(0);   // Open
        first.GetProperty("date").GetString().Should().MatchRegex(@"^\d{4}-\d{2}-\d{2}$");
    }

    [Test]
    public async Task Pireps_Mine_EmptyForFreshPilot_Returns200List()
    {
        var (pilot, _, _) = await TestData.SeedAsync();
        var client = ApiTestHost.CreateClientFor(pilot);

        var resp = await client.GetAsync("api/v1/pireps/mine");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resp.Content.ReadFromJsonAsync<List<PirepListItem>>()).Should().BeEmpty();
    }

    [Test]
    public async Task Auth_Login_WrongPassword_Returns401_NotException()
    {
        var (pilot, _, _) = await TestData.SeedAsync();
        var client = ApiTestHost.Factory.CreateClient();

        var resp = await client.PostAsJsonAsync("api/v1/auth/login",
            new LoginRequest(pilot.Callsign, "wrong-password"));

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Anonymous_Routes_IsAllowed_ButBookingsIsNot()
    {
        var client = ApiTestHost.Factory.CreateClient();

        (await client.GetAsync("api/v1/routes")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("api/v1/bookings/mine")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
