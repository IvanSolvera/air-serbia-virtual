using System.ComponentModel.DataAnnotations;

namespace AirSerbiaVirtua.Api.Models;

/// <summary>
/// A pilot rank. Pilots are promoted as their logged hours pass
/// <see cref="MinHours"/>. <see cref="AllowedAircraftTypes"/> gates which
/// fleet types a pilot at this rank may book and fly.
/// </summary>
public class Rank
{
    public int Id { get; set; }

    [MaxLength(60)]
    public string Name { get; set; } = null!;

    /// <summary>Minimum total logged hours required to hold this rank.</summary>
    public decimal MinHours { get; set; }

    /// <summary>
    /// Aircraft types this rank may operate, e.g. ["A319","A320","ATR72"].
    /// Persisted as a jsonb string array.
    /// </summary>
    public List<string> AllowedAircraftTypes { get; set; } = new();

    public ICollection<Pilot> Pilots { get; set; } = new List<Pilot>();
}
