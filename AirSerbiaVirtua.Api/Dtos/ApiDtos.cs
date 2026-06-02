using AirSerbiaVirtua.Api.Models;

namespace AirSerbiaVirtua.Api.Dtos;

// ---- Auth -------------------------------------------------------------------
public record LoginRequest(string Callsign, string Password);

public record RegisterRequest(
    string Callsign,
    string Name,
    string Email,
    string Password,
    string HubIcao);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record RefreshRequest(string RefreshToken);

public record PilotProfileDto(
    int Id, string Callsign, string Name, string Email,
    int RankId, string RankName, decimal TotalHours,
    PilotStatus Status, string HubId, DateTimeOffset DateJoined);

/// <summary>Both tokens issued by login or refresh.</summary>
public record AuthTokensDto(
    string AccessToken,
    DateTimeOffset AccessExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshExpiresAtUtc);

public record LoginResponse(AuthTokensDto Tokens, PilotProfileDto Pilot);

public record RefreshResponse(AuthTokensDto Tokens);

// ---- Airport sync -----------------------------------------------------------
public record AirportDto(
    string Icao, string? Iata, string Name, string Country,
    double Lat, double Lon, int Elevation, int Revision);

public record AirportSyncResponse(int Since, int LatestRevision, int Count, List<AirportDto> Airports);

// ---- Flight session ---------------------------------------------------------
public record FlightStartRequest(int RouteId, int AircraftId, DateOnly Date);

public record FlightStartResponse(
    int FlightSessionId, int RouteId, int AircraftId,
    string AircraftRegistration, DateTimeOffset StartedAtUtc);

// ---- Position report (POSREP) ----------------------------------------------
public record PositionReportDto(
    DateTimeOffset Timestamp, double Lat, double Lon,
    int AltFt, int GsKts, FlightPhase Phase);

// ---- PIREP submission -------------------------------------------------------
public record PirepSubmitRequest(
    int FlightSessionId,
    DateTimeOffset DepActual,
    DateTimeOffset ArrActual,
    int BlockMin,
    int AirMin,
    int FuelUsedKg,
    int LandingRateFpm,
    string Source,
    string? RawJson);

public record PirepResultDto(
    int PirepId, PirepStatus Status, int Score, int LandingRateFpm,
    int BlockMin, decimal PilotTotalHours, int RankId, string RankName, bool Promoted);
