using System.ComponentModel.DataAnnotations;

namespace AirSerbiaVirtua.Api.Models;

/// <summary>
/// A Pilot Report: the record of a completed flight, typically submitted by an
/// ACARS client. <see cref="RawJson"/> retains the original client payload for
/// auditing; <see cref="PositionLogs"/> holds the recorded track.
/// </summary>
public class Pirep
{
    public int Id { get; set; }

    public int PilotId { get; set; }
    public Pilot? Pilot { get; set; }

    public int RouteId { get; set; }
    public Route? Route { get; set; }

    public int AircraftId { get; set; }
    public Aircraft? Aircraft { get; set; }

    /// <summary>Actual off-block / departure timestamp (UTC).</summary>
    public DateTimeOffset DepActual { get; set; }

    /// <summary>Actual on-block / arrival timestamp (UTC).</summary>
    public DateTimeOffset ArrActual { get; set; }

    /// <summary>Block time in minutes (off-block to on-block).</summary>
    public int BlockMin { get; set; }

    /// <summary>Airborne time in minutes (wheels-up to touchdown).</summary>
    public int AirMin { get; set; }

    public int FuelUsedKg { get; set; }

    /// <summary>Touchdown vertical speed in feet per minute (negative = down).</summary>
    public int LandingRateFpm { get; set; }

    /// <summary>Computed flight score (0-100).</summary>
    public int Score { get; set; }

    public PirepStatus Status { get; set; } = PirepStatus.Pending;

    /// <summary>Origin of the report, e.g. "ACARS", "Manual", "smartCARS".</summary>
    [MaxLength(40)]
    public string Source { get; set; } = null!;

    /// <summary>Raw client payload, persisted as jsonb.</summary>
    public string? RawJson { get; set; }

    public ICollection<PositionLog> PositionLogs { get; set; } = new List<PositionLog>();
}
