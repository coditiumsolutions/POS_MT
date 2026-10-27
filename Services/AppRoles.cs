namespace POS_MT.Services;

/// <summary>
/// Role names as stored in Users.RoleName and ClaimTypes.Role.
/// </summary>
public static class AppRoles
{
    public const string Administrator = "Administrator";
    public const string Manager = "Manager";
    public const string Cashier = "Cashier";
    public const string Viewer = "Viewer";

    /// <summary>Legacy alias still seen in older rows / UI.</summary>
    public const string Admin = Administrator;

    public static readonly IReadOnlyList<string> All =
    [
        Administrator,
        Manager,
        Cashier,
        Viewer
    ];

    public static string Normalize(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return Cashier;
        }

        var trimmed = role.Trim();
        if (trimmed.Equals("Admin", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals(Administrator, StringComparison.OrdinalIgnoreCase))
        {
            return Administrator;
        }

        foreach (var known in All)
        {
            if (known.Equals(trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return known;
            }
        }

        return Cashier;
    }

    public static bool IsKnown(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return false;
        }

        var trimmed = role.Trim();
        if (trimmed.Equals("Admin", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return All.Any(x => x.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsPasswordStrongEnough(string? password, out string error)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
        {
            error = "Password must be at least 6 characters.";
            return false;
        }

        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
        {
            error = "Password must include at least one letter and one number.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
