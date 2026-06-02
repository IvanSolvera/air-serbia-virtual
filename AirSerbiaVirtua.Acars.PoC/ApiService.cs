using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace AirSerbiaVirtua.Acars.PoC;

/// <summary>
/// Typed HTTPS client for the Air Serbia Virtua API. Holds the bearer token,
/// attaches it to every authenticated request, and pushes POSREP payloads in the
/// exact JSON shape the server expects.
///
/// PoC note: the token is held in memory only. For the WPF/hybrid port, persist
/// it via the Windows Credential Manager / DPAPI rather than plain storage.
/// </summary>
public sealed class ApiService : IDisposable
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public PilotProfile? Pilot { get; private set; }
    public bool IsAuthenticated => _http.DefaultRequestHeaders.Authorization is not null;

    /// <param name="baseUrl">e.g. https://localhost:7001/</param>
    /// <param name="handler">Optional handler (inject for tests or cert pinning).</param>
    public ApiService(string baseUrl, HttpMessageHandler? handler = null)
    {
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    private void SetToken(string token) =>
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    // ---- Auth ---------------------------------------------------------------
    public async Task<LoginResponse> LoginAsync(string callsign, string password, CancellationToken ct = default)
    {
        using var resp = await _http.PostAsJsonAsync("api/auth/login", new LoginRequest(callsign, password), Json, ct);
        await EnsureSuccess(resp, "Login");
        var result = (await resp.Content.ReadFromJsonAsync<LoginResponse>(Json, ct))!;
        SetToken(result.Token);
        Pilot = result.Pilot;
        return result;
    }

    // ---- Airport sync -------------------------------------------------------
    public async Task<AirportSyncResponse> SyncAirportsAsync(int sinceRevision, CancellationToken ct = default)
    {
        using var resp = await _http.GetAsync($"api/sync/airports?since={sinceRevision}", ct);
        await EnsureSuccess(resp, "Airport sync");
        return (await resp.Content.ReadFromJsonAsync<AirportSyncResponse>(Json, ct))!;
    }

    // ---- Flight session -----------------------------------------------------
    public async Task<FlightStartResponse> StartFlightAsync(int routeId, int aircraftId, DateOnly date, CancellationToken ct = default)
    {
        using var resp = await _http.PostAsJsonAsync("api/flights/start", new FlightStartRequest(routeId, aircraftId, date), Json, ct);
        await EnsureSuccess(resp, "Flight start");
        return (await resp.Content.ReadFromJsonAsync<FlightStartResponse>(Json, ct))!;
    }

    /// <summary>Pushes a single POSREP sample to the active flight session.</summary>
    public async Task PushPositionAsync(int flightSessionId, PositionReport report, CancellationToken ct = default)
    {
        using var resp = await _http.PostAsJsonAsync($"api/flights/{flightSessionId}/position", report, Json, ct);
        await EnsureSuccess(resp, "POSREP");
    }

    /// <summary>Convenience overload that builds the POSREP from live telemetry + state.</summary>
    public Task PushPositionAsync(int flightSessionId, FlightData data, FlightState phase, CancellationToken ct = default) =>
        PushPositionAsync(flightSessionId, new PositionReport(
            Timestamp: data.SampleTimeUtc,
            Lat: data.LatitudeDeg,
            Lon: data.LongitudeDeg,
            AltFt: (int)Math.Round(data.AltitudeFt),
            GsKts: (int)Math.Round(data.GroundSpeedKts),
            Phase: (int)phase.ToApiPhase()), ct);

    // ---- PIREP --------------------------------------------------------------
    public async Task<PirepResult> SubmitPirepAsync(PirepSubmitRequest request, CancellationToken ct = default)
    {
        using var resp = await _http.PostAsJsonAsync("api/pireps", request, Json, ct);
        await EnsureSuccess(resp, "PIREP submit");
        return (await resp.Content.ReadFromJsonAsync<PirepResult>(Json, ct))!;
    }

    private static async Task EnsureSuccess(HttpResponseMessage resp, string op)
    {
        if (resp.IsSuccessStatusCode) return;
        var body = await resp.Content.ReadAsStringAsync();
        throw new ApiException($"{op} failed: {(int)resp.StatusCode} {resp.ReasonPhrase}. {body}", resp.StatusCode);
    }

    public void Dispose() => _http.Dispose();
}

public sealed class ApiException : Exception
{
    public System.Net.HttpStatusCode StatusCode { get; }
    public ApiException(string message, System.Net.HttpStatusCode code) : base(message) => StatusCode = code;
}
