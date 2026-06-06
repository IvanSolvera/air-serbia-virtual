using AirSerbiaVirtua.Api.Auth;
using AirSerbiaVirtua.Api.Data;
using AirSerbiaVirtua.Api.Dtos;
using AirSerbiaVirtua.Api.Models;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace AirSerbiaVirtua.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private const int MinPasswordLength = 8;

    private readonly AppDbContext _db;
    private readonly JwtTokenService _jwt;
    private readonly RefreshTokenService _refresh;
    private readonly IPasswordHasher _hasher;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        AppDbContext db,
        JwtTokenService jwt,
        RefreshTokenService refresh,
        IPasswordHasher hasher,
        ILogger<AuthController> logger)
    {
        _db = db;
        _jwt = jwt;
        _refresh = refresh;
        _hasher = hasher;
        _logger = logger;
    }

    /// <summary>
    /// Self-service pilot registration. Creates a pilot in <see cref="PilotStatus.Pending"/>
    /// state — an admin must promote them to Active (via /admin/pilots/{id}/activate)
    /// before they can sign in.
    /// </summary>
    [HttpPost("register")]
    public async Task<ActionResult<PilotProfileDto>> Register([FromBody] RegisterRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Callsign) || string.IsNullOrWhiteSpace(req.Name) ||
            string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.HubIcao))
        {
            return BadRequest(new { message = "Callsign, name, email and hub are required." });
        }

        if (req.Password is null || req.Password.Length < MinPasswordLength)
            return BadRequest(new { message = $"Password must be at least {MinPasswordLength} characters." });

        // VATSIM CID is optional; when present it must look like one (6-8 digits).
        var vatsimId = string.IsNullOrWhiteSpace(req.VatsimId) ? null : req.VatsimId.Trim();
        if (vatsimId is not null && (vatsimId.Length is < 6 or > 8 || !vatsimId.All(char.IsDigit)))
            return BadRequest(new { message = "VATSIM ID must be 6-8 digits (or leave it empty)." });

        if (await _db.Pilots.AnyAsync(p => p.Callsign == req.Callsign))
            return Conflict(new { message = "Callsign already in use." });

        if (await _db.Pilots.AnyAsync(p => p.Email == req.Email))
            return Conflict(new { message = "Email already in use." });

        var hub = await _db.Airports.FindAsync(req.HubIcao);
        if (hub is null) return BadRequest(new { message = "Unknown hub ICAO." });

        var startingRank = await _db.Ranks.OrderBy(r => r.MinHours).FirstAsync();

        var now = DateTimeOffset.UtcNow;
        var pilot = new Pilot
        {
            Callsign = req.Callsign,
            Name = req.Name,
            Email = req.Email,
            PasswordHash = _hasher.Hash(req.Password),
            PasswordUpdatedAtUtc = now,
            HubId = req.HubIcao,
            RankId = startingRank.Id,
            Status = PilotStatus.Pending,
            DateJoined = now,
            VatsimId = vatsimId
        };

        _db.Pilots.Add(pilot);
        await _db.SaveChangesAsync();

        return Ok(ToProfile(pilot, startingRank.Name));
    }

    /// <summary>Authenticates a pilot and returns an access+refresh token pair.</summary>
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest req)
    {
        var pilot = await _db.Pilots
            .Include(p => p.Rank)
            .FirstOrDefaultAsync(p => p.Callsign == req.Callsign);

        if (pilot is null || !_hasher.Verify(req.Password, pilot.PasswordHash))
        {
            _logger.LogWarning("Login failed for callsign {Callsign} from {RemoteIp}", req.Callsign, RemoteIp());
            return Unauthorized(new { message = "Invalid credentials." });
        }

        if (pilot.Status == PilotStatus.Pending)
        {
            _logger.LogWarning("Login rejected for pilot {PilotId} ({Callsign}): awaiting approval",
                pilot.Id, pilot.Callsign);
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Account awaiting approval — an administrator must activate your roster entry." });
        }

        if (pilot.Status == PilotStatus.Banned || pilot.Status == PilotStatus.Inactive)
        {
            _logger.LogWarning("Login rejected for pilot {PilotId} ({Callsign}): status {Status}",
                pilot.Id, pilot.Callsign, pilot.Status);
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Account is not active." });
        }

        var (accessToken, accessExpires) = _jwt.CreateAccessToken(pilot);
        var (refreshRaw, refreshExpires) = await _refresh.IssueAsync(pilot.Id, RemoteIp());

        _logger.LogInformation("Pilot {PilotId} ({Callsign}) logged in from {RemoteIp}",
            pilot.Id, pilot.Callsign, RemoteIp());

        var tokens = new AuthTokensDto(accessToken, accessExpires, refreshRaw, refreshExpires);
        return Ok(new LoginResponse(tokens, ToProfile(pilot, pilot.Rank?.Name ?? string.Empty)));
    }

    /// <summary>
    /// Exchanges a still-valid refresh token for a fresh access+refresh pair.
    /// Re-use of a revoked refresh token wipes every active token for that pilot
    /// (theft detection) and returns 401.
    /// </summary>
    [HttpPost("refresh")]
    public async Task<ActionResult<RefreshResponse>> Refresh([FromBody] RefreshRequest req)
    {
        var result = await _refresh.RotateAsync(req.RefreshToken, RemoteIp());

        switch (result)
        {
            case RotationResult.Success s:
                var pilot = await _db.Pilots.FirstOrDefaultAsync(p => p.Id == s.PilotId);
                if (pilot is null) return Unauthorized();
                var (accessToken, accessExpires) = _jwt.CreateAccessToken(pilot);
                return Ok(new RefreshResponse(new AuthTokensDto(accessToken, accessExpires, s.RawToken, s.ExpiresAtUtc)));

            case RotationResult.Reused:
                return Unauthorized(new { message = "Refresh token reuse detected; all sessions revoked." });

            case RotationResult.Expired:
            case RotationResult.NotFound:
            default:
                return Unauthorized(new { message = "Invalid refresh token." });
        }
    }

    /// <summary>
    /// Returns the signed-in pilot's profile. Used by Auto Login, which restores a
    /// session from a persisted refresh token and has no cached profile to show.
    /// </summary>
    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<PilotProfileDto>> Me()
    {
        var pilotId = User.PilotId();
        if (pilotId is null) return Unauthorized();

        var pilot = await _db.Pilots
            .Include(p => p.Rank)
            .FirstOrDefaultAsync(p => p.Id == pilotId);
        if (pilot is null) return Unauthorized();

        return Ok(ToProfile(pilot, pilot.Rank?.Name ?? string.Empty));
    }

    /// <summary>Revokes the presented refresh token (logout from one device).</summary>
    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest req)
    {
        var hash = RefreshTokenService.HashToken(req.RefreshToken);
        var token = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash);
        if (token is { RevokedAtUtc: null }) token.RevokedAtUtc = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Changes the authenticated pilot's password after verifying the current one.</summary>
    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        var pilotId = User.PilotId();
        if (pilotId is null) return Unauthorized();

        if (req.NewPassword is null || req.NewPassword.Length < MinPasswordLength)
            return BadRequest(new { message = $"Password must be at least {MinPasswordLength} characters." });

        var pilot = await _db.Pilots.FirstOrDefaultAsync(p => p.Id == pilotId);
        if (pilot is null) return Unauthorized();

        if (!_hasher.Verify(req.CurrentPassword ?? string.Empty, pilot.PasswordHash))
            return Unauthorized(new { message = "Current password is incorrect." });

        pilot.PasswordHash = _hasher.Hash(req.NewPassword);
        pilot.PasswordUpdatedAtUtc = DateTimeOffset.UtcNow;

        // Force re-login everywhere by killing all refresh tokens for this pilot.
        await _refresh.RevokeAllForPilotAsync(pilot.Id);

        await _db.SaveChangesAsync();
        return NoContent();
    }

    private string? RemoteIp() => HttpContext.Connection.RemoteIpAddress?.ToString();

    private static PilotProfileDto ToProfile(Pilot p, string rankName) => new(
        p.Id, p.Callsign, p.Name, p.Email,
        p.RankId, rankName, p.TotalHours,
        p.Status, p.HubId, p.DateJoined,
        p.IsAdmin);
}
