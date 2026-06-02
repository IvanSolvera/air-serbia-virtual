using System.ComponentModel.DataAnnotations;

namespace AirSerbiaVirtua.Api.Models;

/// <summary>
/// A single position sample within a <see cref="Pirep"/>'s recorded track.
/// High-volume table: keyed by a 64-bit id.
/// </summary>
public class PositionLog
{
    public long Id { get; set; }

    public int PirepId { get; set; }
    public Pirep? Pirep { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    public double Lat { get; set; }

    public double Lon { get; set; }

    /// <summary>Altitude in feet (barometric/MSL).</summary>
    public int AltFt { get; set; }

    /// <summary>Ground speed in knots.</summary>
    public int GsKts { get; set; }

    public FlightPhase Phase { get; set; }
}
