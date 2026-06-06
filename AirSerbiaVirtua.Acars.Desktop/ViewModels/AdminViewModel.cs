using System.Collections.ObjectModel;
using System.Windows;
using AirSerbiaVirtua.Acars.Core;
using AirSerbiaVirtua.Acars.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AirSerbiaVirtua.Acars.Desktop.ViewModels;

/// <summary>
/// Admin roster page (Phase 5): lists every pilot (Pending first) and lets an
/// administrator approve registrations or deactivate accounts. The tab is only
/// reachable when the signed-in pilot carries the Admin role.
/// </summary>
public sealed partial class AdminViewModel : ObservableObject
{
    private readonly ISessionService _session;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private string _rosterSummary = "—";

    public ObservableCollection<AdminPilotRow> Pilots { get; } = [];

    public AdminViewModel(ISessionService session)
    {
        _session = session;
        _ = RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        StatusMessage = null;
        HasError = false;
        try
        {
            var roster = await _session.Api.GetAdminPilotsAsync();
            var myId = _session.Pilot?.Id;

            Pilots.Clear();
            foreach (var p in roster)
                Pilots.Add(AdminPilotRow.From(p, myId));

            var pending = roster.Count(p => p.Status == 0);
            RosterSummary = $"{roster.Count} pilots · {pending} awaiting approval";
        }
        catch (Exception ex)
        {
            HasError = true;
            StatusMessage = $"Could not load the roster: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ActivateAsync(AdminPilotRow row)
    {
        await RunRosterActionAsync(
            () => _session.Api.ActivatePilotAsync(row.Id),
            $"{row.Callsign} activated.");
    }

    [RelayCommand]
    private async Task DeactivateAsync(AdminPilotRow row)
    {
        var confirm = MessageBox.Show(
            $"Deactivate {row.Callsign} — {row.Name}?\n\nThey will no longer be able to sign in; " +
            "their logbook and history are kept.",
            "Deactivate pilot",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        await RunRosterActionAsync(
            () => _session.Api.DeactivatePilotAsync(row.Id),
            $"{row.Callsign} deactivated.");
    }

    private async Task RunRosterActionAsync(Func<Task> action, string successMessage)
    {
        IsBusy = true;
        StatusMessage = null;
        HasError = false;
        try
        {
            await action();
            StatusMessage = successMessage;
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            HasError = true;
            StatusMessage = ex.Message;
            IsBusy = false;
        }
    }
}

public sealed record AdminPilotRow(
    int Id,
    string Callsign,
    string Name,
    string Email,
    string RankName,
    string HoursLabel,
    string StatusLabel,
    string JoinedLabel,
    bool IsAdmin,
    bool CanActivate,
    bool CanDeactivate)
{
    public static AdminPilotRow From(AdminPilot p, int? myId)
    {
        var status = (Status)p.Status;
        return new AdminPilotRow(
            p.Id,
            p.Callsign,
            p.Name,
            p.Email,
            string.IsNullOrWhiteSpace(p.RankName) ? "—" : p.RankName,
            $"{p.TotalHours:0.0} h",
            status.ToString(),
            p.DateJoined.UtcDateTime.ToString("d MMM yyyy"),
            p.IsAdmin,
            CanActivate: status is Status.Pending or Status.Inactive,
            CanDeactivate: status == Status.Active && !p.IsAdmin && p.Id != myId);
    }

    /// <summary>Mirror of the API PilotStatus enum.</summary>
    private enum Status { Pending = 0, Active = 1, OnLeave = 2, Inactive = 3, Banned = 4 }
}
