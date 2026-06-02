using System.ComponentModel.DataAnnotations;

namespace AirSerbiaVirtua.Api.Models;

/// <summary>
/// A virtual airline pilot. <see cref="Callsign"/> is the unique roster ID
/// (e.g. "ASL001"). <see cref="HubId"/> references the pilot's home base
/// <see cref="Airport"/> by ICAO.
/// </summary>
public class Pilot
{
    public int Id { get; set; }

    /// <summary>Unique roster callsign, e.g. "ASL001".</summary>
    [MaxLength(10)]
    public string Callsign { get; set; } = null!;

    [MaxLength(120)]
    public string Name { get; set; } = null!;

    [MaxLength(256)]
    public string Email { get; set; } = null!;

    public int RankId { get; set; }
    public Rank? Rank { get; set; }

    /// <summary>Total logged block hours across all accepted PIREPs.</summary>
    public decimal TotalHours { get; set; }

    public PilotStatus Status { get; set; } = PilotStatus.Pending;

    /// <summary>Home hub ICAO, references <see cref="Airport.Icao"/>.</summary>
    [MaxLength(4)]
    public string HubId { get; set; } = null!;
    public Airport? Hub { get; set; }

    public DateTimeOffset DateJoined { get; set; }

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<Pirep> Pireps { get; set; } = new List<Pirep>();
}
