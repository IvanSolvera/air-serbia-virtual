using System.ComponentModel.DataAnnotations;

namespace AirSerbiaVirtua.Api.Models;

/// <summary>
/// A schedulable route. <see cref="DepIcao"/> and <see cref="ArrIcao"/>
/// reference <see cref="Airport"/> records. <see cref="Days"/> is the set of
/// operating weekdays (ISO 8601: 1 = Monday … 7 = Sunday).
/// </summary>
public class Route
{
    public int Id { get; set; }

    /// <summary>Marketing flight number, e.g. "JU360".</summary>
    [MaxLength(10)]
    public string FlightNumber { get; set; } = null!;

    /// <summary>Departure ICAO, references <see cref="Airport.Icao"/>.</summary>
    [MaxLength(4)]
    public string DepIcao { get; set; } = null!;
    public Airport? Departure { get; set; }

    /// <summary>Arrival ICAO, references <see cref="Airport.Icao"/>.</summary>
    [MaxLength(4)]
    public string ArrIcao { get; set; } = null!;
    public Airport? Arrival { get; set; }

    /// <summary>Recommended/assigned aircraft type, e.g. "A319".</summary>
    [MaxLength(20)]
    public string AircraftType { get; set; } = null!;

    /// <summary>Great-circle distance in nautical miles.</summary>
    public int Distance { get; set; }

    /// <summary>Planned block time in minutes.</summary>
    public int PlannedTime { get; set; }

    /// <summary>
    /// Operating weekdays, ISO 8601 (1 = Mon … 7 = Sun). Persisted as a jsonb
    /// integer array.
    /// </summary>
    public List<int> Days { get; set; } = new();

    /// <summary>
    /// True for ad-hoc charter legs created via Outstation Flights — these are
    /// one-offs outside the published schedule and are hidden from Bookings.
    /// </summary>
    public bool IsOutstation { get; set; }

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<Pirep> Pireps { get; set; } = new List<Pirep>();
}
