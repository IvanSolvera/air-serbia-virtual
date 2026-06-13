using AirSerbiaVirtua.Acars.Core;
using AirSerbiaVirtua.Acars.Desktop.Services;
using AirSerbiaVirtua.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AirSerbiaVirtua.Acars.Desktop.ViewModels;

/// <summary>
/// Briefing (Phase 4): pre-flight summary for the active flight session — route,
/// distance, planned time, a rough fuel estimate, and live METARs for departure
/// and arrival. Loads on construction and via Refresh.
/// </summary>
public sealed partial class BriefingViewModel : ObservableObject
{
    private readonly ISessionService _session;
    private readonly FlightSessionState _flightState;

    [ObservableProperty] private bool _hasActiveSession;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _statusMessage;

    [ObservableProperty] private string _flightNumber = "—";
    [ObservableProperty] private string _legLabel = "—";
    [ObservableProperty] private string _aircraft = "—";
    [ObservableProperty] private string _distance = "—";
    [ObservableProperty] private string _plannedTime = "—";

    [ObservableProperty] private string _fuelTrip = "—";
    [ObservableProperty] private string _fuelReserve = "—";
    [ObservableProperty] private string _fuelTotal = "—";

    [ObservableProperty] private string _depIcao = "—";
    [ObservableProperty] private string _depMetar = "—";
    [ObservableProperty] private string _arrIcao = "—";
    [ObservableProperty] private string _arrMetar = "—";

    public BriefingViewModel(ISessionService session, FlightSessionState flightState)
    {
        _session = session;
        _flightState = flightState;
        _ = RefreshAsync();
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        HasActiveSession = _flightState.HasActiveSession;
        if (!HasActiveSession)
        {
            StatusMessage = "No active flight. Start a flight from Bookings to see its briefing.";
            return;
        }
        if (!_session.IsAuthenticated || _flightState.RouteId is not { } routeId)
        {
            StatusMessage = "Sign in to load the briefing.";
            return;
        }

        IsBusy = true;
        StatusMessage = null;
        try
        {
            var route = await _session.Api.GetRouteAsync(routeId);

            FlightNumber = route.FlightNumber;
            LegLabel = $"{route.DepIcao} → {route.ArrIcao}";
            Aircraft = $"{route.AircraftType} · {_flightState.Registration}";
            Distance = $"{route.DistanceNm} nm";
            PlannedTime = $"{route.PlannedMinutes / 60}h {route.PlannedMinutes % 60:D2}m";
            DepIcao = route.DepIcao;
            ArrIcao = route.ArrIcao;

            var (trip, reserve) = FuelEstimator.Estimate(route.AircraftType, route.DistanceNm);
            FuelTrip = $"{trip:N0} kg";
            FuelReserve = $"{reserve:N0} kg";
            FuelTotal = $"{trip + reserve:N0} kg";

            DepMetar = ArrMetar = "Loading…";
            try
            {
                var metars = await _session.Api.GetMetarAsync(new[] { route.DepIcao, route.ArrIcao });
                DepMetar = FindMetar(metars, route.DepIcao);
                ArrMetar = FindMetar(metars, route.ArrIcao);
            }
            catch
            {
                DepMetar = ArrMetar = "METAR unavailable.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string FindMetar(IEnumerable<MetarInfo> metars, string icao)
    {
        var m = metars.FirstOrDefault(x => string.Equals(x.Icao, icao, StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrWhiteSpace(m?.Raw) ? "No report available." : m!.Raw!;
    }
}
