using AirSerbiaVirtua.Api.Data;
using AirSerbiaVirtua.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AirSerbiaVirtua.Api.Controllers;

[ApiController]
[Route("api/routes")]
[Authorize]
public class RoutesController : ControllerBase
{
    private readonly AppDbContext _db;
    public RoutesController(AppDbContext db) => _db = db;

    /// <summary>
    /// Lists schedulable routes, optionally filtered by hub ICAO (matches either
    /// departure or arrival airport so we surface both outbound and inbound legs).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<RouteDto>>> List([FromQuery] string? hub = null)
    {
        var query = _db.Routes.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(hub))
        {
            var h = hub.Trim().ToUpperInvariant();
            query = query.Where(r => r.DepIcao == h || r.ArrIcao == h);
        }

        var routes = await query
            .OrderBy(r => r.FlightNumber)
            .Select(r => new RouteDto(
                r.Id, r.FlightNumber, r.DepIcao, r.ArrIcao,
                r.AircraftType, r.Distance, r.PlannedTime, r.Days))
            .ToListAsync();
        return Ok(routes);
    }
}
