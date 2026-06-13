using System.Windows;
using AirSerbiaVirtua.Acars.Core;
using AirSerbiaVirtua.Acars.Desktop.Services;
using AirSerbiaVirtua.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AirSerbiaVirtua.Acars.Desktop.ViewModels;

/// <summary>
/// Pilot Centre (Phase 3): shows the signed-in pilot's profile and provides a
/// change-password form. Profile data comes from the cached <see cref="PilotProfile"/>
/// on the session; it refreshes whenever auth state changes.
/// </summary>
public sealed partial class PilotCentreViewModel : ObservableObject
{
    private readonly ISessionService _session;
    private readonly FlightSessionState _flightState;
    private readonly DispatchService _dispatch;
    private BookingInfo? _dispatchBooking;

    // ---- W4 resume-dispatch card ----
    [ObservableProperty] private bool _hasDispatch;
    [ObservableProperty] private string _dispatchTitle = string.Empty;
    [ObservableProperty] private string _dispatchSub = string.Empty;
    [ObservableProperty] private string? _dispatchMessage;

    [ObservableProperty] private bool _isSignedIn;
    [ObservableProperty] private string _callsign = "—";
    [ObservableProperty] private string _name = "—";
    [ObservableProperty] private string _email = "—";
    [ObservableProperty] private string _rankName = "—";
    [ObservableProperty] private string _totalHours = "—";
    [ObservableProperty] private string _statusText = "—";
    [ObservableProperty] private string _hub = "—";
    [ObservableProperty] private string _joined = "—";
    [ObservableProperty] private string _initials = "··";

    // ---- Change-password form ----
    [ObservableProperty] private string _currentPassword = string.Empty;
    [ObservableProperty] private string _newPassword = string.Empty;
    [ObservableProperty] private string _confirmPassword = string.Empty;
    [ObservableProperty] private bool _isChangingPassword;
    [ObservableProperty] private string? _passwordMessage;
    [ObservableProperty] private bool _passwordError;

    public PilotCentreViewModel(ISessionService session, FlightSessionState flightState, DispatchService dispatch)
    {
        _session = session;
        _flightState = flightState;
        _dispatch = dispatch;
        _session.StateChanged += (_, _) =>
        {
            OnUi(Load);
            _ = RefreshDispatchAsync();
        };
        Load();
        _ = RefreshDispatchAsync();
    }

    private void Load()
    {
        var p = _session.Pilot;
        IsSignedIn = p is not null;
        if (p is null) return;

        Callsign = p.Callsign;
        Name = p.Name;
        Email = p.Email;
        RankName = string.IsNullOrWhiteSpace(p.RankName) ? "—" : p.RankName;
        TotalHours = $"{p.TotalHours:0.0} h";
        StatusText = p.Status.ToString();
        Hub = p.HubId;
        Joined = p.DateJoined.UtcDateTime.ToString("d MMM yyyy");
        Initials = MakeInitials(p.Name, p.Callsign);
    }

    /// <summary>
    /// W4 dispatch pull: shows the resume-dispatch card iff a web-prepared
    /// (dispatch-ready) booking dated today exists and no session is active.
    /// Failures are silent — the card is simply absent when offline.
    /// </summary>
    public async Task RefreshDispatchAsync()
    {
        try
        {
            if (!_session.IsAuthenticated || _flightState.HasActiveSession)
            {
                ClearDispatchCard();
                return;
            }

            var bookings = await _session.Api.GetActiveBookingsAsync();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var pick = bookings
                .Where(b => b.DispatchReadyAtUtc is not null && b.Date == today)
                .OrderByDescending(b => b.DispatchReadyAtUtc)
                .FirstOrDefault();

            if (pick is null)
            {
                ClearDispatchCard();
                return;
            }

            _dispatchBooking = pick;
            DispatchTitle = $"Dispatch ready: {pick.FlightNumber} {pick.DepIcao} → {pick.ArrIcao}";
            DispatchSub = $"{pick.AircraftType} · prepared {pick.DispatchReadyAtUtc:HH:mm}Z";
            HasDispatch = true;
        }
        catch
        {
            ClearDispatchCard();
        }
    }

    private void ClearDispatchCard()
    {
        _dispatchBooking = null;
        HasDispatch = false;
    }

    [RelayCommand]
    private async Task StartDispatchAsync()
    {
        if (_dispatchBooking is null) return;
        DispatchMessage = null;
        var result = await _dispatch.StartAsync(_dispatchBooking);
        if (result.Success)
            ClearDispatchCard();          // navigation to ACARS already happened
        else
            DispatchMessage = result.Message;
    }

    [RelayCommand(CanExecute = nameof(CanChangePassword))]
    private async Task ChangePasswordAsync()
    {
        PasswordMessage = null;
        PasswordError = false;

        if (NewPassword != ConfirmPassword)
        {
            PasswordError = true;
            PasswordMessage = "New password and confirmation do not match.";
            return;
        }
        if (NewPassword.Length < 8)
        {
            PasswordError = true;
            PasswordMessage = "New password must be at least 8 characters.";
            return;
        }

        IsChangingPassword = true;
        try
        {
            await _session.Api.ChangePasswordAsync(CurrentPassword, NewPassword);
            PasswordError = false;
            PasswordMessage = "Password changed. Other devices have been signed out.";
            CurrentPassword = NewPassword = ConfirmPassword = string.Empty;
        }
        catch (Exception ex)
        {
            PasswordError = true;
            PasswordMessage = ex.Message;
        }
        finally
        {
            IsChangingPassword = false;
            ChangePasswordCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanChangePassword() =>
        !IsChangingPassword && IsSignedIn &&
        !string.IsNullOrEmpty(CurrentPassword) &&
        !string.IsNullOrEmpty(NewPassword) &&
        !string.IsNullOrEmpty(ConfirmPassword);

    partial void OnCurrentPasswordChanged(string value) => ChangePasswordCommand.NotifyCanExecuteChanged();
    partial void OnNewPasswordChanged(string value) => ChangePasswordCommand.NotifyCanExecuteChanged();
    partial void OnConfirmPasswordChanged(string value) => ChangePasswordCommand.NotifyCanExecuteChanged();
    partial void OnIsChangingPasswordChanged(bool value) => ChangePasswordCommand.NotifyCanExecuteChanged();

    private static string MakeInitials(string name, string callsign)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2) return $"{char.ToUpper(parts[0][0])}{char.ToUpper(parts[^1][0])}";
        if (parts.Length == 1 && parts[0].Length >= 2) return parts[0][..2].ToUpper();
        return callsign.Length >= 2 ? callsign[..2].ToUpper() : "··";
    }

    private static void OnUi(Action a) => Application.Current?.Dispatcher.Invoke(a);
}
