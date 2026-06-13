using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AirSerbiaVirtua.Contracts;

namespace AirSerbiaVirtua.Acars.Core;

/// <summary>
/// Typed HTTPS client for the Air Serbia Virtua API (v1 — all endpoints live
/// under api/v1/). Holds the bearer + refresh tokens, attaches the access token
/// to every authenticated request, and automatically refreshes (once per call)
/// when the server returns 401.
///
/// PoC note: tokens are held in memory only. For the WPF/hybrid port, persist
/// the refresh token via the Windows Credential Manager / DPAPI rather than
/// plain storage. The access token can stay in memory.
/// </summary>
public sealed class ApiService : IApiService
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private string? _accessToken;
    private string? _refreshToken;
    public DateTimeOffset? AccessTokenExpiresAtUtc { get; private set; }
    public DateTimeOffset? RefreshTokenExpiresAtUtc { get; private set; }

    public PilotProfile? Pilot { get; private set; }
    public bool IsAuthenticated => _accessToken is not null && _refreshToken is not null;

    /// <summary>Host name of the configured API endpoint (for display in the UI).</summary>
    public string BaseHost => _http.BaseAddress?.Host ?? "unknown";

    /// <summary>
    /// Raised whenever a new token pair is stored (login, refresh rotation).
    /// Lets the host persist the rotated refresh token for Auto Login.
    /// </summary>
    public event Action<AuthTokens>? TokensUpdated;

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
        using var resp = await _http.PostAsJsonAsync("api/v1/auth/login", new LoginRequest(callsign, password), Json, ct);
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
            using var resp = await _http.PostAsJsonAsync("api/v1/auth/refresh", new RefreshRequest(_refreshToken), Json, ct);
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

    /// <summary>Fetches the signed-in pilot's profile (used after a token-only session restore).</summary>
    public async Task<PilotProfile> GetMeAsync(CancellationToken ct = default)
    {
        using var resp = await SendWithAuthRetryAsync(() => _http.GetAsync("api/v1/auth/me", ct), ct);
        await EnsureSuccess(resp, "Profile");
        var profile = (await resp.Content.ReadFromJsonAsync<PilotProfile>(Json, ct))!;
        Pilot = profile;
        return profile;
    }

    /// <summary>
    /// Restores a session from a persisted refresh token (Auto Login): rotates the
    /// token for a fresh pair and loads the pilot profile. Returns false when the
    /// token is expired/revoked or the API is unreachable.
    /// </summary>
    public async Task<bool> TryRestoreSessionAsync(string refreshToken, CancellationToken ct = default)
    {
        _refreshToken = refreshToken;
        if (!await RefreshAsync(ct)) return false;

        try
        {
            await GetMeAsync(ct);
            return true;
        }
        catch
        {
            ClearTokens();
            Pilot = null;
            return false;
        }
    }

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_refreshToken)) return;

        try
        {
            using var _ = await SendWithAuthRetryAsync(
                () => _http.PostAsJsonAsync("api/v1/auth/logout", new RefreshRequest(_refreshToken!), Json, ct),
                ct);
        }
        catch { /* best-effort */ }
        finally
        {
            ClearTokens();
            Pilot = null;
        }
    }

    // ---- Health / ping --------------------------------------------------------
    /// <summary>
    /// Round-trip latency to the API's anonymous /health endpoint in ms, or null
    /// when unreachable. Drives the status-bar API LED + PING cell.
    /// </summary>
    public async Task<long?> PingAsync(CancellationToken ct = default)
    {
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(4));
            using var resp = await _http.GetAsync("health", timeout.Token);
            sw.Stop();
            return resp.IsSuccessStatusCode ? sw.ElapsedMilliseconds : null;
        }
        catch
        {
            return null;
        }
    }

    // ---- Airport sync -------------------------------------------------------
    public async Task<AirportSyncResponse> SyncAirportsAsync(int sinceRevision, CancellationToken ct = default)
    {
        using var resp = await SendWithAuthRetryAsync(
            () => _http.GetAsync($"api/v1/sync/airports?since={sinceRevision}", ct), ct);
        await EnsureSuccess(resp, "Airport sync");
        return (await resp.Content.ReadFromJsonAsync<AirportSyncResponse>(Json, ct))!;
    }

    // ---- Flight session -----------------------------------------------------
    public async Task<FlightStartResponse> StartFlightAsync(int routeId, int aircraftId, DateOnly date, CancellationToken ct = default)
    {
        using var resp = await SendWithAuthRetryAsync(
            () => _http.PostAsJsonAsync("api/v1/flights/start", new FlightStartRequest(routeId, aircraftId, date), Json, ct),
            ct);
        await EnsureSuccess(resp, "Flight start");
        return (await resp.Content.ReadFromJsonAsync<FlightStartResponse>(Json, ct))!;
    }

    /// <summary>Pushes a single POSREP sample to the active flight session.</summary>
    public async Task PushPositionAsync(int flightSessionId, PositionReport report, CancellationToken ct = default)
    {
        using var resp = await SendWithAuthRetryAsync(
            () => _http.PostAsJsonAsync($"api/v1/flights/{flightSessionId}/position", report, Json, ct),
            ct);
        await EnsureSuccess(resp, "POSREP");
    }

    /// <summary>Convenience overload that builds the POSREP from live telemetry + state.</summary>
    public Task PushPositionAsync(int flightSessionId, FlightData data, FlightState phase, CancellationToken ct = default) =>
        PushPositionAsync(flightSessionId, PositionReports.FromTelemetry(data, phase), ct);

    /// <summary>Starts an ad-hoc outstation flight (no booking; one-off charter leg).</summary>
    public async Task<FlightStartResponse> StartOutstationFlightAsync(OutstationStartRequest request, CancellationToken ct = default)
    {
        using var resp = await SendWithAuthRetryAsync(
            () => _http.PostAsJsonAsync("api/v1/flights/start-outstation", request, Json, ct),
            ct);
        await EnsureSuccess(resp, "Outstation start");
        return (await resp.Content.ReadFromJsonAsync<FlightStartResponse>(Json, ct))!;
    }

    /// <summary>Abandons the active flight session: PIREP → Aborted, aircraft + booking released.</summary>
    public async Task AbortFlightAsync(int flightSessionId, CancellationToken ct = default)
    {
        using var resp = await SendWithAuthRetryAsync(
            () => _http.PostAsync($"api/v1/flights/{flightSessionId}/abort", content: null, ct),
            ct);
        await EnsureSuccess(resp, "Flight abort");
    }

    // ---- Routes -------------------------------------------------------------
    public async Task<List<RouteInfo>> GetRoutesAsync(string? hubIcao = null, CancellationToken ct = default)
    {
        var url = string.IsNullOrWhiteSpace(hubIcao) ? "api/v1/routes" : $"api/v1/routes?hub={Uri.EscapeDataString(hubIcao)}";
        using var resp = await SendWithAuthRetryAsync(() => _http.GetAsync(url, ct), ct);
        await EnsureSuccess(resp, "Routes list");
        return (await resp.Content.ReadFromJsonAsync<List<RouteInfo>>(Json, ct)) ?? new();
    }

    // ---- Bookings -----------------------------------------------------------
    public async Task<List<BookingInfo>> GetMyBookingsAsync(CancellationToken ct = default)
    {
        using var resp = await SendWithAuthRetryAsync(() => _http.GetAsync("api/v1/bookings/mine", ct), ct);
        await EnsureSuccess(resp, "Bookings (mine)");
        return (await resp.Content.ReadFromJsonAsync<List<BookingInfo>>(Json, ct)) ?? new();
    }

    /// <summary>Open/Confirmed bookings, dispatch-ready first (W4 dispatch pull).</summary>
    public async Task<List<BookingInfo>> GetActiveBookingsAsync(CancellationToken ct = default)
    {
        using var resp = await SendWithAuthRetryAsync(() => _http.GetAsync("api/v1/bookings/active", ct), ct);
        await EnsureSuccess(resp, "Bookings (active)");
        return (await resp.Content.ReadFromJsonAsync<List<BookingInfo>>(Json, ct)) ?? new();
    }

    public async Task<BookingInfo> CreateBookingAsync(int routeId, DateOnly date, CancellationToken ct = default)
    {
        using var resp = await SendWithAuthRetryAsync(
            () => _http.PostAsJsonAsync("api/v1/bookings", new CreateBookingRequest(routeId, date), Json, ct),
            ct);
        await EnsureSuccess(resp, "Create booking");
        return (await resp.Content.ReadFromJsonAsync<BookingInfo>(Json, ct))!;
    }

    public async Task CancelBookingAsync(int bookingId, CancellationToken ct = default)
    {
        using var resp = await SendWithAuthRetryAsync(
            () => _http.DeleteAsync($"api/v1/bookings/{bookingId}", ct),
            ct);
        await EnsureSuccess(resp, "Cancel booking");
    }

    // ---- Aircraft -----------------------------------------------------------
    public async Task<List<AircraftInfo>> GetAircraftAsync(string? type = null, string? status = null, CancellationToken ct = default)
    {
        var qs = new List<string>();
        if (!string.IsNullOrWhiteSpace(type))   qs.Add($"type={Uri.EscapeDataString(type)}");
        if (!string.IsNullOrWhiteSpace(status)) qs.Add($"status={Uri.EscapeDataString(status)}");
        var url = qs.Count == 0 ? "api/v1/aircraft" : $"api/v1/aircraft?{string.Join("&", qs)}";

        using var resp = await SendWithAuthRetryAsync(() => _http.GetAsync(url, ct), ct);
        await EnsureSuccess(resp, "Aircraft list");
        return (await resp.Content.ReadFromJsonAsync<List<AircraftInfo>>(Json, ct)) ?? new();
    }

    // ---- PIREP --------------------------------------------------------------
    public async Task<PirepResult> SubmitPirepAsync(PirepSubmitRequest request, CancellationToken ct = default)
    {
        using var resp = await SendWithAuthRetryAsync(
            () => _http.PostAsJsonAsync("api/v1/pireps", request, Json, ct),
            ct);
        await EnsureSuccess(resp, "PIREP submit");
        return (await resp.Content.ReadFromJsonAsync<PirepResult>(Json, ct))!;
    }

    /// <summary>Returns the signed-in pilot's PIREPs (newest first), optionally filtered by status name.</summary>
    public async Task<List<PirepListItem>> GetMyPirepsAsync(string? status = null, CancellationToken ct = default)
    {
        var url = string.IsNullOrWhiteSpace(status) ? "api/v1/pireps/mine" : $"api/v1/pireps/mine?status={Uri.EscapeDataString(status)}";
        using var resp = await SendWithAuthRetryAsync(() => _http.GetAsync(url, ct), ct);
        await EnsureSuccess(resp, "Logbook");
        return (await resp.Content.ReadFromJsonAsync<List<PirepListItem>>(Json, ct)) ?? new();
    }

    // ---- Route detail -------------------------------------------------------
    public async Task<RouteInfo> GetRouteAsync(int id, CancellationToken ct = default)
    {
        using var resp = await SendWithAuthRetryAsync(() => _http.GetAsync($"api/v1/routes/{id}", ct), ct);
        await EnsureSuccess(resp, "Route");
        return (await resp.Content.ReadFromJsonAsync<RouteInfo>(Json, ct))!;
    }

    // ---- Weather (METAR) ----------------------------------------------------
    public async Task<List<MetarInfo>> GetMetarAsync(IEnumerable<string> icaos, CancellationToken ct = default)
    {
        var ids = string.Join(",", icaos);
        using var resp = await SendWithAuthRetryAsync(
            () => _http.GetAsync($"api/v1/metar?icaos={Uri.EscapeDataString(ids)}", ct), ct);
        await EnsureSuccess(resp, "METAR");
        return (await resp.Content.ReadFromJsonAsync<List<MetarInfo>>(Json, ct)) ?? new();
    }

    // ---- Admin (roster management) -------------------------------------------
    /// <summary>Full roster for the admin page (requires the Admin role).</summary>
    public async Task<List<AdminPilot>> GetAdminPilotsAsync(CancellationToken ct = default)
    {
        using var resp = await SendWithAuthRetryAsync(() => _http.GetAsync("api/v1/admin/pilots", ct), ct);
        await EnsureSuccess(resp, "Roster");
        return (await resp.Content.ReadFromJsonAsync<List<AdminPilot>>(Json, ct)) ?? new();
    }

    /// <summary>Promotes a Pending/Inactive pilot to Active.</summary>
    public async Task ActivatePilotAsync(int pilotId, CancellationToken ct = default)
    {
        using var resp = await SendWithAuthRetryAsync(
            () => _http.PostAsync($"api/v1/admin/pilots/{pilotId}/activate", content: null, ct), ct);
        await EnsureSuccess(resp, "Activate pilot");
    }

    /// <summary>Deactivates an Active pilot.</summary>
    public async Task DeactivatePilotAsync(int pilotId, CancellationToken ct = default)
    {
        using var resp = await SendWithAuthRetryAsync(
            () => _http.PostAsync($"api/v1/admin/pilots/{pilotId}/deactivate", content: null, ct), ct);
        await EnsureSuccess(resp, "Deactivate pilot");
    }

    // ---- Change password ----------------------------------------------------
    public async Task ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken ct = default)
    {
        using var resp = await SendWithAuthRetryAsync(
            () => _http.PostAsJsonAsync("api/v1/auth/change-password",
                new ChangePasswordRequest(currentPassword, newPassword), Json, ct),
            ct);
        await EnsureSuccess(resp, "Change password");
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
        TokensUpdated?.Invoke(tokens);
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
