using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using AirSerbiaVirtua.Acars.Core;
using AirSerbiaVirtua.Acars.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AirSerbiaVirtua.Acars.Desktop.ViewModels;

/// <summary>
/// Shell view-model. Owns the active page (via <see cref="INavigationService"/>),
/// the side-nav availability (which depends on auth state), and the status-bar
/// bindings (LEDs, Zulu clock, API ping) shown in the footer.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly ISessionService _session;
    private readonly INavigationService _navigation;
    private readonly SimulatorService _sim;
    private readonly FlightSessionState _flight;
    private readonly DispatcherTimer _clock;
    private readonly DispatcherTimer _ping;

    [ObservableProperty] private bool _isAuthenticated;
    [ObservableProperty] private bool _isAdmin;

    // ---- Status bar ------------------------------------------------------------
    [ObservableProperty] private string _serverStatusText = "Offline";
    [ObservableProperty] private bool _isApiOnline;
    [ObservableProperty] private string _simStatusText = "Disconnected";
    [ObservableProperty] private bool _isSimConnected;
    [ObservableProperty] private string _acarsStatusText = "Standby";
    [ObservableProperty] private bool _isAcarsActive;
    [ObservableProperty] private string _hubText = "LYBE · Belgrade";
    [ObservableProperty] private string _zuluText = "--:--:--Z";
    [ObservableProperty] private string _pingText = "—";

    // ---- Titlebar / sidebar ------------------------------------------------------
    [ObservableProperty] private string _pilotCardName = "Not signed in";
    [ObservableProperty] private string _pilotCardSub = "Sign in to begin duty";
    [ObservableProperty] private string _pilotInitials = "··";

    public string VersionText { get; } =
        $"v{Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0"}";

    public INavigationService Navigation => _navigation;

    public MainViewModel(
        ISessionService session,
        INavigationService navigation,
        SimulatorService sim,
        FlightSessionState flight)
    {
        _session = session;
        _navigation = navigation;
        _sim = sim;
        _flight = flight;

        _session.StateChanged += OnSessionChanged;
        _sim.ConnectionStateChanged += OnSimConnectionChanged;
        _flight.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(FlightSessionState.HasActiveSession) or null)
                OnFlightSessionChanged();
        };

        _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clock.Tick += (_, _) => ZuluText = DateTime.UtcNow.ToString("HH:mm:ss") + "Z";
        _clock.Start();

        _ping = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _ping.Tick += async (_, _) => await PingApiAsync();
        _ping.Start();
        _ = PingApiAsync();   // first sample immediately, not after 15 s

        OnSessionChanged(this, EventArgs.Empty);
    }

    private async Task PingApiAsync()
    {
        var ms = await _session.Api.PingAsync();
        IsApiOnline = ms is not null;
        ServerStatusText = ms is not null ? "Connected" : "Offline";
        PingText = ms is not null ? $"{ms} ms" : "—";
    }

    private void OnSimConnectionChanged(bool connected) =>
        Application.Current?.Dispatcher.Invoke(() =>
        {
            IsSimConnected = connected;
            SimStatusText = connected ? "Connected" : "Disconnected";
        });

    private void OnFlightSessionChanged() =>
        Application.Current?.Dispatcher.Invoke(() =>
        {
            IsAcarsActive = _flight.HasActiveSession;
            AcarsStatusText = _flight.HasActiveSession
                ? $"Active · {_flight.FlightNumber}"
                : "Standby";
        });

    // Sidebar is locked until the pilot signs in — the suite design treats every
    // page as crew-only. Logout drops straight back to the login screen.
    [RelayCommand]
    private void NavigateLogin() => _navigation.NavigateTo(NavTarget.Login);

    [RelayCommand(CanExecute = nameof(IsAuthenticated))]
    private void NavigateMetars() => _navigation.NavigateTo(NavTarget.Metars);

    [RelayCommand(CanExecute = nameof(IsAuthenticated))]
    private void NavigatePilotCentre() => _navigation.NavigateTo(NavTarget.PilotCentre);

    [RelayCommand(CanExecute = nameof(IsAuthenticated))]
    private void NavigateBookings() => _navigation.NavigateTo(NavTarget.Bookings);

    [RelayCommand(CanExecute = nameof(IsAuthenticated))]
    private void NavigateBriefing() => _navigation.NavigateTo(NavTarget.Briefing);

    [RelayCommand(CanExecute = nameof(IsAuthenticated))]
    private void NavigateAcars() => _navigation.NavigateTo(NavTarget.Acars);

    [RelayCommand(CanExecute = nameof(IsAuthenticated))]
    private void NavigateDebriefing() => _navigation.NavigateTo(NavTarget.Debriefing);

    [RelayCommand(CanExecute = nameof(IsAuthenticated))]
    private void NavigateOutstation() => _navigation.NavigateTo(NavTarget.Outstation);

    [RelayCommand(CanExecute = nameof(IsAuthenticated))]
    private void NavigateLogbook() => _navigation.NavigateTo(NavTarget.Logbook);

    [RelayCommand(CanExecute = nameof(IsAdmin))]
    private void NavigateAdmin() => _navigation.NavigateTo(NavTarget.Admin);

    [RelayCommand(CanExecute = nameof(IsAuthenticated))]
    private async Task LogoutAsync()
    {
        await _session.LogoutAsync();
        _navigation.NavigateTo(NavTarget.Login);
    }

    private void OnSessionChanged(object? sender, EventArgs e)
    {
        IsAuthenticated = _session.IsAuthenticated;
        IsAdmin = IsAuthenticated && _session.Pilot?.IsAdmin == true;

        if (_session.Pilot is { } p)
        {
            PilotCardName = $"{p.Callsign} — {p.Name}";
            PilotCardSub = string.IsNullOrEmpty(p.RankName) ? p.HubId : $"{p.RankName} · {p.HubId}";
            HubText = $"{p.HubId} · Belgrade";
            PilotInitials = MakeInitials(p.Name, p.Callsign);
        }
        else
        {
            PilotCardName = "Not signed in";
            PilotCardSub = "Sign in to begin duty";
            HubText = "LYBE · Belgrade";
            PilotInitials = "··";
        }

        NavigateMetarsCommand.NotifyCanExecuteChanged();
        NavigatePilotCentreCommand.NotifyCanExecuteChanged();
        NavigateBookingsCommand.NotifyCanExecuteChanged();
        NavigateBriefingCommand.NotifyCanExecuteChanged();
        NavigateAcarsCommand.NotifyCanExecuteChanged();
        NavigateDebriefingCommand.NotifyCanExecuteChanged();
        NavigateOutstationCommand.NotifyCanExecuteChanged();
        NavigateLogbookCommand.NotifyCanExecuteChanged();
        NavigateAdminCommand.NotifyCanExecuteChanged();
        LogoutCommand.NotifyCanExecuteChanged();

        // Auto-jump to the Pilot Centre when login succeeds.
        if (IsAuthenticated && _navigation.Current == NavTarget.Login)
            _navigation.NavigateTo(NavTarget.PilotCentre);
    }

    private static string MakeInitials(string name, string callsign)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2) return $"{char.ToUpper(parts[0][0])}{char.ToUpper(parts[^1][0])}";
        if (parts.Length == 1 && parts[0].Length >= 2) return parts[0][..2].ToUpper();
        return callsign.Length >= 2 ? callsign[..2].ToUpper() : "··";
    }
}
