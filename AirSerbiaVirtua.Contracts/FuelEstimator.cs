namespace AirSerbiaVirtua.Contracts;

/// <summary>
/// Rough planning fuel shared by the web Briefing page and the desktop client:
/// cruise burn per nm by type + a fixed reserve block (taxi + contingency +
/// 45 min final reserve). Indicative only — not a real OFP.
/// </summary>
public static class FuelEstimator
{
    /// <returns>(trip kg, reserve kg)</returns>
    public static (int TripKg, int ReserveKg) Estimate(string aircraftType, int distanceNm)
    {
        double burnPerNm = aircraftType.ToUpperInvariant() switch
        {
            "A319" => 7.0,
            "A320" => 7.4,
            "A321" => 8.2,
            "ATR72" or "AT72" or "ATR" => 2.6,
            "E195" or "E190" => 5.6,
            _ => 6.5
        };
        int trip = (int)Math.Round(distanceNm * burnPerNm);
        // Reserve: 45 min at ~ (burnPerNm * 360 nm/h) plus a fixed taxi/contingency pad.
        int reserve = (int)Math.Round(burnPerNm * 360 * 0.75) + 400;
        return (trip, reserve);
    }
}
