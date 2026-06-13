using AirSerbiaVirtua.Acars.Core;
using AirSerbiaVirtua.Contracts;

namespace AirSerbiaVirtua.Acars.Desktop.Services;

/// <summary>
/// Process-wide pilot session. Owns the single <see cref="ApiService"/> instance,
/// the current pilot profile, and notifies subscribers when authentication state
/// changes so the shell and status bar can react.
/// </summary>
public interface ISessionService
{
    IApiService Api { get; }
    PilotProfile? Pilot { get; }
    bool IsAuthenticated { get; }
    bool IsApiReachable { get; }

    event EventHandler? StateChanged;

    Task<LoginResult> LoginAsync(string callsign, string password, CancellationToken ct = default);

    /// <summary>Silent sign-in from the persisted refresh token (Auto Login).</summary>
    Task<bool> TryAutoLoginAsync(CancellationToken ct = default);

    Task LogoutAsync(CancellationToken ct = default);
}

public sealed record LoginResult(bool Success, string? ErrorMessage);
