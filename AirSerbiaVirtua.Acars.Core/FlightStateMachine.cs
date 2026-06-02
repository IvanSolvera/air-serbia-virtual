namespace AirSerbiaVirtua.Acars.Core;

/// <summary>Phases of a tracked flight, in order.</summary>
public enum FlightState
{
    Preflight,
    Boarding,
    Taxi,
    Takeoff,
    Climb,
    Cruise,
    Descent,
    Landing,
    TaxiIn,
    Arrived
}

/// <summary>
/// Drives the flight phase transitions from a stream of <see cref="FlightData"/>
/// samples. Designed to be fed one sample per poll (â‰ˆ1 Hz) via <see cref="Update"/>,
/// but is fully deterministic and time-driven off each sample's timestamp, so it
/// can be exercised with mock data in tests.
///
/// Captured milestones:
///   OffBlock  â€” first movement out of the gate (brake released, rolling)
///   Takeoff   â€” wheels-up (on-ground 1â†’0)
///   Landing   â€” touchdown (on-ground 0â†’1); landing rate captured at this instant
///   OnBlock   â€” parked at gate (brake set, stopped)
/// </summary>
public sealed class FlightStateMachine
{
    // ---- Tunable thresholds -------------------------------------------------
    public double TaxiGsKts { get; init; } = 1.0;
    public double CruiseVsBandFpm { get; init; } = 200.0;
    public double CruiseStableSeconds { get; init; } = 30.0;
    public double CruiseAltToleranceFt { get; init; } = 200.0;
    public double DescentVsFpm { get; init; } = -300.0;
    public double DescentSustainSeconds { get; init; } = 15.0;
    public double TaxiInGsKts { get; init; } = 30.0;
    public double ArrivedGsKts { get; init; } = 1.0;

    /// <summary>Planned cruise altitude (ft MSL); enables the altitude-based Climbâ†’Cruise trigger.</summary>
    public double? PlannedCruiseAltFt { get; set; }

    public FlightState State { get; private set; } = FlightState.Preflight;

    // ---- Captured milestones ------------------------------------------------
    public DateTimeOffset? OffBlockUtc { get; private set; }
    public DateTimeOffset? TakeoffUtc { get; private set; }
    public DateTimeOffset? LandingUtc { get; private set; }
    public DateTimeOffset? OnBlockUtc { get; private set; }

    public double FuelAtStartKg { get; private set; }
    public double FuelAtEndKg { get; private set; }

    /// <summary>Exact vertical speed (fpm) captured at the moment of touchdown.</summary>
    public double LandingRateFpm { get; private set; }

    // ---- Derived metrics ----------------------------------------------------
    public TimeSpan? BlockTime =>
        (OffBlockUtc is { } ob && OnBlockUtc is { } onb) ? onb - ob : null;

    public TimeSpan? AirTime =>
        (TakeoffUtc is { } to && LandingUtc is { } ld) ? ld - to : null;

    public double? FuelUsedKg =>
        (OffBlockUtc is not null && OnBlockUtc is not null) ? FuelAtStartKg - FuelAtEndKg : null;

    /// <summary>Raised whenever the state changes, with (previous, next, triggering sample).</summary>
    public event Action<FlightState, FlightState, FlightData>? StateChanged;

    // ---- Internal tracking --------------------------------------------------
    private FlightData? _previous;
    private DateTimeOffset? _cruiseStableSince;
    private DateTimeOffset? _descentSince;

    /// <summary>
    /// Feed one telemetry sample. Returns true if the state changed on this sample.
    /// </summary>
    public bool Update(FlightData s)
    {
        var from = State;

        switch (State)
        {
            case FlightState.Preflight:
            case FlightState.Boarding:
                // First movement out of the gate.
                if (!s.ParkingBrakeSet && s.GroundSpeedKts > TaxiGsKts)
                {
                    OffBlockUtc = s.SampleTimeUtc;
                    FuelAtStartKg = s.FuelKg;
                    TransitionTo(FlightState.Taxi, s);
                }
                break;

            case FlightState.Taxi:
                // Wheels-up.
                if (WentAirborne(s))
                {
                    TakeoffUtc = s.SampleTimeUtc;
                    TransitionTo(FlightState.Takeoff, s);
                }
                break;

            case FlightState.Takeoff:
                // Brief liftoff phase; once a positive climb is established, we are climbing.
                if (!s.OnGround)
                    TransitionTo(FlightState.Climb, s);
                break;

            case FlightState.Climb:
                if (ReachedCruise(s))
                {
                    _cruiseStableSince = null;
                    TransitionTo(FlightState.Cruise, s);
                }
                break;

            case FlightState.Cruise:
                // Sustained descent below the threshold.
                if (s.VerticalSpeedFpm < DescentVsFpm)
                {
                    _descentSince ??= s.SampleTimeUtc;
                    if ((s.SampleTimeUtc - _descentSince.Value).TotalSeconds >= DescentSustainSeconds)
                        TransitionTo(FlightState.Descent, s);
                }
                else
                {
                    _descentSince = null;
                }
                break;

            case FlightState.Descent:
                // Touchdown â€” capture the landing rate at this instant.
                if (Touchdown(s))
                {
                    LandingUtc = s.SampleTimeUtc;
                    LandingRateFpm = CaptureTouchdownVs(s);
                    TransitionTo(FlightState.Landing, s);
                }
                break;

            case FlightState.Landing:
                if (s.OnGround && s.GroundSpeedKts < TaxiInGsKts)
                    TransitionTo(FlightState.TaxiIn, s);
                break;

            case FlightState.TaxiIn:
                if (s.ParkingBrakeSet && s.GroundSpeedKts < ArrivedGsKts)
                {
                    OnBlockUtc = s.SampleTimeUtc;
                    FuelAtEndKg = s.FuelKg;
                    TransitionTo(FlightState.Arrived, s);
                }
                break;

            case FlightState.Arrived:
                break; // Terminal.
        }

        _previous = s;
        return State != from;
    }

    /// <summary>Optional manual step Preflight â†’ Boarding (no telemetry trigger defined).</summary>
    public void MarkBoarding(FlightData s)
    {
        if (State == FlightState.Preflight)
            TransitionTo(FlightState.Boarding, s);
    }

    private bool WentAirborne(FlightData s) => _previous is { OnGround: true } && !s.OnGround;

    private bool Touchdown(FlightData s) => _previous is { OnGround: false } && s.OnGround;

    private bool ReachedCruise(FlightData s)
    {
        // Altitude-based trigger.
        if (PlannedCruiseAltFt is { } target && s.AltitudeFt >= target - CruiseAltToleranceFt)
            return true;

        // Vertical-speed-stabilised trigger: |VS| within band for the required dwell.
        if (Math.Abs(s.VerticalSpeedFpm) <= CruiseVsBandFpm)
        {
            _cruiseStableSince ??= s.SampleTimeUtc;
            return (s.SampleTimeUtc - _cruiseStableSince.Value).TotalSeconds >= CruiseStableSeconds;
        }

        _cruiseStableSince = null;
        return false;
    }

    /// <summary>
    /// At 1 Hz the exact touchdown can fall between samples; the on-ground sample
    /// often already reads VS â‰ˆ 0. Use the more-negative of the touchdown sample
    /// and the last airborne sample to best represent the real touchdown rate.
    /// </summary>
    private double CaptureTouchdownVs(FlightData s)
    {
        double current = s.VerticalSpeedFpm;
        double prior = _previous?.VerticalSpeedFpm ?? current;
        return Math.Min(current, prior);
    }

    private void TransitionTo(FlightState next, FlightData s)
    {
        var prev = State;
        State = next;
        StateChanged?.Invoke(prev, next, s);
    }
}
