namespace AirSerbiaVirtua.Api.Auth;

/// <summary>
/// Abstraction over the password hashing algorithm so the rest of the code never
/// touches a specific library. Swap implementations without touching callers.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Returns a salted hash suitable for storing in the database.</summary>
    string Hash(string plain);

    /// <summary>Constant-time verification of <paramref name="plain"/> against a stored hash.</summary>
    bool Verify(string plain, string hash);
}
