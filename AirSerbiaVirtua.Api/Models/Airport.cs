using System.ComponentModel.DataAnnotations;

namespace AirSerbiaVirtua.Api.Models;

/// <summary>
/// A navigation database airport. Keyed by ICAO code so that pilots, aircraft
/// and routes can reference it directly. <see cref="Revision"/> tracks the
/// nav-data cycle the record was last sourced from.
/// </summary>
public class Airport
{
    /// <summary>4-letter ICAO identifier, e.g. "LYBE". Primary key.</summary>
    [MaxLength(4)]
    public string Icao { get; set; } = null!;

    /// <summary>3-letter IATA identifier, e.g. "BEG".</summary>
    [MaxLength(3)]
    public string? Iata { get; set; }

    [MaxLength(150)]
    public string Name { get; set; } = null!;

    [MaxLength(80)]
    public string Country { get; set; } = null!;

    public double Lat { get; set; }

    public double Lon { get; set; }

    /// <summary>Field elevation in feet.</summary>
    public int Elevation { get; set; }

    /// <summary>Nav-data revision / AIRAC cycle the record was sourced from.</summary>
    public int Revision { get; set; }
}
