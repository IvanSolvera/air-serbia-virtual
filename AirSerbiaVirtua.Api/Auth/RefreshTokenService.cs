using System.Security.Cryptography;
using System.Text;
using AirSerbiaVirtua.Api.Data;
using AirSerbiaVirtua.Api.Models;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace AirSerbiaVirtua.Api.Auth;

/// <summary>
/// Issues and rotates opaque refresh tokens with theft-detection semantics:
/// presenting an already-revoked refresh token revokes every other active
/// refresh token for the same pilot, forcing a full re-login.
/// </summary>
public class RefreshTokenService
{
    private readonly AppDbContext _db;
    private readonly int _expirationDays;

    public RefreshTokenService(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _expirationDays = int.Parse(config["Jwt:RefreshExpiresDays"] ?? "30");
    }

    public async Task<(string rawToken, DateTimeOffset expiresAtUtc)> IssueAsync(
        int pilotId, string? ip, CancellationToken ct = default)
    {
        var raw = GenerateRawToken();
        var now = DateTimeOffset.UtcNow;
        var entity = new RefreshToken
        {
            PilotId = pilotId,
            TokenHash = HashToken(raw),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(_expirationDays),
            CreatedFromIp = ip
        };
        _db.RefreshTokens.Add(entity);
        await _db.SaveChangesAsync(ct);
        return (raw, entity.ExpiresAtUtc);
    }

    public async Task<RotationResult> RotateAsync(string rawToken, string? ip, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
            return new RotationResult.NotFound();

        var hash = HashToken(rawToken);
        var existing = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (existing is null) return new RotationResult.NotFound();

        var now = DateTimeOffset.UtcNow;

        // Theft detection — a revoked token presented again means it was stolen
        // (or the chain forked). Burn every active token for this pilot.
        if (existing.RevokedAtUtc is not null)
        {
            await _db.RefreshTokens
                .Where(t => t.PilotId == existing.PilotId && t.RevokedAtUtc == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now), ct);
            return new RotationResult.Reused();
        }

        if (existing.ExpiresAtUtc <= now)
            return new RotationResult.Expired();

        existing.RevokedAtUtc = now;
        var (newRaw, newExpires) = await IssueAsync(existing.PilotId, ip, ct);
        existing.ReplacedByTokenHash = HashToken(newRaw);
        await _db.SaveChangesAsync(ct);

        return new RotationResult.Success(existing.PilotId, newRaw, newExpires);
    }

    public async Task RevokeAllForPilotAsync(int pilotId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        await _db.RefreshTokens
            .Where(t => t.PilotId == pilotId && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now), ct);
    }

    private static string GenerateRawToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return WebEncoders.Base64UrlEncode(bytes);
    }

    public static string HashToken(string raw)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(raw), hash);
        return Convert.ToHexString(hash);
    }
}

public abstract record RotationResult
{
    public sealed record Success(int PilotId, string RawToken, DateTimeOffset ExpiresAtUtc) : RotationResult;
    public sealed record NotFound : RotationResult;
    public sealed record Expired : RotationResult;
    public sealed record Reused : RotationResult;
}
