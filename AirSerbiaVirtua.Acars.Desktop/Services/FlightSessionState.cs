using AirSerbiaVirtua.Acars.Core;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AirSerbiaVirtua.Acars.Desktop.Services;

/// <summary>
/// Process-wide handle to the currently active flight session. Owned by the
/// <see cref="BookingsViewModel"/> after a successful Start-flight call, read
/// by <see cref="ViewModels.AcarsViewModel"/> to scope POSREP push and PIREP
/// submission to that session.
/// </summary>
public sealed partial class FlightSessionState : ObservableObject
{
    [ObservableProperty] private int? _flightSessionId;
    [ObservableProperty] private string? _flightNumber;
    [ObservableProperty] private string? _registration;
    [ObservableProperty] private int? _routeId;
    [ObservableProperty] private int? _aircraftId;
    [ObservableProperty] private DateTimeOffset? _startedAtUtc;

    public bool HasActiveSession => FlightSessionId is not null;

    public void Set(FlightStartResponse start, string flightNumber)
    {
        FlightSessionId = start.FlightSessionId;
        FlightNumber = flightNumber;
        Registration = start.AircraftRegistration;
        RouteId = start.RouteId;
        AircraftId = start.AircraftId;
        StartedAtUtc = start.StartedAtUtc;
        OnPropertyChanged(nameof(HasActiveSession));
    }

    public void Clear()
    {
        FlightSessionId = null;
        FlightNumber = null;
        Registration = null;
        RouteId = null;
        AircraftId = null;
        StartedAtUtc = null;
        OnPropertyChanged(nameof(HasActiveSession));
    }
}
