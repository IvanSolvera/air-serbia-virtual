using AirSerbiaVirtua.Contracts;

namespace AirSerbiaVirtua.Web.Client.Services;

/// <summary>Seam over <see cref="PortalApi"/> so bUnit tests can fake the API.</summary>
public interface IPortalApi
{
    Task<(bool Ok, string? Error)> LoginAsync(string callsign, string password);
    Task LogoutAsync();

    Task<List<RouteInfo>> GetRoutesAsync();
    Task<RouteInfo?> GetRouteAsync(int id);
    Task<List<BookingInfo>> GetMyBookingsAsync();
    Task<List<BookingInfo>> GetActiveBookingsAsync();
    Task<(bool Ok, string? Error)> CreateBookingAsync(int routeId, DateOnly date);
    Task<(bool Ok, string? Error)> CancelBookingAsync(int bookingId);
    Task<(bool Ok, string? Error)> MarkDispatchReadyAsync(int bookingId);
    Task<(bool Ok, string? Error)> ClearDispatchReadyAsync(int bookingId);

    Task<List<MetarInfo>> GetMetarAsync(IEnumerable<string> icaos);
    Task<List<PirepListItem>> GetMyPirepsAsync();
    Task<(bool Ok, string? Error)> ChangePasswordAsync(string current, string next);
}
