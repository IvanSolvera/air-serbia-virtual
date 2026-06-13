using System.Collections.ObjectModel;
using AirSerbiaVirtua.Acars.Core;
using AirSerbiaVirtua.Acars.Desktop.Services;
using AirSerbiaVirtua.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AirSerbiaVirtua.Acars.Desktop.ViewModels;

/// <summary>
/// Logbook (Phase 3): lists the pilot's submitted PIREPs from
/// <c>GET /api/v1/pireps/mine</c>, filterable by status, with summary totals.
/// </summary>
public sealed partial class LogbookViewModel : ObservableObject
{
    private readonly ISessionService _session;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private string _selectedFilter = "All";
    [ObservableProperty] private string _totalsText = "â€”";

    public string[] Filters { get; } = { "All", "Accepted", "Pending", "Rejected", "UnderReview" };

    public ObservableCollection<PirepRow> Pireps { get; } = new();

    public LogbookViewModel(ISessionService session)
    {
        _session = session;
        _ = RefreshAsync();
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (!_session.IsAuthenticated)
        {
            Pireps.Clear();
            TotalsText = "â€”";
            HasError = false;
            StatusMessage = "Sign in to view your logbook.";
            return;
        }

        IsBusy = true;
        StatusMessage = null;
        HasError = false;
        try
        {
            var filter = SelectedFilter == "All" ? null : SelectedFilter;
            var items = await _session.Api.GetMyPirepsAsync(filter);

            Pireps.Clear();
            foreach (var p in items) Pireps.Add(new PirepRow(p));

            var accepted = items.Where(i => i.Status == PirepStatus.Accepted).ToList();
            var totalMin = accepted.Sum(i => i.BlockMin);
            var avgScore = accepted.Count > 0 ? (int)Math.Round(accepted.Average(i => i.Score)) : 0;
            TotalsText = $"{items.Count} flight{(items.Count == 1 ? "" : "s")}  â€¢  "
                       + $"{totalMin / 60}h {totalMin % 60:D2}m logged  â€¢  avg score {avgScore}";

            if (items.Count == 0)
                StatusMessage = "No flights logged yet.";
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

    partial void OnSelectedFilterChanged(string value) => _ = RefreshAsync();
}

public sealed class PirepRow
{
    public int Id { get; }
    public string FlightNumber { get; }
    public string LegLabel { get; }
    public string Registration { get; }
    public string DateLabel { get; }
    public string BlockLabel { get; }
    public string AirLabel { get; }
    public string LandingLabel { get; }
    public int Score { get; }
    public string StatusLabel { get; }

    public PirepRow(PirepListItem p)
    {
        Id = p.Id;
        FlightNumber = p.FlightNumber;
        LegLabel = $"{p.DepIcao} â†’ {p.ArrIcao}";
        Registration = p.AircraftRegistration;
        DateLabel = p.DepActual.UtcDateTime.ToString("d MMM yyyy");
        BlockLabel = $"{p.BlockMin / 60}h {p.BlockMin % 60:D2}m";
        AirLabel = $"{p.AirMin / 60}h {p.AirMin % 60:D2}m";
        LandingLabel = p.LandingRateFpm != 0 ? $"{p.LandingRateFpm} fpm" : "â€”";
        Score = p.Score;
        StatusLabel = p.Status.ToString();
    }
}
