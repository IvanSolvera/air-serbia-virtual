using AirSerbiaVirtua.Api.Auth;
using AirSerbiaVirtua.Api.Data;
using AirSerbiaVirtua.Api.Models;
using AirSerbiaVirtua.Contracts;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AirSerbiaVirtua.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/flights")]
[Authorize]
public class FlightsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<FlightsController> _logger;

    public FlightsController(AppDbContext db, ILogger<FlightsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Starts a flight session: validates the pilot's booking, locks the aircraft
    /// (sets it InFlight) and opens a Pending PIREP that acts as the session.
    /// The PIREP id is returned as the flightSessionId.
    /// </summary>
    [HttpPost("start")]
    public async Task<ActionResult<FlightStartResponse>> Start([FromBody] FlightStartRequest req)
    {
        var pilotId = User.PilotId();
        if (pilotId is null) return Unauthorized();

        var route = await _db.Routes.FindAsync(req.RouteId);
        if (route is null) return BadRequest(new { message = "Unknown route." });

        var aircraft = await _db.Aircraft.FindAsync(req.AircraftId);
        if (aircraft is null) return BadRequest(new { message = "Unknown aircraft." });

        if (aircraft.Status != AircraftStatus.Active)
            return Conflict(new { message = $"Aircraft {aircraft.Registration} is not available ({aircraft.Status})." });

        var booking = await _db.Bookings.FirstOrDefaultAsync(b =>
            b.PilotId == pilotId &&
            b.RouteId == req.RouteId &&
            (b.Status == BookingStatus.Open || b.Status == BookingStatus.Confirmed));

        if (booking is null)
            return BadRequest(new { message = "No open booking for this pilot and route." });

        // Open the session.
        var now = DateTimeOffset.UtcNow;
        var pirep = new Pirep
        {
            PilotId = pilotId.Value,
            RouteId = req.RouteId,
            AircraftId = req.AircraftId,
            DepActual = now,
            ArrActual = now,
            Status = PirepStatus.Pending,
            Source = "ACARS"
        };
        _db.Pireps.Add(pirep);

        aircraft.Status = AircraftStatus.InFlight;     // lock the airframe
        booking.Status = BookingStatus.Confirmed;

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Flight session {FlightSessionId} started on route {RouteId} with aircraft {Registration}",
            pirep.Id, route.Id, aircraft.Registration);

        return Ok(new FlightStartResponse(pirep.Id, route.Id, aircraft.Id, aircraft.Registration, now));
    }

    /// <summary>
    /// Starts an outstation (ad-hoc charter) flight outside the published schedule:
    /// creates a one-off Route flagged IsOutstation (auto-registering unknown
    /// airports), locks the aircraft and opens a Pending PIREP session — no booking
    /// involved. Phase 5.
    /// </summary>
    [HttpPost("start-outstation")]
    public async Task<ActionResult<FlightStartResponse>> StartOutstation([FromBody] OutstationStartRequest req)
    {
        var pilotId = User.PilotId();
        if (pilotId is null) return Unauthorized();

        var dep = NormalizeIcao(req.DepIcao);
        var arr = NormalizeIcao(req.ArrIcao);
        if (dep is null || arr is null)
            return BadRequest(new { message = "Departure and arrival must be valid 4-letter ICAO codes." });
        if (dep == arr)
            return BadRequest(new { message = "Departure and arrival cannot be the same airport." });
        if (string.IsNullOrWhiteSpace(req.FlightNumber) || req.FlightNumber.Trim().Length > 10)
            return BadRequest(new { message = "Flight number is required (max 10 characters)." });

        var aircraft = await _db.Aircraft.FindAsync(req.AircraftId);
        if (aircraft is null) return BadRequest(new { message = "Unknown aircraft." });
        if (aircraft.Status != AircraftStatus.Active)
            return Conflict(new { message = $"Aircraft {aircraft.Registration} is not available ({aircraft.Status})." });

        // Outstations may serve airports outside the nav database — register
        // minimal stubs so FK references hold; a nav-data sync can enrich later.
        var depAirport = await EnsureAirportAsync(dep);
        var arrAirport = await EnsureAirportAsync(arr);

        var distanceNm = GreatCircleNm(depAirport, arrAirport);

        var route = new Models.Route
        {
            FlightNumber = req.FlightNumber.Trim().ToUpperInvariant(),
            DepIcao = dep,
            ArrIcao = arr,
            AircraftType = aircraft.Type,
            Distance = distanceNm,
            PlannedTime = distanceNm > 0 ? (int)(distanceNm / 7.5) + 25 : 0,   // ~450 kts + taxi pad
            Days = [],
            IsOutstation = true
        };
        _db.Routes.Add(route);

        var now = DateTimeOffset.UtcNow;
        var pirep = new Pirep
        {
            PilotId = pilotId.Value,
            Route = route,
            AircraftId = aircraft.Id,
            DepActual = now,
            ArrActual = now,
            Status = PirepStatus.Pending,
            Source = "ACARS"
        };
        _db.Pireps.Add(pirep);

        aircraft.Status = AircraftStatus.InFlight;

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Outstation flight session {FlightSessionId} started: {FlightNumber} {Dep}->{Arr} on {Registration}",
            pirep.Id, route.FlightNumber, dep, arr, aircraft.Registration);

        return Ok(new FlightStartResponse(pirep.Id, route.Id, aircraft.Id, aircraft.Registration, now));
    }

    private static string? NormalizeIcao(string? raw)
    {
        var icao = raw?.Trim().ToUpperInvariant();
        return icao is { Length: 4 } && icao.All(char.IsLetter) ? icao : null;
    }

    private async Task<Airport> EnsureAirportAsync(string icao)
    {
        var airport = await _db.Airports.FindAsync(icao);
        if (airport is not null) return airport;

        airport = new Airport
        {
            Icao = icao,
            Name = $"{icao} (outstation)",
            Country = "",
            Lat = 0,
            Lon = 0,
            Elevation = 0,
            Revision = 0
        };
        _db.Airports.Add(airport);
        return airport;
    }

    /// <summary>Great-circle distance in nm; 0 when either airport lacks coordinates.</summary>
    private static int GreatCircleNm(Airport a, Airport b)
    {
        if ((a.Lat == 0 && a.Lon == 0) || (b.Lat == 0 && b.Lon == 0)) return 0;
        const double earthRadiusNm = 3440.065;
        double ToRad(double deg) => deg * Math.PI / 180.0;
        var dLat = ToRad(b.Lat - a.Lat);
        var dLon = ToRad(b.Lon - a.Lon);
        var h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRad(a.Lat)) * Math.Cos(ToRad(b.Lat)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return (int)Math.Round(2 * earthRadiusNm * Math.Asin(Math.Sqrt(h)));
    }

    /// <summary>
    /// Aborts an active flight session (sim crash, user abandon): marks the PIREP
    /// Aborted, releases the aircraft and re-opens the booking so the pilot can
    /// fly the leg again. GVA-inspired item #14 — failed flights are first-class.
    /// </summary>
    [HttpPost("{id:int}/abort")]
    public async Task<IActionResult> Abort(int id)
    {
        var pilotId = User.PilotId();
        if (pilotId is null) return Unauthorized();

        var pirep = await _db.Pireps.FirstOrDefaultAsync(p => p.Id == id && p.PilotId == pilotId);
        if (pirep is null) return NotFound(new { message = "Flight session not found." });
        if (pirep.Status != PirepStatus.Pending)
            return Conflict(new { message = "Flight session is no longer active." });

        pirep.Status = PirepStatus.Aborted;
        pirep.ArrActual = DateTimeOffset.UtcNow;

        var aircraft = await _db.Aircraft.FindAsync(pirep.AircraftId);
        if (aircraft is { Status: AircraftStatus.InFlight })
            aircraft.Status = AircraftStatus.Active;

        var booking = await _db.Bookings.FirstOrDefaultAsync(b =>
            b.PilotId == pilotId &&
            b.RouteId == pirep.RouteId &&
            b.Status == BookingStatus.Confirmed);
        if (booking is not null)
            booking.Status = BookingStatus.Open;   // leg can be re-flown

        await _db.SaveChangesAsync();

        _logger.LogInformation("Flight session {FlightSessionId} aborted by pilot", id);
        return Ok(new { message = "Flight aborted." });
    }

    /// <summary>
    /// Receives a POSREP telemetry sample (pushed ~every 30 s) and appends it to
    /// the session's position log. Idempotent: a client retry carrying the same
    /// <see cref="PositionReport.ClientReportId"/> is accepted without creating
    /// a duplicate row (production-readiness item #5).
    /// </summary>
    [HttpPost("{id:int}/position")]
    public async Task<IActionResult> Position(int id, [FromBody] PositionReport report)
    {
        var pilotId = User.PilotId();
        if (pilotId is null) return Unauthorized();

        var pirep = await _db.Pireps.FirstOrDefaultAsync(p => p.Id == id && p.PilotId == pilotId);
        if (pirep is null) return NotFound(new { message = "Flight session not found." });
        if (pirep.Status != PirepStatus.Pending)
            return Conflict(new { message = "Flight session is no longer active." });

        // An old client (or a replayed empty id) must not collide with other
        // samples on the (PirepId, ClientReportId) unique index — give it a real id.
        var clientReportId = report.ClientReportId == Guid.Empty ? Guid.NewGuid() : report.ClientReportId;

        _db.PositionLogs.Add(new PositionLog
        {
            PirepId = id,
            ClientReportId = clientReportId,
            Timestamp = report.Timestamp,
            Lat = report.Lat,
            Lon = report.Lon,
            AltFt = report.AltFt,
            GsKts = report.GsKts,
            Phase = report.Phase
        });

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Likely a duplicate from a client retry. If the sample is already
            // stored, report success so the client can drop it from its queue.
            var alreadyStored = await _db.PositionLogs
                .AnyAsync(p => p.PirepId == id && p.ClientReportId == clientReportId);
            if (alreadyStored)
            {
                _logger.LogInformation(
                    "Duplicate POSREP {ClientReportId} for flight session {FlightSessionId} acknowledged",
                    clientReportId, id);
                return Ok(new { message = "Already received." });
            }
            throw;
        }

        _logger.LogInformation(
            "POSREP stored for flight session {FlightSessionId}: phase {Phase}, alt {AltFt} ft, gs {GsKts} kts",
            id, report.Phase, report.AltFt, report.GsKts);

        return Accepted();
    }
}
