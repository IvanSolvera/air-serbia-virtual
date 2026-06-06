using AirSerbiaVirtua.Api.Auth;
using AirSerbiaVirtua.Api.Data;
using AirSerbiaVirtua.Api.Models;
using AirSerbiaVirtua.Contracts;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AirSerbiaVirtua.Api.Controllers;

/// <summary>
/// Roster administration (Phase 5). Every endpoint requires the Admin role,
/// which is granted via <see cref="Pilot.IsAdmin"/> and carried as a JWT role claim.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<AdminController> _logger;

    public AdminController(AppDbContext db, ILogger<AdminController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>Full roster, newest first — Pending pilots surface for approval.</summary>
    [HttpGet("pilots")]
    public async Task<ActionResult<List<AdminPilot>>> Pilots()
    {
        var pilots = await _db.Pilots
            .Include(p => p.Rank)
            .OrderBy(p => p.Status == PilotStatus.Pending ? 0 : 1)
            .ThenByDescending(p => p.DateJoined)
            .Select(p => new AdminPilot(
                p.Id, p.Callsign, p.Name, p.Email,
                p.Rank != null ? p.Rank.Name : string.Empty, p.TotalHours,
                p.Status, p.HubId, p.DateJoined, p.IsAdmin, p.VatsimId))
            .ToListAsync();

        return Ok(pilots);
    }

    /// <summary>Promotes a Pending/Inactive pilot to Active (roster approval).</summary>
    [HttpPost("pilots/{id:int}/activate")]
    public async Task<IActionResult> Activate(int id)
    {
        var pilot = await _db.Pilots.FindAsync(id);
        if (pilot is null) return NotFound(new { message = "Pilot not found." });
        if (pilot.Status == PilotStatus.Active)
            return Ok(new { message = "Pilot is already active." });
        if (pilot.Status == PilotStatus.Banned)
            return Conflict(new { message = "Banned pilots cannot be activated from here." });

        pilot.Status = PilotStatus.Active;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Pilot {TargetPilotId} ({Callsign}) activated by admin {PilotId}",
            pilot.Id, pilot.Callsign, User.PilotId());
        return Ok(new { message = $"{pilot.Callsign} is now active." });
    }

    /// <summary>Deactivates an Active pilot (keeps the roster entry and history).</summary>
    [HttpPost("pilots/{id:int}/deactivate")]
    public async Task<IActionResult> Deactivate(int id)
    {
        var pilot = await _db.Pilots.FindAsync(id);
        if (pilot is null) return NotFound(new { message = "Pilot not found." });

        if (pilot.Id == User.PilotId())
            return Conflict(new { message = "You cannot deactivate your own account." });
        if (pilot.IsAdmin)
            return Conflict(new { message = "Administrators cannot be deactivated from here." });
        if (pilot.Status == PilotStatus.Inactive)
            return Ok(new { message = "Pilot is already inactive." });

        pilot.Status = PilotStatus.Inactive;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Pilot {TargetPilotId} ({Callsign}) deactivated by admin {PilotId}",
            pilot.Id, pilot.Callsign, User.PilotId());
        return Ok(new { message = $"{pilot.Callsign} is now inactive." });
    }
}
