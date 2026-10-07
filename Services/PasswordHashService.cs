using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using POS_MT.Interfaces;

namespace POS_MT.Services;

public sealed class PasswordHashService : IPasswordHashService
{
    private readonly PasswordHasher<string> _hasher = new();

    public string Hash(string password)
    {
        return _hasher.HashPassword(user: string.Empty, password);
    }

    public bool Verify(string passwordHash, string password)
    {
        if (string.IsNullOrWhiteSpace(passwordHash) || password is null)
        {
            return false;
        }

        if (IsAspNetIdentityHash(passwordHash))
        {
            var result = _hasher.VerifyHashedPassword(user: string.Empty, passwordHash, password);
            return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
        }

        // Existing POS_MT users store uppercase SHA-256 hex of the password.
        var legacyHash = ComputeSha256Hex(password);
        return string.Equals(legacyHash, passwordHash.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public bool NeedsRehash(string passwordHash)
    {
        return !IsAspNetIdentityHash(passwordHash);
    }

    private static bool IsAspNetIdentityHash(string passwordHash)
    {
        return passwordHash.StartsWith("AQAAAA", StringComparison.Ordinal);
    }

    private static string ComputeSha256Hex(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes);
    }
}
