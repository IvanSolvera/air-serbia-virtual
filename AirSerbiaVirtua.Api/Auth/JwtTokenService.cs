using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AirSerbiaVirtua.Api.Models;
using Microsoft.IdentityModel.Tokens;

namespace AirSerbiaVirtua.Api.Auth;

/// <summary>Issues signed JWTs for authenticated pilots.</summary>
public class JwtTokenService
{
    private readonly IConfiguration _config;
    public JwtTokenService(IConfiguration config) => _config = config;

    public (string token, DateTimeOffset expires) CreateAccessToken(Pilot pilot)
    {
        var jwt = _config.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var ttlMinutes = int.Parse(jwt["AccessExpiresMinutes"] ?? jwt["ExpiresMinutes"] ?? "60");
        var expires = DateTimeOffset.UtcNow.AddMinutes(ttlMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, pilot.Id.ToString()),
            new(ClaimTypes.NameIdentifier, pilot.Id.ToString()),
            new("callsign", pilot.Callsign),
            new("rankId", pilot.RankId.ToString()),
            new(JwtRegisteredClaimNames.Email, pilot.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (pilot.IsAdmin)
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));

        var token = new JwtSecurityToken(
            issuer: jwt["Issuer"],
            audience: jwt["Audience"],
            claims: claims,
            expires: expires.UtcDateTime,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
