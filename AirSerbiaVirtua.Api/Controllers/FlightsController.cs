using AirSerbiaVirtua.Api.Auth;
using AirSerbiaVirtua.Api.Data;
using AirSerbiaVirtua.Api.Dtos;
using AirSerbiaVirtua.Api.Models;
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
    /// Receives a POSREP telemetry sample (pushed ~every 30 s) and appends it to
    /// the session's position log. Idempotent: a client retry carrying the same
    /// <see cref="PositionReportDto.ClientReportId"/> is accepted without creating
    /// a duplicate row (production-readiness item #5).
    /// </summary>
    [HttpPost("{id:int}/position")]
    public async Task<IActionResult> Position(int id, [FromBody] PositionReportDto report)
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
