namespace AirSerbiaVirtua.Acars.Core;

// Client-side mirrors of the server DTOs. Property names serialize to camelCase
// (System.Text.Json default), matching the ASP.NET Core API.

public record LoginRequest(string Callsign, string Password);

public record RefreshRequest(string RefreshToken);

public record PilotProfile(
    int Id, string Callsign, string Name, string Email,
    int RankId, string RankName, decimal TotalHours,
    int Status, string HubId, DateTimeOffset DateJoined);

public record AuthTokens(
    string AccessToken,
    DateTimeOffset AccessExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshExpiresAtUtc);

public record LoginResponse(AuthTokens Tokens, PilotProfile Pilot);

public record RefreshResponse(AuthTokens Tokens);

public record AirportDto(
    string Icao, string? Iata, string Name, string Country,
    double Lat, double Lon, int Elevation, int Revision);

public record AirportSyncResponse(int Since, int LatestRevision, int Count, List<AirportDto> Airports);

public record FlightStartRequest(int RouteId, int AircraftId, DateOnly Date);

public record FlightStartResponse(
    int FlightSessionId, int RouteId, int AircraftId,
    string AircraftRegistration, DateTimeOffset StartedAtUtc);

/// <summary>POSREP payload. <see cref="Phase"/> uses the API's FlightPhase ordinal.</summary>
public record PositionReport(
    DateTimeOffset Timestamp, double Lat, double Lon,
    int AltFt, int GsKts, int Phase);

public record PirepSubmitRequest(
    int FlightSessionId, DateTimeOffset DepActual, DateTimeOffset ArrActual,
    int BlockMin, int AirMin, int FuelUsedKg, int LandingRateFpm,
    string Source, string? RawJson);

public record PirepResult(
    int PirepId, int Status, int Score, int LandingRateFpm,
    int BlockMin, decimal PilotTotalHours, int RankId, string RankName, bool Promoted);

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
