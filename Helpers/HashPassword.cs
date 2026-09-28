using Isopoh.Cryptography.Argon2;
namespace WebTruyenTranh.Helpers;
public static class PasswordHasher
{
    public static string Hash(string password)
    {
        return Argon2.Hash(password);
    }

    public static bool Verify(string hash, string password)
    {
        return Argon2.Verify(hash, password);
    }
}
