namespace AirSerbiaVirtua.Acars.Core;

// Client-side mirrors of the server DTOs. Property names serialize to camelCase
// (System.Text.Json default), matching the ASP.NET Core API.

public record LoginRequest(string Callsign, string Password);

public record RefreshRequest(string RefreshToken);

public record PilotProfile(
    int Id, string Callsign, string Name, string Email,
    int RankId, string RankName, decimal TotalHours,
    int Status, string HubId, DateTimeOffset DateJoined,
    bool IsAdmin = false);

// ---- Admin (roster management) ------------------------------------------------
public record AdminPilot(
    int Id, string Callsign, string Name, string Email,
    string RankName, decimal TotalHours,
    int Status, string HubId, DateTimeOffset DateJoined,
    bool IsAdmin, string? VatsimId = null);

// ---- Outstation (ad-hoc charter) -----------------------------------------------
public record OutstationStartRequest(
    string DepIcao, string ArrIcao, int AircraftId, string FlightNumber);

public record AuthTokens(
    string AccessToken,
    DateTimeOffset AccessExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshExpiresAtUtc);

public record LoginResponse(AuthTokens Tokens, PilotProfile Pilot);

public record RefreshResponse(AuthTokens Tokens);

// ---- Routes & Bookings ------------------------------------------------------
public record ApiRoute(
    int Id,
    string FlightNumber,
    string DepIcao,
    string ArrIcao,
    string AircraftType,
    int DistanceNm,
    int PlannedMinutes,
    List<int> Days);

public record CreateBookingRequest(int RouteId, DateOnly Date);

public record ApiBooking(
    int Id,
    int RouteId,
    string FlightNumber,
    string DepIcao,
    string ArrIcao,
    string AircraftType,
    int PlannedMinutes,
    DateOnly Date,
    int Status);

/// <summary>Mirror of API BookingStatus enum.</summary>
public enum ApiBookingStatus
{
    Open = 0, Confirmed = 1, Flown = 2, Cancelled = 3, Expired = 4
}

public record ApiAircraft(
    int Id,
    string Type,
    string Registration,
    string Status,
    string HubId);

public record AirportDto(
    string Icao, string? Iata, string Name, string Country,
    double Lat, double Lon, int Elevation, int Revision);

public record AirportSyncResponse(int Since, int LatestRevision, int Count, List<AirportDto> Airports);

public record FlightStartRequest(int RouteId, int AircraftId, DateOnly Date);

public record FlightStartResponse(
    int FlightSessionId, int RouteId, int AircraftId,
    string AircraftRegistration, DateTimeOffset StartedAtUtc);

/// <summary>
/// POSREP payload. <see cref="Phase"/> uses the API's FlightPhase ordinal.
/// <see cref="ClientReportId"/> is generated once per sample and stays stable
/// across retries so the server can dedupe (production-readiness item #5).
/// </summary>
public record PositionReport(
    Guid ClientReportId,
    DateTimeOffset Timestamp, double Lat, double Lon,
    int AltFt, int GsKts, int Phase)
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
            Phase: (int)phase.ToApiPhase());
}

public record PirepSubmitRequest(
    int FlightSessionId, DateTimeOffset DepActual, DateTimeOffset ArrActual,
    int BlockMin, int AirMin, int FuelUsedKg, int LandingRateFpm,
    string Source, string? RawJson);

public record PirepResult(
    int PirepId, int Status, int Score, int LandingRateFpm,
    int BlockMin, decimal PilotTotalHours, int RankId, string RankName, bool Promoted);

// ---- Logbook ----------------------------------------------------------------
public record PirepListItem(
    int Id,
    string FlightNumber,
    string DepIcao,
    string ArrIcao,
    string AircraftType,
    string AircraftRegistration,
    DateTimeOffset DepActual,
    DateTimeOffset ArrActual,
    int BlockMin,
    int AirMin,
    int FuelUsedKg,
    int LandingRateFpm,
    int Score,
    int Status);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

// ---- Weather (METAR) --------------------------------------------------------
public record MetarInfo(string Icao, string? Raw, DateTimeOffset? ObservedAtUtc);

/// <summary>API FlightPhase ordinals (must match AirSerbiaVirtua.Api.Models.FlightPhase).</summary>
public enum ApiFlightPhase
{
    Preflight = 0, Pushback = 1, Taxi = 2, Takeoff = 3, Climb = 4,
    Cruise = 5, Descent = 6, Approach = 7, Landing = 8, TaxiIn = 9, Shutdown = 10
}

public static class PhaseMapping
{
    /// <summary>Maps the client state machine's phase to the API FlightPhase.</summary>
    public static ApiFlightPhase ToApiPhase(this FlightState state) => state switch
    {
        FlightState.Preflight => ApiFlightPhase.Preflight,
        FlightState.Boarding => ApiFlightPhase.Preflight,
        FlightState.Taxi => ApiFlightPhase.Taxi,
        FlightState.Takeoff => ApiFlightPhase.Takeoff,
        FlightState.Climb => ApiFlightPhase.Climb,
        FlightState.Cruise => ApiFlightPhase.Cruise,
        FlightState.Descent => ApiFlightPhase.Descent,
        FlightState.Landing => ApiFlightPhase.Landing,
        FlightState.TaxiIn => ApiFlightPhase.TaxiIn,
        FlightState.Arrived => ApiFlightPhase.Shutdown,
        _ => ApiFlightPhase.Preflight
    };
}
