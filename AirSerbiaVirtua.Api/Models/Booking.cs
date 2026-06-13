using AirSerbiaVirtua.Contracts;

namespace AirSerbiaVirtua.Api.Models;

/// <summary>
/// A pilot's reservation of a <see cref="Route"/> for a given date. A booking
/// is the precursor to a flown <see cref="Pirep"/>.
/// </summary>
public class Booking
{
    public int Id { get; set; }

    public int PilotId { get; set; }
    public Pilot? Pilot { get; set; }

    public int RouteId { get; set; }
    public Route? Route { get; set; }

    /// <summary>Scheduled service date.</summary>
    public DateOnly Date { get; set; }

    public BookingStatus Status { get; set; } = BookingStatus.Open;

    /// <summary>
    /// Set when the pilot marks this booking "dispatch ready" on the web
    /// Briefing page; null = not prepared. The desktop client offers
    /// dispatch-ready bookings for today as one-click "Resume dispatch".
    /// </summary>
    public DateTimeOffset? DispatchReadyAtUtc { get; set; }
}
