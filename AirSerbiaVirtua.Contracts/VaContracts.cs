namespace AirSerbiaVirtua.Contracts;

// =============================================================================
// Shared DTO contracts for ALL AirSerbiaVirtua parties: the API serves these,
// the desktop ACARS client and the web portal consume them. Property names
// serialize to camelCase (System.Text.Json web defaults); enums serialize as
// numbers (no JsonStringEnumConverter anywhere — keep it that way or every
// client breaks). Wire shapes are locked by Tests.Unit/Contracts/WireShapeTests.
// =============================================================================

// ---- Auth -------------------------------------------------------------------
public record LoginRequest(string Callsign, string Password);

public record RefreshRequest(string RefreshToken);

public record RegisterRequest(
    string Callsign, string Name, string Email, string Password, string HubIcao,
    string? VatsimId = null);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record PilotProfile(
    int Id, string Callsign, string Name, string Email,
    int RankId, string RankName, decimal TotalHours,
    PilotStatus Status, string HubId, DateTimeOffset DateJoined,
    bool IsAdmin = false);

public record AuthTokens(
    string AccessToken,
    DateTimeOffset AccessExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshExpiresAtUtc);

public record LoginResponse(AuthTokens Tokens, PilotProfile Pilot);

public record RefreshResponse(AuthTokens Tokens);

// ---- Schedules / routes -------------------------------------------------------
public record RouteInfo(
    int Id,
    string FlightNumber,
    string DepIcao,
    string ArrIcao,
    string AircraftType,
    int DistanceNm,
    int PlannedMinutes,
    List<int> Days);

// ---- Fleet ---------------------------------------------------------------------
public record AircraftInfo(
    int Id,
    string Type,
    string Registration,
    string Status,
    string HubId);

// ---- Bookings -------------------------------------------------------------------
public record BookingInfo(
    int Id,
    int RouteId,
    string FlightNumber,
    string DepIcao,
    string ArrIcao,
    string AircraftType,
    int PlannedMinutes,
    DateOnly Date,
    BookingStatus Status,
    DateTimeOffset? DispatchReadyAtUtc = null);

public record CreateBookingRequest(int RouteId, DateOnly Date);

// ---- Flight session ---------------------------------------------------------
public record FlightStartRequest(int RouteId, int AircraftId, DateOnly Date);

public record FlightStartResponse(
    int FlightSessionId, int RouteId, int AircraftId,
    string AircraftRegistration, DateTimeOffset StartedAtUtc);

// ---- Outstation (ad-hoc charter) -----------------------------------------------
public record OutstationStartRequest(
    string DepIcao, string ArrIcao, int AircraftId, string FlightNumber);

// ---- Position report (POSREP) ----------------------------------------------
/// <summary>
/// POSREP wire payload. <see cref="ClientReportId"/> is generated once per
/// sample and stays stable across retries so the server can dedupe
/// (production-readiness item #5). The telemetry-to-report factory lives in
/// Acars.Core (PositionReports.FromTelemetry) — it needs sim-side types.
/// </summary>
public record PositionReport(
    Guid ClientReportId,
    DateTimeOffset Timestamp, double Lat, double Lon,
    int AltFt, int GsKts, FlightPhase Phase);

// ---- PIREP submission -------------------------------------------------------
public record PirepSubmitRequest(
    int FlightSessionId, DateTimeOffset DepActual, DateTimeOffset ArrActual,
    int BlockMin, int AirMin, int FuelUsedKg, int LandingRateFpm,
    string Source, string? RawJson);

public record PirepResult(
    int PirepId, PirepStatus Status, int Score, int LandingRateFpm,
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
    PirepStatus Status);

// ---- Airport sync -----------------------------------------------------------
public record AirportDto(
    string Icao, string? Iata, string Name, string Country,
    double Lat, double Lon, int Elevation, int Revision);

public record AirportSyncResponse(int Since, int LatestRevision, int Count, List<AirportDto> Airports);

// ---- Weather ----------------------------------------------------------------------
public record MetarInfo(string Icao, string? Raw, DateTimeOffset? ObservedAtUtc);

// ---- Public stats (website landing) -------------------------------------------------
public record VaStats(int Pilots, int FlightsFlown, decimal HoursLogged, int Routes);

// ---- Admin -----------------------------------------------------------------------
public record AdminPilot(
    int Id, string Callsign, string Name, string Email,
    string RankName, decimal TotalHours,
    PilotStatus Status, string HubId, DateTimeOffset DateJoined,
    bool IsAdmin, string? VatsimId = null);
