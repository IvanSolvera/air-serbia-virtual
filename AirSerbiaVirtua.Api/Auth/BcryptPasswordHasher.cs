namespace AirSerbiaVirtua.Api.Auth;

/// <summary>BCrypt-backed implementation. Work factor 12 — ~250 ms on modern HW.</summary>
public sealed class BcryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    public string Hash(string plain) => BCrypt.Net.BCrypt.HashPassword(plain, WorkFactor);

    public bool Verify(string plain, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(plain, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }
}
