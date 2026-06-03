using System.Windows;
using AirSerbiaVirtua.Acars.Core;
using AirSerbiaVirtua.Acars.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AirSerbiaVirtua.Acars.Desktop.ViewModels;

/// <summary>
/// Shell view-model. Owns the active page (via <see cref="INavigationService"/>),
/// the side-nav availability (which depends on auth state), and the status-bar
/// bindings shown in the footer.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly ISessionService _session;
    private readonly INavigationService _navigation;
    private readonly SimulatorService _sim;

    [ObservableProperty] private string _pilotStatusText = "<not signed in>";
    [ObservableProperty] private string _serverStatusText = "Offline";
    [ObservableProperty] private string _simStatusText = "Disconnected";
    [ObservableProperty] private bool _isAuthenticated;

    public INavigationService Navigation => _navigation;

    public MainViewModel(ISessionService session, INavigationService navigation, SimulatorService sim)
    {
        _session = session;
        _navigation = navigation;
        _sim = sim;
        _session.StateChanged += OnSessionChanged;
        _sim.ConnectionStateChanged += OnSimConnectionChanged;
        OnSessionChanged(this, EventArgs.Empty);
    }

    private void OnSimConnectionChanged(bool connected) =>
        Application.Current?.Dispatcher.Invoke(() =>
            SimStatusText = connected ? "Connected" : "Disconnected");

    // Phase 2a: nav is open so the live ACARS page works without API. The Pilot
    // Centre / Bookings / Logbook are still placeholders; ACARS Live is functional
    // (FSUIPC telemetry + state machine). Lock back to IsAuthenticated when the
    // pilot-centric pages are wired up end-to-end.
    [RelayCommand]
    private void NavigateLogin() => _navigation.NavigateTo(NavTarget.Login);

    [RelayCommand]
    private void NavigatePilotCentre() => _navigation.NavigateTo(NavTarget.PilotCentre);

    [RelayCommand]
    private void NavigateBookings() => _navigation.NavigateTo(NavTarget.Bookings);

    [RelayCommand]
    private void NavigateAcars() => _navigation.NavigateTo(NavTarget.Acars);

    [RelayCommand]
    private void NavigateLogbook() => _navigation.NavigateTo(NavTarget.Logbook);

    [RelayCommand(CanExecute = nameof(IsAuthenticated))]
    private async Task LogoutAsync()
    {
        await _session.LogoutAsync();
        _navigation.NavigateTo(NavTarget.Login);
    }

    private void OnSessionChanged(object? sender, EventArgs e)
    {
        IsAuthenticated = _session.IsAuthenticated;
        PilotStatusText = _session.Pilot is { } p ? $"{p.Callsign} — {p.Name}" : "<not signed in>";
        ServerStatusText = _session.IsApiReachable ? "Online" : "Offline";

        LogoutCommand.NotifyCanExecuteChanged();

        // Auto-jump to the Pilot Centre when login succeeds.
        if (IsAuthenticated && _navigation.Current == NavTarget.Login)
            _navigation.NavigateTo(NavTarget.PilotCentre);
    }
}
