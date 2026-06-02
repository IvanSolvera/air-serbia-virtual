using System.ComponentModel.DataAnnotations;

namespace AirSerbiaVirtua.Api.Models;

/// <summary>
/// A single airframe in the fleet. <see cref="Registration"/> is the unique
/// tail (e.g. "YU-API"); <see cref="HubId"/> references the based
/// <see cref="Airport"/> by ICAO.
/// </summary>
public class Aircraft
{
    public int Id { get; set; }

    /// <summary>ICAO/marketing type, e.g. "A320", "A320neo", "ATR72".</summary>
    [MaxLength(20)]
    public string Type { get; set; } = null!;

    /// <summary>Unique tail registration, e.g. "YU-API".</summary>
    [MaxLength(10)]
    public string Registration { get; set; } = null!;

    /// <summary>SELCAL code, e.g. "ABCD".</summary>
    [MaxLength(5)]
    public string? Selcal { get; set; }

    /// <summary>Based hub ICAO, references <see cref="Airport.Icao"/>.</summary>
    [MaxLength(4)]
    public string HubId { get; set; } = null!;
    public Airport? Hub { get; set; }

    public AircraftStatus Status { get; set; } = AircraftStatus.Active;

    public ICollection<Pirep> Pireps { get; set; } = new List<Pirep>();
}
