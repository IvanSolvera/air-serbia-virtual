namespace AirSerbiaVirtua.Acars.Core;

/// <summary>
/// An immutable snapshot of simulator telemetry for a single poll, already
/// converted into standard aviation units.
/// </summary>
public sealed record FlightData
{
    public DateTimeOffset SampleTimeUtc { get; init; } = DateTimeOffset.UtcNow;

    // Position (decimal degrees) and altitude (feet MSL).
    public double LatitudeDeg { get; init; }
    public double LongitudeDeg { get; init; }
    public double AltitudeFt { get; init; }

    // Speeds.
    public double IndicatedAirspeedKts { get; init; }
    public double GroundSpeedKts { get; init; }
    public double VerticalSpeedFpm { get; init; }

    // Discrete status.
    public bool OnGround { get; init; }
    public bool ParkingBrakeSet { get; init; }

    // ATC / identity strings.
    public string FlightNumber { get; init; } = string.Empty;
    public string TailNumber { get; init; } = string.Empty;

    // Fuel on board (kilograms).
    public double FuelKg { get; init; }

    public string ToConsoleBlock() =>
        $"""
         â”€â”€ {SampleTimeUtc:HH:mm:ss} UTC â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
          Flight {FlightNumber,-8} Reg {TailNumber,-8}
          Pos   {LatitudeDeg,11:F6}, {LongitudeDeg,11:F6}   Alt {AltitudeFt,8:F0} ft
          Speed IAS {IndicatedAirspeedKts,6:F1} kt   GS {GroundSpeedKts,6:F1} kt   VS {VerticalSpeedFpm,7:F0} fpm
          State {(OnGround ? "ON GROUND" : "AIRBORNE "),-9}  Parking brake {(ParkingBrakeSet ? "SET" : "OFF")}
          Fuel  {FuelKg,9:F1} kg
         """;
}
