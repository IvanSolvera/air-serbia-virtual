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
        RaiseStateChanged();
    }

    private void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

    public void Dispose() => Api.Dispose();
}
