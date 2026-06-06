using System.Collections.ObjectModel;
using System.Windows;
using AirSerbiaVirtua.Acars.Core;
using AirSerbiaVirtua.Acars.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AirSerbiaVirtua.Acars.Desktop.ViewModels;

/// <summary>
/// Outstation Flights (Phase 5): ad-hoc charter legs outside the published
/// schedule. The pilot picks any two ICAOs and an available airframe; the server
/// creates a one-off route + Pending PIREP session and the flight proceeds
/// through ACARS exactly like a scheduled leg (no booking involved).
/// </summary>
public sealed partial class OutstationViewModel : ObservableObject
{
    private readonly ISessionService _session;
    private readonly INavigationService _navigation;
    private readonly FlightSessionState _flightState;
    private readonly SimulatorService _sim;

    [ObservableProperty] private string _flightNumber;
    [ObservableProperty] private string _depIcao = "LYBE";
    [ObservableProperty] private string _arrIcao = string.Empty;
    [ObservableProperty] private ApiAircraft? _selectedAircraft;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _hasError;

    public ObservableCollection<ApiAircraft> Fleet { get; } = [];

    public OutstationViewModel(
        ISessionService session,
        INavigationService navigation,
        FlightSessionState flightState,
        SimulatorService sim)
    {
        _session = session;
        _navigation = navigation;
        _flightState = flightState;
        _sim = sim;

        // Charter-style default in the JU9xxx range; freely editable.
        _flightNumber = $"JU9{Random.Shared.Next(0, 100):00}";

        _ = LoadFleetAsync();
    }

    private async Task LoadFleetAsync()
    {
        try
        {
            var fleet = await _session.Api.GetAircraftAsync(status: "Active");
            OnUi(() =>
            {
                Fleet.Clear();
                foreach (var ac in fleet.OrderBy(a => a.Type).ThenBy(a => a.Registration))
                    Fleet.Add(ac);
                SelectedAircraft ??= Fleet.FirstOrDefault();
                StartFlightCommand.NotifyCanExecuteChanged();
            });
        }
        catch (Exception ex)
        {
            OnUi(() =>
            {
                HasError = true;
                StatusMessage = $"Could not load the fleet: {ex.Message}";
            });
        }
    }

    [RelayCommand]
    private Task RefreshAsync() => LoadFleetAsync();

    [RelayCommand(CanExecute = nameof(CanStartFlight))]
    private async Task StartFlightAsync()
    {
        if (SelectedAircraft is not { } aircraft) return;

        IsBusy = true;
        StatusMessage = null;
        HasError = false;
        try
        {
            var request = new OutstationStartRequest(
                DepIcao.Trim().ToUpperInvariant(),
                ArrIcao.Trim().ToUpperInvariant(),
                aircraft.Id,
                FlightNumber.Trim().ToUpperInvariant());

            var start = await _session.Api.StartOutstationFlightAsync(request);

            _sim.ResetSession();
            _flightState.Set(start, request.FlightNumber);

            StatusMessage = $"Outstation {request.FlightNumber} started on {aircraft.Registration}. Switching to ACARS Live.";
            _navigation.NavigateTo(NavTarget.Acars);
        }
        catch (Exception ex)
        {
            HasError = true;
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanStartFlight() =>
        !IsBusy &&
        !_flightState.HasActiveSession &&
        SelectedAircraft is not null &&
        IsIcao(DepIcao) && IsIcao(ArrIcao) &&
        !string.Equals(DepIcao.Trim(), ArrIcao.Trim(), StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(FlightNumber);

    private static bool IsIcao(string raw)
    {
        var s = raw.Trim();
        return s.Length == 4 && s.All(char.IsLetter);
    }

    partial void OnFlightNumberChanged(string value) => StartFlightCommand.NotifyCanExecuteChanged();
    partial void OnDepIcaoChanged(string value) => StartFlightCommand.NotifyCanExecuteChanged();
    partial void OnArrIcaoChanged(string value) => StartFlightCommand.NotifyCanExecuteChanged();
    partial void OnSelectedAircraftChanged(ApiAircraft? value) => StartFlightCommand.NotifyCanExecuteChanged();
    partial void OnIsBusyChanged(bool value) => StartFlightCommand.NotifyCanExecuteChanged();

    private static void OnUi(Action a) => Application.Current?.Dispatcher.Invoke(a);
}
