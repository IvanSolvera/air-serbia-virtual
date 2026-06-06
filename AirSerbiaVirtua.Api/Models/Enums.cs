namespace AirSerbiaVirtua.Api.Models;

/// <summary>Lifecycle state of a virtual pilot.</summary>
public enum PilotStatus
{
    Pending = 0,
    Active = 1,
    OnLeave = 2,
    Inactive = 3,
    Banned = 4
}

/// <summary>Operational state of an airframe in the fleet.</summary>
public enum AircraftStatus
{
    Active = 0,
    InFlight = 1,
    Maintenance = 2,
    Stored = 3,
    Retired = 4
}

/// <summary>State of a route booking made by a pilot.</summary>
public enum BookingStatus
{
    Open = 0,
    Confirmed = 1,
    Flown = 2,
    Cancelled = 3,
    Expired = 4
}

/// <summary>State of a submitted pilot report (PIREP).</summary>
public enum PirepStatus
{
    Pending = 0,
    Accepted = 1,
    Rejected = 2,
    UnderReview = 3,
    /// <summary>Flight was abandoned (sim crash, user abort) — first-class outcome, not a stuck Pending.</summary>
    Aborted = 4
}

/// <summary>Phase of flight for a recorded position sample.</summary>
public enum FlightPhase
{
    Preflight = 0,
    Pushback = 1,
    Taxi = 2,
    Takeoff = 3,
    Climb = 4,
    Cruise = 5,
    Descent = 6,
    Approach = 7,
    Landing = 8,
    TaxiIn = 9,
    Shutdown = 10
}
