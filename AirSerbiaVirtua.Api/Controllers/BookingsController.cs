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
[Route("api/v{version:apiVersion}/bookings")]
[Authorize]
public class BookingsController : ControllerBase
{
    private readonly AppDbContext _db;
    public BookingsController(AppDbContext db) => _db = db;

    /// <summary>Returns the calling pilot's bookings, newest scheduled date first.</summary>
    [HttpGet("mine")]
    public async Task<ActionResult<List<BookingInfo>>> Mine()
    {
        var pilotId = User.PilotId();
        if (pilotId is null) return Unauthorized();

        var bookings = await _db.Bookings
            .AsNoTracking()
            .Where(b => b.PilotId == pilotId)
            .Include(b => b.Route)
            .OrderByDescending(b => b.Date)
            .ThenBy(b => b.Id)
            .Select(b => new BookingInfo(
                b.Id, b.RouteId,
                b.Route!.FlightNumber, b.Route.DepIcao, b.Route.ArrIcao,
                b.Route.AircraftType, b.Route.PlannedTime,
                b.Date, b.Status, b.DispatchReadyAtUtc))
            .ToListAsync();

        return Ok(bookings);
    }

    /// <summary>Creates an open booking for the calling pilot. Idempotent per (pilot, route, date).</summary>
    [HttpPost]
    public async Task<ActionResult<BookingInfo>> Create([FromBody] CreateBookingRequest req)
    {
        var pilotId = User.PilotId();
        if (pilotId is null) return Unauthorized();

        var route = await _db.Routes.AsNoTracking().FirstOrDefaultAsync(r => r.Id == req.RouteId);
        if (route is null) return BadRequest(new { message = "Unknown route." });

        var existing = await _db.Bookings.FirstOrDefaultAsync(b =>
            b.PilotId == pilotId &&
            b.RouteId == req.RouteId &&
            b.Date == req.Date);

        if (existing is not null)
            return Conflict(new { message = "You already have a booking for this flight on this date." });

        var booking = new Booking
        {
            PilotId = pilotId.Value,
            RouteId = req.RouteId,
            Date = req.Date,
            Status = BookingStatus.Open
        };
        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync();

        booking.Route = route;
        return Ok(ToDto(booking));
    }

    /// <summary>Cancels a still-Open booking owned by the calling pilot.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Cancel(int id)
    {
        var pilotId = User.PilotId();
        if (pilotId is null) return Unauthorized();

        var booking = await _db.Bookings.FirstOrDefaultAsync(b => b.Id == id && b.PilotId == pilotId);
        if (booking is null) return NotFound();

        if (booking.Status is BookingStatus.Flown or BookingStatus.Cancelled)
            return Conflict(new { message = $"Booking is already {booking.Status}." });

        booking.Status = BookingStatus.Cancelled;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static BookingInfo ToDto(Booking b) => new(
        b.Id, b.RouteId,
        b.Route!.FlightNumber, b.Route.DepIcao, b.Route.ArrIcao,
        b.Route.AircraftType, b.Route.PlannedTime,
        b.Date, b.Status, b.DispatchReadyAtUtc);

    /// <summary>
    /// The calling pilot's Open/Confirmed bookings — dispatch-ready first
    /// (newest preparation wins), then by service date. The desktop client
    /// pulls this to offer "Resume dispatch".
    /// </summary>
    [HttpGet("active")]
    public async Task<ActionResult<List<BookingInfo>>> Active()
    {
        var pilotId = User.PilotId();
        if (pilotId is null) return Unauthorized();

        var bookings = await _db.Bookings
            .AsNoTracking()
            .Where(b => b.PilotId == pilotId &&
                        (b.Status == BookingStatus.Open || b.Status == BookingStatus.Confirmed))
            .Include(b => b.Route)
            .ToListAsync();

        var ordered = bookings
            .OrderByDescending(b => b.DispatchReadyAtUtc.HasValue)
            .ThenByDescending(b => b.DispatchReadyAtUtc)
            .ThenBy(b => b.Date)
            .ThenBy(b => b.Id)
            .Select(ToDto)
            .ToList();

        return Ok(ordered);
    }

    /// <summary>
    /// Marks an Open/Confirmed booking dispatch-ready (web Briefing page).
    /// Idempotent — repeating refreshes the timestamp.
    /// </summary>
    [HttpPost("{id:int}/dispatch")]
    public async Task<ActionResult<BookingInfo>> MarkDispatchReady(int id)
    {
        var pilotId = User.PilotId();
        if (pilotId is null) return Unauthorized();

        var booking = await _db.Bookings
            .Include(b => b.Route)
            .FirstOrDefaultAsync(b => b.Id == id && b.PilotId == pilotId);
        if (booking is null) return NotFound();

        if (booking.Status is not (BookingStatus.Open or BookingStatus.Confirmed))
            return Conflict(new { message = $"Booking is {booking.Status} — only Open or Confirmed bookings can be dispatched." });

        booking.DispatchReadyAtUtc = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(ToDto(booking));
    }

    /// <summary>Clears the dispatch-ready flag ("unprepare").</summary>
    [HttpDelete("{id:int}/dispatch")]
    public async Task<IActionResult> ClearDispatchReady(int id)
    {
        var pilotId = User.PilotId();
        if (pilotId is null) return Unauthorized();

        var booking = await _db.Bookings.FirstOrDefaultAsync(b => b.Id == id && b.PilotId == pilotId);
        if (booking is null) return NotFound();

        if (booking.Status is not (BookingStatus.Open or BookingStatus.Confirmed))
            return Conflict(new { message = $"Booking is {booking.Status}." });

        booking.DispatchReadyAtUtc = null;
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
