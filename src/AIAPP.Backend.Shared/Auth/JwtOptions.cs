namespace AIAPP.Backend.Shared.Auth;

public sealed class JwtOptions
{
    public string Issuer { get; init; } = "AIAPP";

    public string Audience { get; init; } = "AIAPP.Android";

    public string SigningKey { get; init; } = string.Empty;

    public int AccessTokenMinutes { get; init; } = 30;

    public int RefreshTokenDays { get; init; } = 30;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SigningKey) || SigningKey.Length < 32)
        {
            throw new InvalidOperationException("Jwt:SigningKey must be at least 32 characters.");
        }

        if (AccessTokenMinutes <= 0)
        {
            throw new InvalidOperationException("Jwt:AccessTokenMinutes must be positive.");
        }

        if (RefreshTokenDays <= 0)
        {
            throw new InvalidOperationException("Jwt:RefreshTokenDays must be positive.");
        }
    }
}
