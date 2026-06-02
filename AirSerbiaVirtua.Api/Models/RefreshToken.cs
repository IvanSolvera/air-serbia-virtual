using System.ComponentModel.DataAnnotations;

namespace AirSerbiaVirtua.Api.Models;

/// <summary>
/// Server-side record of a refresh token. The raw token is never stored — only
/// its SHA-256 hex hash, so a database leak does not yield usable credentials.
/// Rotation: every successful refresh marks the presented token as revoked and
/// issues a new one whose hash is recorded in <see cref="ReplacedByTokenHash"/>.
/// </summary>
public class RefreshToken
{
    public long Id { get; set; }

    public int PilotId { get; set; }
    public Pilot? Pilot { get; set; }

    /// <summary>SHA-256 hex of the raw refresh token (64 chars).</summary>
    [MaxLength(64)]
    public string TokenHash { get; set; } = null!;

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }

    /// <summary>Set on logout or rotation. A non-null value means the token is dead.</summary>
    public DateTimeOffset? RevokedAtUtc { get; set; }

    /// <summary>Hash of the token issued in place of this one (chain marker).</summary>
    [MaxLength(64)]
    public string? ReplacedByTokenHash { get; set; }

    /// <summary>Remote IP that obtained this token (forensics).</summary>
    [MaxLength(45)]
    public string? CreatedFromIp { get; set; }

    public bool IsActive => RevokedAtUtc is null && ExpiresAtUtc > DateTimeOffset.UtcNow;
}
