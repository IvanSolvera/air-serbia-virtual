using System.Net;
using System.Net.Http.Json;
using AirSerbiaVirtua.Contracts;
using FluentAssertions;

namespace AirSerbiaVirtua.Tests.Api;

public class DispatchEndpointTests
{
    // ---- POST /bookings/{id}/dispatch ---------------------------------------

    [Test]
    public async Task MarkReady_OnOpenBooking_SetsTimestampAndReturnsBooking()
    {
        var (pilot, route, _) = await TestData.SeedAsync();
        var booking = await TestData.SeedBookingAsync(pilot.Id, route.Id);
        var client = ApiTestHost.CreateClientFor(pilot);

        var resp = await client.PostAsync($"api/v1/bookings/{booking.Id}/dispatch", null);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = (await resp.Content.ReadFromJsonAsync<BookingInfo>())!;
        dto.Id.Should().Be(booking.Id);
        dto.DispatchReadyAtUtc.Should().NotBeNull()
            .And.BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(10));
    }

    [Test]
    public async Task MarkReady_Twice_IsIdempotentAndRefreshesTimestamp()
    {
        var (pilot, route, _) = await TestData.SeedAsync();
        var booking = await TestData.SeedBookingAsync(pilot.Id, route.Id);
        var client = ApiTestHost.CreateClientFor(pilot);

        var first = await (await client.PostAsync($"api/v1/bookings/{booking.Id}/dispatch", null))
            .Content.ReadFromJsonAsync<BookingInfo>();
        await Task.Delay(50);
        var secondResp = await client.PostAsync($"api/v1/bookings/{booking.Id}/dispatch", null);

        secondResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var second = await secondResp.Content.ReadFromJsonAsync<BookingInfo>();
        second!.DispatchReadyAtUtc.Should().BeOnOrAfter(first!.DispatchReadyAtUtc!.Value);
    }

    [Test]
    public async Task MarkReady_OnFlownBooking_Returns409()
    {
        var (pilot, route, _) = await TestData.SeedAsync();
        var booking = await TestData.SeedBookingAsync(pilot.Id, route.Id, BookingStatus.Flown);
        var client = ApiTestHost.CreateClientFor(pilot);

        var resp = await client.PostAsync($"api/v1/bookings/{booking.Id}/dispatch", null);

        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task MarkReady_OnAnotherPilotsBooking_Returns404()
    {
        var (owner, route, _) = await TestData.SeedAsync();
        var (other, _, _) = await TestData.SeedAsync();
        var booking = await TestData.SeedBookingAsync(owner.Id, route.Id);
        var client = ApiTestHost.CreateClientFor(other);

        var resp = await client.PostAsync($"api/v1/bookings/{booking.Id}/dispatch", null);

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task MarkReady_Anonymous_Returns401()
    {
        var client = ApiTestHost.Factory.CreateClient();
        var resp = await client.PostAsync("api/v1/bookings/1/dispatch", null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---- DELETE /bookings/{id}/dispatch -------------------------------------

    [Test]
    public async Task ClearReady_RemovesFlag_Returns204()
    {
        var (pilot, route, _) = await TestData.SeedAsync();
        var booking = await TestData.SeedBookingAsync(
            pilot.Id, route.Id, dispatchReadyAtUtc: DateTimeOffset.UtcNow);
        var client = ApiTestHost.CreateClientFor(pilot);

        var resp = await client.DeleteAsync($"api/v1/bookings/{booking.Id}/dispatch");

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var mine = await client.GetFromJsonAsync<List<BookingInfo>>("api/v1/bookings/mine");
        mine!.Single(b => b.Id == booking.Id).DispatchReadyAtUtc.Should().BeNull();
    }

    [Test]
    public async Task ClearReady_OnCancelledBooking_Returns409()
    {
        var (pilot, route, _) = await TestData.SeedAsync();
        var booking = await TestData.SeedBookingAsync(pilot.Id, route.Id, BookingStatus.Cancelled);
        var client = ApiTestHost.CreateClientFor(pilot);

        var resp = await client.DeleteAsync($"api/v1/bookings/{booking.Id}/dispatch");

        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ---- GET /bookings/active -------------------------------------------------

    [Test]
    public async Task Active_ReturnsOnlyOpenAndConfirmed_OrderedReadyFirst()
    {
        var (pilot, route, _) = await TestData.SeedAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // NOTE: distinct dates per booking — (PilotId, RouteId, Date) is a unique
        // index, so reusing one date for several bookings of the same route throws.
        // Ordering is by dispatch-ready (desc) first, so the dates below don't
        // affect the expected order [readyNew, readyOld, open].
        var open       = await TestData.SeedBookingAsync(pilot.Id, route.Id, BookingStatus.Open,      today.AddDays(2));
        var flown      = await TestData.SeedBookingAsync(pilot.Id, route.Id, BookingStatus.Flown,     today.AddDays(3));
        var cancelled  = await TestData.SeedBookingAsync(pilot.Id, route.Id, BookingStatus.Cancelled, today.AddDays(4));
        var readyOld   = await TestData.SeedBookingAsync(pilot.Id, route.Id, BookingStatus.Open,      today,
            dispatchReadyAtUtc: DateTimeOffset.UtcNow.AddMinutes(-30));
        var readyNew   = await TestData.SeedBookingAsync(pilot.Id, route.Id, BookingStatus.Confirmed, today.AddDays(1),
            dispatchReadyAtUtc: DateTimeOffset.UtcNow);
        var client = ApiTestHost.CreateClientFor(pilot);

        var active = await client.GetFromJsonAsync<List<BookingInfo>>("api/v1/bookings/active");

        active!.Select(b => b.Id).Should().Equal(readyNew.Id, readyOld.Id, open.Id);
        active.Should().NotContain(b => b.Id == flown.Id || b.Id == cancelled.Id);
    }

    [Test]
    public async Task Active_DoesNotLeakOtherPilotsBookings()
    {
        var (pilot, route, _) = await TestData.SeedAsync();
        var (other, otherRoute, _) = await TestData.SeedAsync();
        await TestData.SeedBookingAsync(other.Id, otherRoute.Id);
        var client = ApiTestHost.CreateClientFor(pilot);

        var active = await client.GetFromJsonAsync<List<BookingInfo>>("api/v1/bookings/active");

        active.Should().BeEmpty();
    }
}
