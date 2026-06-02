using AirSerbiaVirtua.Api.Data;
using AirSerbiaVirtua.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AirSerbiaVirtua.Api.Controllers;

[ApiController]
[Route("api/sync")]
[Authorize]
public class SyncController : ControllerBase
{
    private readonly AppDbContext _db;
    public SyncController(AppDbContext db) => _db = db;

    /// <summary>
    /// Returns airports whose <c>Revision</c> is greater than <paramref name="since"/>,
    /// so the client can keep its local nav cache up to date with delta pulls.
    /// </summary>
    [HttpGet("airports")]
    public async Task<ActionResult<AirportSyncResponse>> Airports([FromQuery] int since = 0)
    {
        var changed = await _db.Airports
            .Where(a => a.Revision > since)
            .OrderBy(a => a.Revision)
            .Select(a => new AirportDto(a.Icao, a.Iata, a.Name, a.Country, a.Lat, a.Lon, a.Elevation, a.Revision))
            .ToListAsync();

        var latest = await _db.Airports.MaxAsync(a => (int?)a.Revision) ?? since;

        return Ok(new AirportSyncResponse(since, latest, changed.Count, changed));
    }
}
