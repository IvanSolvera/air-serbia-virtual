using AirSerbiaVirtua.Contracts;

namespace AirSerbiaVirtua.Acars.Core;

// =============================================================================
// W4: all wire DTOs moved to AirSerbiaVirtua.Contracts (single source for API,
// desktop and web). Only sim-side helpers remain here — they depend on
// FlightData / FlightState, which the Contracts project must never see.
// =============================================================================

/// <summary>Builds POSREP wire records from live telemetry.</summary>
public static class PositionReports
{
    /// <summary>Builds a POSREP from a telemetry sample, assigning a fresh report id.</summary>
    public static PositionReport FromTelemetry(FlightData data, FlightState phase) =>
        new(
            ClientReportId: Guid.NewGuid(),
            Timestamp: data.SampleTimeUtc,
            Lat: data.LatitudeDeg,
            Lon: data.LongitudeDeg,
            AltFt: (int)Math.Round(data.AltitudeFt),
            GsKts: (int)Math.Round(data.GroundSpeedKts),
            Phase: phase.ToApiPhase());
}

public static class PhaseMapping
{
    /// <summary>Maps the client state machine's phase to the API FlightPhase.</summary>
    public static FlightPhase ToApiPhase(this FlightState state) => state switch
    {
        FlightState.Preflight => FlightPhase.Preflight,
        FlightState.Boarding => FlightPhase.Preflight,
        FlightState.Taxi => FlightPhase.Taxi,
        FlightState.Takeoff => FlightPhase.Takeoff,
        FlightState.Climb => FlightPhase.Climb,
        FlightState.Cruise => FlightPhase.Cruise,
        FlightState.Descent => FlightPhase.Descent,
        FlightState.Landing => FlightPhase.Landing,
        FlightState.TaxiIn => FlightPhase.TaxiIn,
        FlightState.Arrived => FlightPhase.Shutdown,
        _ => FlightPhase.Preflight
    };
}
