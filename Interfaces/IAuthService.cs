namespace POS_MT.Interfaces;

public interface IAuthService
{
    Task<AuthResult> SignInAsync(string userName, string password, CancellationToken cancellationToken = default);
}

public sealed class AuthResult
{
    public bool Succeeded { get; init; }

    public string? ErrorMessage { get; init; }

    public SignedInUser? User { get; init; }

    public static AuthResult Failed(string message) => new()
    {
        Succeeded = false,
        ErrorMessage = message
    };

    public static AuthResult Success(SignedInUser user) => new()
    {
        Succeeded = true,
        User = user
    };
}

public sealed class SignedInUser
{
    public required string UserId { get; init; }

    public required string UserName { get; init; }

    public required string DisplayName { get; init; }

    public required string Role { get; init; }
}
