using AirSerbiaVirtua.Api.Auth;
using AirSerbiaVirtua.Api.Data;
using AirSerbiaVirtua.Api.Dtos;
using AirSerbiaVirtua.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AirSerbiaVirtua.Api.Controllers;

[ApiController]
[Route("api/pireps")]
[Authorize]
public class PirepsController : ControllerBase
{
    private readonly AppDbContext _db;
    public PirepsController(AppDbContext db) => _db = db;

    /// <summary>
    /// Finalizes a flight session: stores the actuals, scores the flight, credits
    /// the pilot's hours, re-evaluates rank, unlocks the aircraft and marks the
    /// booking flown.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<PirepResultDto>> Submit([FromBody] PirepSubmitRequest req)
    {
        var pilotId = User.PilotId();
        if (pilotId is null) return Unauthorized();

        var pirep = await _db.Pireps.FirstOrDefaultAsync(p => p.Id == req.FlightSessionId && p.PilotId == pilotId);
        if (pirep is null) return NotFound(new { message = "Flight session not found." });
        if (pirep.Status != PirepStatus.Pending)
            return Conflict(new { message = "This PIREP has already been submitted." });

        // Record actuals.
        pirep.DepActual = req.DepActual;
        pirep.ArrActual = req.ArrActual;
        pirep.BlockMin = req.BlockMin;
        pirep.AirMin = req.AirMin;
        pirep.FuelUsedKg = req.FuelUsedKg;
        pirep.LandingRateFpm = req.LandingRateFpm;
        pirep.Source = string.IsNullOrWhiteSpace(req.Source) ? "ACARS" : req.Source;
        pirep.RawJson = req.RawJson;
        pirep.Score = ScoreFlight(req.LandingRateFpm, req.BlockMin);
        pirep.Status = PirepStatus.Accepted;

        // Credit hours and re-evaluate rank.
        var pilot = await _db.Pilots.FirstAsync(p => p.Id == pilotId);
        var oldRankId = pilot.RankId;
        pilot.TotalHours += Math.Round(req.BlockMin / 60m, 1);

        var ranks = await _db.Ranks.OrderByDescending(r => r.MinHours).ToListAsync();
        var earnedRank = ranks.FirstOrDefault(r => pilot.TotalHours >= r.MinHours) ?? ranks.Last();
        pilot.RankId = earnedRank.Id;
        var promoted = earnedRank.Id != oldRankId;

        // Unlock the aircraft.
        var aircraft = await _db.Aircraft.FindAsync(pirep.AircraftId);
        if (aircraft is not null && aircraft.Status == AircraftStatus.InFlight)
            aircraft.Status = AircraftStatus.Active;

        // Close the booking.
        var booking = await _db.Bookings.FirstOrDefaultAsync(b =>
            b.PilotId == pilotId && b.RouteId == pirep.RouteId && b.Status == BookingStatus.Confirmed);
        if (booking is not null) booking.Status = BookingStatus.Flown;

        await _db.SaveChangesAsync();

        return Ok(new PirepResultDto(
            pirep.Id, pirep.Status, pirep.Score, pirep.LandingRateFpm,
            pirep.BlockMin, pilot.TotalHours, earnedRank.Id, earnedRank.Name, promoted));
    }

    /// <summary>
    /// Baseline landing-quality score (0-100). Replace/extend with a full scoring
    /// engine that also weighs route adherence, fuel and time accuracy.
    /// </summary>
    private static int ScoreFlight(int landingRateFpm, int blockMin)
    {
        int abs = Math.Abs(landingRateFpm);
        int score = abs switch
        {
            <= 100 => 100,
            <= 200 => 95,
            <= 300 => 85,
            <= 500 => 70,
            <= 700 => 50,
            _ => 25
        };
        if (blockMin <= 0) score -= 20; // implausible block time
        return Math.Clamp(score, 0, 100);
    }
}
