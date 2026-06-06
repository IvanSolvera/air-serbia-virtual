namespace AirSerbiaVirtua.Contracts;

// =============================================================================
// Shared DTO contracts for all AirSerbiaVirtua clients (website, desktop ACARS).
// Mirrors the server DTOs in AirSerbiaVirtua.Api — property names serialize to
// camelCase (System.Text.Json default).
//
// NOTE (W1): the desktop client still carries its own copies of these records
// in AirSerbiaVirtua.Acars.Core/ApiContracts.cs. They converge here in phase W4
// (desktop slim-down) — until then, keep field changes in sync in both files.
// =============================================================================

// ---- Auth -------------------------------------------------------------------
public record LoginRequest(string Callsign, string Password);

public record RefreshRequest(string RefreshToken);

public record RegisterRequest(
    string Callsign, string Name, string Email, string Password, string HubIcao);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record PilotProfile(
    int Id, string Callsign, string Name, string Email,
    int RankId, string RankName, decimal TotalHours,
    int Status, string HubId, DateTimeOffset DateJoined,
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
    int PlannedTimeMin,
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
    DateOnly Date,
    int Status);

public record CreateBookingRequest(int RouteId, DateOnly Date);

// ---- PIREPs ---------------------------------------------------------------------
public record PirepListItem(
    int Id,
    string FlightNumber,
    string DepIcao,
    string ArrIcao,
    string AircraftType,
    string Registration,
    DateTimeOffset DepActual,
    DateTimeOffset ArrActual,
    int BlockMin,
    int AirMin,
    int FuelUsedKg,
    int LandingRateFpm,
    int Score,
    int Status);

// ---- Weather ----------------------------------------------------------------------
public record MetarInfo(string Icao, string? Raw, DateTimeOffset? ObservedAtUtc);

// ---- Admin -----------------------------------------------------------------------
public record AdminPilot(
    int Id, string Callsign, string Name, string Email,
    string RankName, decimal TotalHours,
    int Status, string HubId, DateTimeOffset DateJoined,
    bool IsAdmin);
