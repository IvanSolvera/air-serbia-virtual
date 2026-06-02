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

    [ObservableProperty] private string _pilotStatusText = "<not signed in>";
    [ObservableProperty] private string _serverStatusText = "Offline";
    [ObservableProperty] private string _simStatusText = "-";
    [ObservableProperty] private bool _isAuthenticated;

    public INavigationService Navigation => _navigation;

    public MainViewModel(ISessionService session, INavigationService navigation)
    {
        _session = session;
        _navigation = navigation;
        _session.StateChanged += OnSessionChanged;
        OnSessionChanged(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void NavigateLogin() => _navigation.NavigateTo(NavTarget.Login);

    [RelayCommand(CanExecute = nameof(IsAuthenticated))]
    private void NavigatePilotCentre() => _navigation.NavigateTo(NavTarget.PilotCentre);

    [RelayCommand(CanExecute = nameof(IsAuthenticated))]
    private void NavigateBookings() => _navigation.NavigateTo(NavTarget.Bookings);

    [RelayCommand(CanExecute = nameof(IsAuthenticated))]
    private void NavigateAcars() => _navigation.NavigateTo(NavTarget.Acars);

    [RelayCommand(CanExecute = nameof(IsAuthenticated))]
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

        NavigatePilotCentreCommand.NotifyCanExecuteChanged();
        NavigateBookingsCommand.NotifyCanExecuteChanged();
        NavigateAcarsCommand.NotifyCanExecuteChanged();
        NavigateLogbookCommand.NotifyCanExecuteChanged();
        LogoutCommand.NotifyCanExecuteChanged();

        // Auto-jump to the Pilot Centre when login succeeds.
        if (IsAuthenticated && _navigation.Current == NavTarget.Login)
            _navigation.NavigateTo(NavTarget.PilotCentre);
    }
}
