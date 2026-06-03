using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace AirSerbiaVirtua.Acars.Core;

/// <summary>
/// Typed HTTPS client for the Air Serbia Virtua API. Holds the bearer + refresh
/// tokens, attaches the access token to every authenticated request, and
/// automatically refreshes (once per call) when the server returns 401.
///
/// PoC note: tokens are held in memory only. For the WPF/hybrid port, persist
/// the refresh token via the Windows Credential Manager / DPAPI rather than
/// plain storage. The access token can stay in memory.
/// </summary>
public sealed class ApiService : IDisposable
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private string? _accessToken;
    private string? _refreshToken;
    public DateTimeOffset? AccessTokenExpiresAtUtc { get; private set; }
    public DateTimeOffset? RefreshTokenExpiresAtUtc { get; private set; }

    public PilotProfile? Pilot { get; private set; }
    public bool IsAuthenticated => _accessToken is not null && _refreshToken is not null;

    /// <param name="baseUrl">e.g. https://localhost:7001/</param>
    /// <param name="handler">Optional handler (inject for tests or cert pinning).</param>
    public ApiService(string baseUrl, HttpMessageHandler? handler = null)
    {
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    // ---- Auth ---------------------------------------------------------------
    public async Task<LoginResponse> LoginAsync(string callsign, string password, CancellationToken ct = default)
    {
        using var resp = await _http.PostAsJsonAsync("api/auth/login", new LoginRequest(callsign, password), Json, ct);
        await EnsureSuccess(resp, "Login");
        var result = (await resp.Content.ReadFromJsonAsync<LoginResponse>(Json, ct))!;
        StoreTokens(result.Tokens);
        Pilot = result.Pilot;
        return result;
    }

    /// <summary>
    /// Exchanges the stored refresh token for a fresh pair. Returns false on
    /// any failure (the caller should treat the session as ended).
    /// </summary>
    public async Task<bool> RefreshAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_refreshToken)) return false;

        try
        {
            using var resp = await _http.PostAsJsonAsync("api/auth/refresh", new RefreshRequest(_refreshToken), Json, ct);
            if (!resp.IsSuccessStatusCode)
            {
                ClearTokens();
                return false;
            }
            var result = (await resp.Content.ReadFromJsonAsync<RefreshResponse>(Json, ct))!;
            StoreTokens(result.Tokens);
            return true;
        }
        catch
        {
            ClearTokens();
            return false;
        }
    }

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_refreshToken)) return;

        try
        {
            using var _ = await SendWithAuthRetryAsync(
                () => _http.PostAsJsonAsync("api/auth/logout", new RefreshRequest(_refreshToken!), Json, ct),
                ct);
        }
        catch { /* best-effort */ }
        finally
        {
            ClearTokens();
            Pilot = null;
        }
    }

    // ---- Airport sync -------------------------------------------------------
    public async Task<AirportSyncResponse> SyncAirportsAsync(int sinceRevision, CancellationToken ct = default)
    {
        using var resp = await SendWithAuthRetryAsync(
            () => _http.GetAsync($"api/sync/airports?since={sinceRevision}", ct), ct);
        await EnsureSuccess(resp, "Airport sync");
        return (await resp.Content.ReadFromJsonAsync<AirportSyncResponse>(Json, ct))!;
    }

    // ---- Flight session -----------------------------------------------------
    public async Task<FlightStartResponse> StartFlightAsync(int routeId, int aircraftId, DateOnly date, CancellationToken ct = default)
    {
        using var resp = await SendWithAuthRetryAsync(
            () => _http.PostAsJsonAsync("api/flights/start", new FlightStartRequest(routeId, aircraftId, date), Json, ct),
            ct);
        await EnsureSuccess(resp, "Flight start");
        return (await resp.Content.ReadFromJsonAsync<FlightStartResponse>(Json, ct))!;
    }

    /// <summary>Pushes a single POSREP sample to the active flight session.</summary>
    public async Task PushPositionAsync(int flightSessionId, PositionReport report, CancellationToken ct = default)
    {
        using var resp = await SendWithAuthRetryAsync(
            () => _http.PostAsJsonAsync($"api/flights/{flightSessionId}/position", report, Json, ct),
            ct);
        await EnsureSuccess(resp, "POSREP");
    }

    /// <summary>Convenience overload that builds the POSREP from live telemetry + state.</summary>
    public Task PushPositionAsync(int flightSessionId, FlightData data, FlightState phase, CancellationToken ct = default) =>
        PushPositionAsync(flightSessionId, PositionReport.FromTelemetry(data, phase), ct);

    // ---- Routes -------------------------------------------------------------
    public async Task<List<ApiRoute>> GetRoutesAsync(string? hubIcao = null, CancellationToken ct = default)
    {
        var url = string.IsNullOrWhiteSpace(hubIcao) ? "api/routes" : $"api/routes?hub={Uri.EscapeDataString(hubIcao)}";
        using var resp = await SendWithAuthRetryAsync(() => _http.GetAsync(url, ct), ct);
        await EnsureSuccess(resp, "Routes list");
        return (await resp.Content.ReadFromJsonAsync<List<ApiRoute>>(Json, ct)) ?? new();
    }

    // ---- Bookings -----------------------------------------------------------
    public async Task<List<ApiBooking>> GetMyBookingsAsync(CancellationToken ct = default)
    {
        using var resp = await SendWithAuthRetryAsync(() => _http.GetAsync("api/bookings/mine", ct), ct);
        await EnsureSuccess(resp, "Bookings (mine)");
        return (await resp.Content.ReadFromJsonAsync<List<ApiBooking>>(Json, ct)) ?? new();
    }

    public async Task<ApiBooking> CreateBookingAsync(int routeId, DateOnly date, CancellationToken ct = default)
    {
        using var resp = await SendWithAuthRetryAsync(
            () => _http.PostAsJsonAsync("api/bookings", new CreateBookingRequest(routeId, date), Json, ct),
            ct);
        await EnsureSuccess(resp, "Create booking");
        return (await resp.Content.ReadFromJsonAsync<ApiBooking>(Json, ct))!;
    }

    public async Task CancelBookingAsync(int bookingId, CancellationToken ct = default)
    {
        using var resp = await SendWithAuthRetryAsync(
            () => _http.DeleteAsync($"api/bookings/{bookingId}", ct),
            ct);
        await EnsureSuccess(resp, "Cancel booking");
    }

    // ---- Aircraft -----------------------------------------------------------
    public async Task<List<ApiAircraft>> GetAircraftAsync(string? type = null, string? status = null, CancellationToken ct = default)
    {
        var qs = new List<string>();
        if (!string.IsNullOrWhiteSpace(type))   qs.Add($"type={Uri.EscapeDataString(type)}");
        if (!string.IsNullOrWhiteSpace(status)) qs.Add($"status={Uri.EscapeDataString(status)}");
        var url = qs.Count == 0 ? "api/aircraft" : $"api/aircraft?{string.Join("&", qs)}";

        using var resp = await SendWithAuthRetryAsync(() => _http.GetAsync(url, ct), ct);
        await EnsureSuccess(resp, "Aircraft list");
        return (await resp.Content.ReadFromJsonAsync<List<ApiAircraft>>(Json, ct)) ?? new();
    }

    // ---- PIREP --------------------------------------------------------------
    public async Task<PirepResult> SubmitPirepAsync(PirepSubmitRequest request, CancellationToken ct = default)
    {
        using var resp = await SendWithAuthRetryAsync(
            () => _http.PostAsJsonAsync("api/pireps", request, Json, ct),
            ct);
        await EnsureSuccess(resp, "PIREP submit");
        return (await resp.Content.ReadFromJsonAsync<PirepResult>(Json, ct))!;
    }

    // ---- Internals ----------------------------------------------------------
    private async Task<HttpResponseMessage> SendWithAuthRetryAsync(
        Func<Task<HttpResponseMessage>> send,
        CancellationToken ct)
    {
        // Each call rebuilds the request, so on a 401 we can transparently
        // refresh and replay. Refreshes happen at most once per logical call.
        var resp = await send();
        if (resp.StatusCode != HttpStatusCode.Unauthorized || string.IsNullOrEmpty(_refreshToken))
            return resp;

        resp.Dispose();
        if (!await RefreshAsync(ct))
            return await send();   // Refresh failed â€” let caller see the next 401.

        return await send();
    }

    private void StoreTokens(AuthTokens tokens)
    {
        _accessToken = tokens.AccessToken;
        _refreshToken = tokens.RefreshToken;
        AccessTokenExpiresAtUtc = tokens.AccessExpiresAtUtc;
        RefreshTokenExpiresAtUtc = tokens.RefreshExpiresAtUtc;
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
    }

    private void ClearTokens()
    {
        _accessToken = null;
        _refreshToken = null;
        AccessTokenExpiresAtUtc = null;
        RefreshTokenExpiresAtUtc = null;
        _http.DefaultRequestHeaders.Authorization = null;
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
    public HttpStatusCode StatusCode { get; }
    public ApiException(string message, HttpStatusCode code) : base(message) => StatusCode = code;
}
