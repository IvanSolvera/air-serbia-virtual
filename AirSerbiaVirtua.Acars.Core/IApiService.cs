using AirSerbiaVirtua.Contracts;

namespace AirSerbiaVirtua.Acars.Core;

/// <summary>
/// Seam over <see cref="ApiService"/> so ViewModels and services are unit-testable
/// with a mock. One member per API endpoint the desktop client uses.
/// </summary>
public interface IApiService : IDisposable
{
    PilotProfile? Pilot { get; }
    bool IsAuthenticated { get; }
    string BaseHost { get; }
    DateTimeOffset? AccessTokenExpiresAtUtc { get; }
    DateTimeOffset? RefreshTokenExpiresAtUtc { get; }
    event Action<AuthTokens>? TokensUpdated;

    Task<LoginResponse> LoginAsync(string callsign, string password, CancellationToken ct = default);
    Task<bool> RefreshAsync(CancellationToken ct = default);
    Task<PilotProfile> GetMeAsync(CancellationToken ct = default);
    Task<bool> TryRestoreSessionAsync(string refreshToken, CancellationToken ct = default);
    Task LogoutAsync(CancellationToken ct = default);
    Task<long?> PingAsync(CancellationToken ct = default);
    Task ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken ct = default);

    Task<AirportSyncResponse> SyncAirportsAsync(int sinceRevision, CancellationToken ct = default);

    Task<FlightStartResponse> StartFlightAsync(int routeId, int aircraftId, DateOnly date, CancellationToken ct = default);
    Task<FlightStartResponse> StartOutstationFlightAsync(OutstationStartRequest request, CancellationToken ct = default);
    Task PushPositionAsync(int flightSessionId, PositionReport report, CancellationToken ct = default);
    Task PushPositionAsync(int flightSessionId, FlightData data, FlightState phase, CancellationToken ct = default);
    Task AbortFlightAsync(int flightSessionId, CancellationToken ct = default);
    Task<PirepResult> SubmitPirepAsync(PirepSubmitRequest request, CancellationToken ct = default);

    Task<List<RouteInfo>> GetRoutesAsync(string? hubIcao = null, CancellationToken ct = default);
    Task<RouteInfo> GetRouteAsync(int id, CancellationToken ct = default);

    Task<List<BookingInfo>> GetMyBookingsAsync(CancellationToken ct = default);
    Task<List<BookingInfo>> GetActiveBookingsAsync(CancellationToken ct = default);
    Task<BookingInfo> CreateBookingAsync(int routeId, DateOnly date, CancellationToken ct = default);
    Task CancelBookingAsync(int bookingId, CancellationToken ct = default);

    Task<List<AircraftInfo>> GetAircraftAsync(string? type = null, string? status = null, CancellationToken ct = default);
    Task<List<PirepListItem>> GetMyPirepsAsync(string? status = null, CancellationToken ct = default);
    Task<List<MetarInfo>> GetMetarAsync(IEnumerable<string> icaos, CancellationToken ct = default);

    Task<List<AdminPilot>> GetAdminPilotsAsync(CancellationToken ct = default);
    Task ActivatePilotAsync(int pilotId, CancellationToken ct = default);
    Task DeactivatePilotAsync(int pilotId, CancellationToken ct = default);
}
