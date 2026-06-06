using AirSerbiaVirtua.Api.Services;
using AirSerbiaVirtua.Contracts;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AirSerbiaVirtua.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/metar")]
[Authorize]
public class MetarController : ControllerBase
{
    private readonly MetarService _metar;
    public MetarController(MetarService metar) => _metar = metar;

    /// <summary>
    /// Returns the latest METAR(s) for one or more comma-separated ICAOs,
    /// e.g. <c>/api/metar?icaos=LYBE,LOWW</c>. Used by the Briefing (Phase 4).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<MetarInfo>>> Get([FromQuery] string icaos, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(icaos))
            return BadRequest(new { message = "Provide at least one ICAO via ?icaos=" });

        var ids = icaos.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return Ok(await _metar.GetAsync(ids, ct));
    }
}
