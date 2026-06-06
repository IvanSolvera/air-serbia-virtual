using AirSerbiaVirtua.Api.Data;
using AirSerbiaVirtua.Api.Models;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AirSerbiaVirtua.Api.Controllers;

/// <summary>
/// Public VA statistics for the website landing page (phase W1).
/// Anonymous by design — it powers the pre-login hero strip.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/stats")]
[AllowAnonymous]
public class StatsController : ControllerBase
{
    private readonly AppDbContext _db;
    public StatsController(AppDbContext db) => _db = db;

    public record VaStatsDto(int Pilots, int FlightsFlown, decimal HoursLogged, int Routes);

    [HttpGet]
    public async Task<ActionResult<VaStatsDto>> Get()
    {
        var pilots = await _db.Pilots.CountAsync(p => p.Status != PilotStatus.Banned);
        var flights = await _db.Pireps.CountAsync(p => p.Status == PirepStatus.Accepted);
        var hours = await _db.Pilots.SumAsync(p => p.TotalHours);
        var routes = await _db.Routes.CountAsync(r => !r.IsOutstation);

        return Ok(new VaStatsDto(pilots, flights, hours, routes));
    }
}
