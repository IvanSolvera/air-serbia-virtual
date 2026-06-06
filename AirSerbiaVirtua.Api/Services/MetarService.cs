using System.Text.Json;
using AirSerbiaVirtua.Contracts;

namespace AirSerbiaVirtua.Api.Services;

/// <summary>
/// Fetches raw METARs from the free aviationweather.gov data API and maps them to
/// <see cref="MetarInfo"/>. Network/parse failures degrade gracefully to a DTO with
/// a null <c>Raw</c> rather than throwing, so the Briefing always renders.
/// </summary>
public sealed class MetarService
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public MetarService(HttpClient http) => _http = http;

    /// <summary>Fetches the latest METAR for each ICAO. Order/length matches the input.</summary>
    public async Task<List<MetarInfo>> GetAsync(IEnumerable<string> icaos, CancellationToken ct = default)
    {
        var ids = icaos
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();

        if (ids.Count == 0) return new List<MetarInfo>();

        var byIcao = new Dictionary<string, MetarInfo>();
        try
        {
            // e.g. https://aviationweather.gov/api/data/metar?ids=LYBE,LOWW&format=json
            var url = $"api/data/metar?ids={string.Join(",", ids)}&format=json";
            using var resp = await _http.GetAsync(url, ct);
            if (resp.IsSuccessStatusCode)
            {
                var raw = await resp.Content.ReadAsStringAsync(ct);
                var entries = JsonSerializer.Deserialize<List<AwcMetar>>(raw, Json) ?? new();
                foreach (var e in entries)
                {
                    if (string.IsNullOrWhiteSpace(e.IcaoId)) continue;
                    DateTimeOffset? observed = e.ObsTime is { } t and > 0
                        ? DateTimeOffset.FromUnixTimeSeconds(t)
                        : null;
                    byIcao[e.IcaoId.ToUpperInvariant()] = new MetarInfo(e.IcaoId.ToUpperInvariant(), e.RawOb, observed);
                }
            }
        }
        catch
        {
            // Swallow — fall through and return null-raw entries for everything.
        }

        return ids.Select(id => byIcao.TryGetValue(id, out var m) ? m : new MetarInfo(id, null, null)).ToList();
    }

    // Shape of the relevant fields from aviationweather.gov's JSON METAR.
    private sealed record AwcMetar(string? IcaoId, string? RawOb, long? ObsTime);
}
