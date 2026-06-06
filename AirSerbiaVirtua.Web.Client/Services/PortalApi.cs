using System.Net;
using System.Net.Http.Json;
using AirSerbiaVirtua.Contracts;

namespace AirSerbiaVirtua.Web.Client.Services;

/// <summary>
/// Typed API client for the pilot portal (WASM). Attaches the bearer token from
/// <see cref="PortalSession"/> and transparently refreshes the pair once when a
/// call comes back 401 — same pattern as the desktop ACARS client.
/// </summary>
public sealed class PortalApi(HttpClient http, PortalSession session)
{
    // ---- Auth ---------------------------------------------------------------
    public async Task<(bool Ok, string? Error)> LoginAsync(string callsign, string password)
    {
        try
        {
            var resp = await http.PostAsJsonAsync("api/v1/auth/login", new LoginRequest(callsign, password));
            if (!resp.IsSuccessStatusCode)
                return (false, await ReadErrorAsync(resp, "Sign-in failed"));

            var result = (await resp.Content.ReadFromJsonAsync<LoginResponse>())!;
            await session.SetAsync(result.Tokens, result.Pilot);
            return (true, null);
        }
        catch
        {
            return (false, "The crew desk is unreachable — is the API running?");
        }
    }

    public async Task LogoutAsync()
    {
        try
        {
            if (session.Tokens is { } t)
            {
                using var req = NewRequest(HttpMethod.Post, "api/v1/auth/logout");
                req.Content = JsonContent.Create(new RefreshRequest(t.RefreshToken));
                await http.SendAsync(req);
            }
        }
        catch { /* best-effort */ }
        finally
        {
            await session.ClearAsync();
        }
    }

    // ---- Portal data ----------------------------------------------------------
    public Task<List<RouteInfo>> GetRoutesAsync() =>
        GetAsync<List<RouteInfo>>("api/v1/routes");

    public Task<List<BookingInfo>> GetMyBookingsAsync() =>
        GetAsync<List<BookingInfo>>("api/v1/bookings/mine");

    public async Task<(bool Ok, string? Error)> CreateBookingAsync(int routeId, DateOnly date)
    {
        var resp = await SendWithRetryAsync(() =>
        {
            var req = NewRequest(HttpMethod.Post, "api/v1/bookings");
            req.Content = JsonContent.Create(new CreateBookingRequest(routeId, date));
            return req;
        });
        return resp.IsSuccessStatusCode ? (true, null) : (false, await ReadErrorAsync(resp, "Booking failed"));
    }

    public async Task<(bool Ok, string? Error)> CancelBookingAsync(int bookingId)
    {
        var resp = await SendWithRetryAsync(() => NewRequest(HttpMethod.Delete, $"api/v1/bookings/{bookingId}"));
        return resp.IsSuccessStatusCode ? (true, null) : (false, await ReadErrorAsync(resp, "Cancel failed"));
    }

    public Task<List<PirepListItem>> GetMyPirepsAsync() =>
        GetAsync<List<PirepListItem>>("api/v1/pireps/mine");

    public async Task<(bool Ok, string? Error)> ChangePasswordAsync(string current, string next)
    {
        var resp = await SendWithRetryAsync(() =>
        {
            var req = NewRequest(HttpMethod.Post, "api/v1/auth/change-password");
            req.Content = JsonContent.Create(new ChangePasswordRequest(current, next));
            return req;
        });
        return resp.IsSuccessStatusCode ? (true, null) : (false, await ReadErrorAsync(resp, "Password change failed"));
    }

    // ---- Internals ---------------------------------------------------------------
    private async Task<T> GetAsync<T>(string url) where T : new()
    {
        var resp = await SendWithRetryAsync(() => NewRequest(HttpMethod.Get, url));
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<T>() ?? new T();
    }

    private HttpRequestMessage NewRequest(HttpMethod method, string url)
    {
        var req = new HttpRequestMessage(method, url);
        if (session.Tokens is { } t)
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", t.AccessToken);
        return req;
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(Func<HttpRequestMessage> makeRequest)
    {
        var resp = await http.SendAsync(makeRequest());
        if (resp.StatusCode != HttpStatusCode.Unauthorized || session.Tokens is null)
            return resp;

        resp.Dispose();
        if (!await TryRefreshAsync())
        {
            await session.ClearAsync();   // session expired — caller lands on login
            return await http.SendAsync(makeRequest());
        }
        return await http.SendAsync(makeRequest());
    }

    private async Task<bool> TryRefreshAsync()
    {
        try
        {
            var resp = await http.PostAsJsonAsync("api/v1/auth/refresh",
                new RefreshRequest(session.Tokens!.RefreshToken));
            if (!resp.IsSuccessStatusCode) return false;

            var result = (await resp.Content.ReadFromJsonAsync<RefreshResponse>())!;
            await session.UpdateTokensAsync(result.Tokens);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage resp, string fallback)
    {
        try
        {
            var body = await resp.Content.ReadFromJsonAsync<ErrorBody>();
            return string.IsNullOrWhiteSpace(body?.Message) ? $"{fallback} ({(int)resp.StatusCode})." : body!.Message!;
        }
        catch
        {
            return $"{fallback} ({(int)resp.StatusCode}).";
        }
    }

    private sealed record ErrorBody(string? Message);
}
