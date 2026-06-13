using AirSerbiaVirtua.Contracts;
using AirSerbiaVirtua.Web.Client.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace AirSerbiaVirtua.Tests.Web;

/// <summary>
/// bUnit context pre-wired with a signed-in PortalSession (loose JSInterop stands
/// in for localStorage) and a Moq IPortalApi. (bUnit 2.x: base is BunitContext.)
/// </summary>
public class PortalTestContext : BunitContext
{
    public Mock<IPortalApi> Api { get; } = new();
    public PortalSession Session { get; }

    protected static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    public PortalTestContext()
    {
        // Fuel figures render with N0 ("2,100") — pin the culture so assertions
        // don't depend on the OS locale (Serbian would format "2.100").
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;

        JSInterop.Mode = JSRuntimeMode.Loose;
        Session = new PortalSession(JSInterop.JSRuntime);
        Services.AddSingleton(Session);
        Services.AddSingleton(Api.Object);
    }

    /// <summary>Signs the fake session in (must run before rendering a portal page).</summary>
    public async Task SignInAsync()
    {
        var tokens = new AuthTokens("at", DateTimeOffset.UtcNow.AddHours(1), "rt", DateTimeOffset.UtcNow.AddDays(7));
        var pilot = new PilotProfile(1, "ASL001", "Test Pilot", "t@asv.test",
            2, "First Officer", 12.5m, PilotStatus.Active, "LYBE", DateTimeOffset.UtcNow);
        await Session.SetAsync(tokens, pilot);
    }

    public static BookingInfo Booking(int id, DateTimeOffset? ready = null,
        BookingStatus status = BookingStatus.Open) => new(
        id, 3, $"JU{id:000}", "LYBE", "LOWW", "A319", 55, Today, status, ready);
}
