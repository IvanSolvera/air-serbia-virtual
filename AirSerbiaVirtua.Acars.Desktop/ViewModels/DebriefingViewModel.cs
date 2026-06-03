using AirSerbiaVirtua.Acars.Core;
using AirSerbiaVirtua.Acars.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AirSerbiaVirtua.Acars.Desktop.ViewModels;

/// <summary>
/// Debriefing (Phase 4): post-flight analysis of the most recently submitted
/// PIREP — score, landing quality, block/air time and fuel.
/// </summary>
public sealed partial class DebriefingViewModel : ObservableObject
{
    private readonly ISessionService _session;

    [ObservableProperty] private bool _hasPirep;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _statusMessage;

    [ObservableProperty] private string _flightNumber = "—";
    [ObservableProperty] private string _legLabel = "—";
    [ObservableProperty] private string _aircraft = "—";
    [ObservableProperty] private string _dateLabel = "—";
    [ObservableProperty] private int _score;
    [ObservableProperty] private string _scoreGrade = "—";
    [ObservableProperty] private string _landingRate = "—";
    [ObservableProperty] private string _landingVerdict = "—";
    [ObservableProperty] private string _blockTime = "—";
    [ObservableProperty] private string _airTime = "—";
    [ObservableProperty] private string _fuelUsed = "—";
    [ObservableProperty] private string _statusLabel = "—";

    public DebriefingViewModel(ISessionService session)
    {
        _session = session;
        _ = RefreshAsync();
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (!_session.IsAuthenticated)
        {
            HasPirep = false;
            StatusMessage = "Sign in to view your latest debrief.";
            return;
        }

        IsBusy = true;
        StatusMessage = null;
        try
        {
            var items = await _session.Api.GetMyPirepsAsync();
            var p = items.FirstOrDefault();   // newest first
            if (p is null)
            {
                HasPirep = false;
                StatusMessage = "No flights to debrief yet.";
                return;
            }

            HasPirep = true;
            FlightNumber = p.FlightNumber;
            LegLabel = $"{p.DepIcao} → {p.ArrIcao}";
            Aircraft = $"{p.AircraftType} · {p.AircraftRegistration}";
            DateLabel = p.DepActual.UtcDateTime.ToString("d MMM yyyy HH:mm") + " UTC";
            Score = p.Score;
            ScoreGrade = Grade(p.Score);
            LandingRate = p.LandingRateFpm != 0 ? $"{p.LandingRateFpm} fpm" : "—";
            LandingVerdict = LandingQuality(p.LandingRateFpm);
            BlockTime = $"{p.BlockMin / 60}h {p.BlockMin % 60:D2}m";
            AirTime = $"{p.AirMin / 60}h {p.AirMin % 60:D2}m";
            FuelUsed = $"{p.FuelUsedKg:N0} kg";
            StatusLabel = ((PirepStatusLabel)p.Status).ToString();
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

    private static string Grade(int score) => score switch
    {
        >= 95 => "Excellent",
        >= 85 => "Very good",
        >= 70 => "Good",
        >= 50 => "Acceptable",
        _ => "Needs work"
    };

    private static string LandingQuality(int fpm)
    {
        int a = Math.Abs(fpm);
        return a switch
        {
            0 => "—",
            <= 100 => "Butter — greaser landing.",
            <= 200 => "Smooth landing.",
            <= 350 => "Firm but acceptable.",
            <= 600 => "Hard landing.",
            _ => "Very hard — inspect required in the real world."
        };
    }

    private enum PirepStatusLabel { Pending = 0, Accepted = 1, Rejected = 2, UnderReview = 3 }
}
