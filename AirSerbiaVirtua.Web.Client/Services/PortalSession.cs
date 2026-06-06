using System.Text.Json;
using AirSerbiaVirtua.Contracts;
using Microsoft.JSInterop;

namespace AirSerbiaVirtua.Web.Client.Services;

/// <summary>
/// Browser-side pilot session for the portal (WASM): holds the JWT pair and the
/// pilot profile, persisted in localStorage so a refresh/revisit keeps you
/// signed in. The access token never outlives its server TTL — PortalApi
/// rotates the pair on 401 via the refresh endpoint.
/// </summary>
public sealed class PortalSession(IJSRuntime js)
{
    private const string TokensKey = "asv.tokens";
    private const string PilotKey = "asv.pilot";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private bool _restored;

    public AuthTokens? Tokens { get; private set; }
    public PilotProfile? Pilot { get; private set; }
    public bool IsAuthenticated => Tokens is not null && Pilot is not null;

    public event Action? Changed;

    /// <summary>Loads a persisted session from localStorage (first call only).</summary>
    public async Task EnsureRestoredAsync()
    {
        if (_restored) return;
        _restored = true;

        try
        {
            var tokensJson = await js.InvokeAsync<string?>("localStorage.getItem", TokensKey);
            var pilotJson = await js.InvokeAsync<string?>("localStorage.getItem", PilotKey);
            if (tokensJson is null || pilotJson is null) return;

            Tokens = JsonSerializer.Deserialize<AuthTokens>(tokensJson, Json);
            Pilot = JsonSerializer.Deserialize<PilotProfile>(pilotJson, Json);
            Changed?.Invoke();
        }
        catch
        {
            // Corrupt storage — treat as signed out.
            Tokens = null;
            Pilot = null;
        }
    }

    public async Task SetAsync(AuthTokens tokens, PilotProfile pilot)
    {
        Tokens = tokens;
        Pilot = pilot;
        await js.InvokeVoidAsync("localStorage.setItem", TokensKey, JsonSerializer.Serialize(tokens, Json));
        await js.InvokeVoidAsync("localStorage.setItem", PilotKey, JsonSerializer.Serialize(pilot, Json));
        Changed?.Invoke();
    }

    public async Task UpdateTokensAsync(AuthTokens tokens)
    {
        Tokens = tokens;
        await js.InvokeVoidAsync("localStorage.setItem", TokensKey, JsonSerializer.Serialize(tokens, Json));
    }

    public async Task ClearAsync()
    {
        Tokens = null;
        Pilot = null;
        await js.InvokeVoidAsync("localStorage.removeItem", TokensKey);
        await js.InvokeVoidAsync("localStorage.removeItem", PilotKey);
        Changed?.Invoke();
    }
}
