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
[Route("api/v{version:apiVersion}/aircraft")]
[Authorize]
public class AircraftController : ControllerBase
{
    private readonly AppDbContext _db;
    public AircraftController(AppDbContext db) => _db = db;

    /// <summary>
    /// Lists airframes, optionally filtered by ICAO type code and operational
    /// status. Used by the desktop client to pick a compatible aircraft for a
    /// route, and anonymously by the public website fleet page (phase W2).
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<List<AircraftInfo>>> List(
        [FromQuery] string? type = null,
        [FromQuery] string? status = null)
    {
        var query = _db.Aircraft.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(type))
        {
            var t = type.Trim();
            query = query.Where(a => a.Type == t);
        }

        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<AircraftStatus>(status, ignoreCase: true, out var parsed))
        {
            query = query.Where(a => a.Status == parsed);
        }

        var list = await query
            .OrderBy(a => a.Registration)
            .Select(a => new AircraftInfo(a.Id, a.Type, a.Registration, a.Status.ToString(), a.HubId))
            .ToListAsync();

        return Ok(list);
    }
}
