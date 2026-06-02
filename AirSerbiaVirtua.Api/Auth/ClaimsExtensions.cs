using System.Security.Claims;

namespace AirSerbiaVirtua.Api.Auth;

public static class ClaimsExtensions
{
    /// <summary>Returns the authenticated pilot's id from the JWT, or null.</summary>
    public static int? PilotId(this ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? user.FindFirstValue("sub");
        return int.TryParse(raw, out var id) ? id : null;
    }
}
