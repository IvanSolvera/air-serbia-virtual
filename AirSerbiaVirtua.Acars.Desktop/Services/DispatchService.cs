using AirSerbiaVirtua.Acars.Core;
using AirSerbiaVirtua.Contracts;

namespace AirSerbiaVirtua.Acars.Desktop.Services;

public sealed record DispatchStartResult(bool Success, string? Message, string? Registration = null);

/// <summary>
/// The one start-flight sequence (W4): pick the first Active airframe of the
/// booking's type, open the server session, reset the sim state machine, set
/// the shared FlightSessionState and jump to ACARS Live. Used by the Bookings
/// page Start button and the Pilot Centre "Resume dispatch" card.
/// </summary>
public sealed class DispatchService
{
    private readonly ISessionService _session;
    private readonly FlightSessionState _flightState;
    private readonly INavigationService _navigation;
    private readonly SimulatorService _sim;

    public DispatchService(
        ISessionService session,
        FlightSessionState flightState,
        INavigationService navigation,
        SimulatorService sim)
    {
        _session = session;
        _flightState = flightState;
        _navigation = navigation;
        _sim = sim;
    }

    public async Task<DispatchStartResult> StartAsync(BookingInfo booking, CancellationToken ct = default)
    {
        if (!_session.IsAuthenticated)
            return new(false, "Sign in to start a flight.");
        if (_flightState.HasActiveSession)
            return new(false, "A flight session is already active.");

        try
        {
            // First Active airframe of the required type; the API returns
            // Conflict if it gets snatched in between — surfaced via Message.
            var fleet = await _session.Api.GetAircraftAsync(booking.AircraftType, "Active", ct);
            if (fleet.Count == 0)
                return new(false, $"No {booking.AircraftType} airframes are available right now.");

            var aircraft = fleet[0];
            var start = await _session.Api.StartFlightAsync(booking.RouteId, aircraft.Id, booking.Date, ct);

            _sim.ResetSession();
            _flightState.Set(start, booking.FlightNumber);
            _navigation.NavigateTo(NavTarget.Acars);
            return new(true, null, aircraft.Registration);
        }
        catch (Exception ex)
        {
            return new(false, ex.Message);
        }
    }
}
