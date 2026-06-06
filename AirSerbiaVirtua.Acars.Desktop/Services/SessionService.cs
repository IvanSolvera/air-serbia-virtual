using System.Net.Http;
using AirSerbiaVirtua.Acars.Core;
using AirSerbiaVirtua.Acars.Desktop.Settings;
using Microsoft.Extensions.Options;

namespace AirSerbiaVirtua.Acars.Desktop.Services;

public sealed class SessionService : ISessionService, IDisposable
{
    public ApiService Api { get; }
    public PilotProfile? Pilot { get; private set; }
    public bool IsAuthenticated => Api.IsAuthenticated;
    public bool IsApiReachable { get; private set; }

    public event EventHandler? StateChanged;

    public SessionService(IOptions<ApiOptions> options)
    {
        Api = new ApiService(options.Value.BaseUrl);

        // Auto Login: every time the token pair rotates (login or refresh) persist
        // the new refresh token via DPAPI — but only while the user has the
        // Auto Login toggle on. The old token is revoked server-side on rotation,
        // so failing to persist would silently break the next auto login.
        Api.TokensUpdated += tokens =>
        {
            if (UiSettings.Load().AutoLogin)
                SessionTokenStore.Save(tokens.RefreshToken);
            else
                SessionTokenStore.Delete();   // toggle off → stop keeping a session around
        };
    }

    /// <summary>
    /// Attempts a silent sign-in from the DPAPI-persisted refresh token. Called
    /// once at startup when the Auto Login preference is enabled.
    /// </summary>
    public async Task<bool> TryAutoLoginAsync(CancellationToken ct = default)
    {
        var token = SessionTokenStore.Load();
        if (string.IsNullOrEmpty(token)) return false;

        try
        {
            if (!await Api.TryRestoreSessionAsync(token, ct))
            {
                SessionTokenStore.Delete();   // revoked/expired — don't retry forever
                return false;
            }

            Pilot = Api.Pilot;
            IsApiReachable = true;
            RaiseStateChanged();
            return true;
        }
        catch
        {
            // API unreachable — keep the stored token; a later manual login or
            // restart can still use it.
            IsApiReachable = false;
            RaiseStateChanged();
            return false;
        }
    }

    public async Task<LoginResult> LoginAsync(string callsign, string password, CancellationToken ct = default)
    {
        try
        {
            var response = await Api.LoginAsync(callsign, password, ct);
            Pilot = response.Pilot;
            IsApiReachable = true;
            RaiseStateChanged();
            return new LoginResult(true, null);
        }
        catch (ApiException ex)
        {
            IsApiReachable = true;
            RaiseStateChanged();
            return new LoginResult(false, ex.Message);
        }
        catch (HttpRequestException ex)
        {
            IsApiReachable = false;
            RaiseStateChanged();
            return new LoginResult(false, $"Cannot reach API: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            IsApiReachable = false;
            RaiseStateChanged();
            return new LoginResult(false, "Request timed out.");
        }
    }

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        await Api.LogoutAsync(ct);
        Pilot = null;
        SessionTokenStore.Delete();   // explicit sign-out always ends the persisted session
        RaiseStateChanged();
    }

    private void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

    public void Dispose() => Api.Dispose();
}
