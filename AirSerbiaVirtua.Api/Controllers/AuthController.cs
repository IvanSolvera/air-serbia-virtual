using AirSerbiaVirtua.Api.Auth;
using AirSerbiaVirtua.Api.Data;
using AirSerbiaVirtua.Api.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AirSerbiaVirtua.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly JwtTokenService _tokens;
    private readonly IConfiguration _config;

    public AuthController(AppDbContext db, JwtTokenService tokens, IConfiguration config)
    {
        _db = db;
        _tokens = tokens;
        _config = config;
    }

    /// <summary>Authenticates a pilot by callsign and returns a JWT plus the pilot profile.</summary>
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest req)
    {
        var pilot = await _db.Pilots
            .Include(p => p.Rank)
            .FirstOrDefaultAsync(p => p.Callsign == req.Callsign);

        if (pilot is null)
            return Unauthorized(new { message = "Unknown callsign." });

        // PoC credential check. Replace with a hashed-password store before production.
        var devPassword = _config["Auth:DevPassword"];
        if (!string.IsNullOrEmpty(devPassword) && req.Password != devPassword)
            return Unauthorized(new { message = "Invalid credentials." });

        if (pilot.Status == Models.PilotStatus.Banned || pilot.Status == Models.PilotStatus.Inactive)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Account is not active." });

        var (token, expires) = _tokens.CreateToken(pilot);

        var profile = new PilotProfileDto(
            pilot.Id, pilot.Callsign, pilot.Name, pilot.Email,
            pilot.RankId, pilot.Rank?.Name ?? string.Empty, pilot.TotalHours,
            pilot.Status, pilot.HubId, pilot.DateJoined);

        return Ok(new LoginResponse(token, expires, profile));
    }
}
