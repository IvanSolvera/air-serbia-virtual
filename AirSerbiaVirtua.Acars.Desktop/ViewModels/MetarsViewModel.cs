using System.Collections.ObjectModel;
using AirSerbiaVirtua.Acars.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AirSerbiaVirtua.Acars.Desktop.ViewModels;

/// <summary>
/// METARs (Phase 5): station weather lookup. Accepts one or more ICAO codes
/// and shows the latest raw METAR per station via the API's aviationweather.gov
/// proxy. Network airports (from the route schedule) are offered as one-click
/// shortcuts.
/// </summary>
public sealed partial class MetarsViewModel : ObservableObject
{
    private readonly ISessionService _session;

    [ObservableProperty] private string _query = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _hasResults;

    public ObservableCollection<MetarRow> Results { get; } = [];
    public ObservableCollection<string> NetworkAirports { get; } = [];

    public MetarsViewModel(ISessionService session)
    {
        _session = session;
        _ = LoadNetworkAirportsAsync();
    }

    /// <summary>Distinct dep/arr ICAOs from the schedule become quick-lookup chips.</summary>
    private async Task LoadNetworkAirportsAsync()
    {
        try
        {
            var routes = await _session.Api.GetRoutesAsync();
            var icaos = routes
                .SelectMany(r => new[] { r.DepIcao, r.ArrIcao })
                .Where(i => !string.IsNullOrWhiteSpace(i))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(i => i)
                .ToList();

            OnUi(() =>
            {
                NetworkAirports.Clear();
                foreach (var icao in icaos) NetworkAirports.Add(icao.ToUpperInvariant());
            });
        }
        catch
        {
            // Chips are a convenience — lookups still work by typing ICAOs.
        }
    }

    [RelayCommand(CanExecute = nameof(CanLookup))]
    private Task LookupAsync() => LookupCoreAsync(ParseIcaos(Query));

    [RelayCommand]
    private Task LookupAirport(string icao) => LookupCoreAsync([icao]);

    private async Task LookupCoreAsync(IReadOnlyList<string> icaos)
    {
        if (icaos.Count == 0)
        {
            StatusMessage = "Enter one or more 4-letter ICAO codes, e.g. LYBE LOWW.";
            return;
        }

        IsBusy = true;
        StatusMessage = null;
        try
        {
            Query = string.Join(" ", icaos);
            var metars = await _session.Api.GetMetarAsync(icaos);

            Results.Clear();
            foreach (var icao in icaos)
            {
                var hit = metars.FirstOrDefault(m => string.Equals(m.Icao, icao, StringComparison.OrdinalIgnoreCase));
                Results.Add(new MetarRow(
                    icao,
                    string.IsNullOrWhiteSpace(hit?.Raw) ? "No METAR available for this station." : hit!.Raw!,
                    AgeLabel(hit?.ObservedAtUtc),
                    HasMetar: !string.IsNullOrWhiteSpace(hit?.Raw)));
            }
            HasResults = Results.Count > 0;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Lookup failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanLookup() => !IsBusy && ParseIcaos(Query).Count > 0;

    partial void OnQueryChanged(string value) => LookupCommand.NotifyCanExecuteChanged();
    partial void OnIsBusyChanged(bool value) => LookupCommand.NotifyCanExecuteChanged();

    private static IReadOnlyList<string> ParseIcaos(string raw) =>
        raw.Split([' ', ',', ';', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
           .Where(t => t.Length == 4 && t.All(char.IsLetter))
           .Select(t => t.ToUpperInvariant())
           .Distinct()
           .Take(8)
           .ToList();

    private static string AgeLabel(DateTimeOffset? observed)
    {
        if (observed is not { } t) return "—";
        var age = DateTimeOffset.UtcNow - t;
        var minutes = Math.Max(0, (int)age.TotalMinutes);
        return $"{t.UtcDateTime:HH:mm}Z · {minutes} min ago";
    }

    private static void OnUi(Action a) =>
        System.Windows.Application.Current?.Dispatcher.Invoke(a);
}

public sealed record MetarRow(string Icao, string Raw, string AgeText, bool HasMetar);
