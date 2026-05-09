using System.Security.Cryptography;
using System.Text;

namespace AIAPP.IdentityService;

public sealed record SmsCodeRequest(string PhoneNumber);

public sealed record SmsLoginRequest(string PhoneNumber, string VerificationCode);

public sealed record WechatLoginRequest(string LoginCode);

public sealed record RefreshTokenRequest(string RefreshToken);

public sealed record AuthTokenResponse(string AccessToken, string RefreshToken, UserSummary User);

public sealed record UserSummary(Guid UserId);

public sealed record SmsCodeResponse(DateTimeOffset NextSendAllowedAtUtc)
{
    public static SmsCodeResponse Create(DateTimeOffset nextSendAllowedAtUtc) => new(nextSendAllowedAtUtc);
}

public static class PhoneNumber
{
    public static string Normalize(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());
        return digits.StartsWith("86", StringComparison.Ordinal) && digits.Length == 13 ? digits[2..] : digits;
    }

    public static string Hash(string normalizedPhone) => TokenHasher.Hash(Normalize(normalizedPhone));
}

public static class TokenHasher
{
    public static string Hash(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }
}

public sealed class UserAccount
{
    private readonly List<RefreshSession> _sessions = [];

    private UserAccount(Guid id, string phoneHash, string protectedPhone, DateTimeOffset createdAtUtc)
    {
        Id = id;
        PhoneHash = phoneHash;
        ProtectedPhone = protectedPhone;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; }

    public string PhoneHash { get; }

    public string ProtectedPhone { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public IReadOnlyList<RefreshSession> RefreshSessions => _sessions;

    public static UserAccount RegisterPhone(string normalizedPhone, string protectedPhone, DateTimeOffset now)
    {
        return new UserAccount(Guid.NewGuid(), PhoneNumber.Hash(normalizedPhone), protectedPhone, now);
    }

    public void IssueRefreshSession(string refreshToken, DateTimeOffset now, TimeSpan lifetime)
    {
        _sessions.Add(RefreshSession.Create(refreshToken, now, lifetime));
    }

    public bool RotateRefreshSession(string currentToken, string nextToken, DateTimeOffset now, TimeSpan lifetime)
    {
        var session = _sessions.FirstOrDefault(item => item.Matches(currentToken, now));
        if (session is null)
        {
            return false;
        }

        var replacement = RefreshSession.Create(nextToken, now, lifetime);
        session.Revoke(now, replacement.Id);
        _sessions.Add(replacement);
        return true;
    }

    public bool HasActiveRefreshToken(string token, DateTimeOffset now) => _sessions.Any(session => session.Matches(token, now));
}

public sealed class RefreshSession
{
    private RefreshSession(Guid id, string tokenHash, DateTimeOffset expiresAtUtc)
    {
        Id = id;
        TokenHash = tokenHash;
        ExpiresAtUtc = expiresAtUtc;
    }

    public Guid Id { get; }

    public string TokenHash { get; }

    public DateTimeOffset ExpiresAtUtc { get; }

    public DateTimeOffset? RevokedAtUtc { get; private set; }

    public Guid? ReplacedBySessionId { get; private set; }

    public static RefreshSession Create(string token, DateTimeOffset now, TimeSpan lifetime) => new(Guid.NewGuid(), TokenHasher.Hash(token), now.Add(lifetime));

    public bool Matches(string token, DateTimeOffset now) => RevokedAtUtc is null && ExpiresAtUtc >= now && TokenHash == TokenHasher.Hash(token);

    public void Revoke(DateTimeOffset now, Guid? replacedBySessionId)
    {
        RevokedAtUtc = now;
        ReplacedBySessionId = replacedBySessionId;
    }
}

public interface IPhoneProtector
{
    string Protect(string normalizedPhone);
}

public sealed class DevelopmentPhoneProtector : IPhoneProtector
{
    public string Protect(string normalizedPhone) => Convert.ToBase64String(Encoding.UTF8.GetBytes(normalizedPhone));
}
