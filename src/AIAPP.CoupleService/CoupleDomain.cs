using System.Security.Cryptography;
using AIAPP.CoupleService.Infrastructure.Persistence;

namespace AIAPP.CoupleService;

public static class CoupleStatus
{
    public const string Active = "active";

    public const string Inactive = "inactive";
}

public sealed record CreateInvitationResponse(string Code, string QrPayload, DateTimeOffset ExpiresAtUtc);

public sealed record CoupleBody(Guid CoupleId, Guid UserAId, Guid UserBId, string Status, DateTimeOffset BoundAtUtc, DateTimeOffset? UnboundAtUtc)
{
    public static CoupleBody FromRecord(CoupleRecord record)
    {
        return new CoupleBody(record.CoupleId, record.UserAId, record.UserBId, record.Status, record.BoundAtUtc, record.UnboundAtUtc);
    }
}

public sealed class CoupleInvitation
{
    private CoupleInvitation(Guid inviterUserId, string code, DateTimeOffset expiresAtUtc)
    {
        InviterUserId = inviterUserId;
        Code = code;
        ExpiresAtUtc = expiresAtUtc;
        QrPayload = $"aiapp://couples/invitations/{code}";
    }

    public Guid InviterUserId { get; }

    public string Code { get; }

    public string QrPayload { get; }

    public DateTimeOffset ExpiresAtUtc { get; }

    public bool IsConsumed { get; private set; }

    public static CoupleInvitation Create(Guid inviterUserId, DateTimeOffset now) => new(inviterUserId, GenerateCode(), now.AddMinutes(15));

    public CoupleBinding Accept(Guid accepterUserId, DateTimeOffset now)
    {
        if (IsConsumed || ExpiresAtUtc < now)
        {
            throw new InvalidOperationException("Invitation cannot be accepted.");
        }

        IsConsumed = true;
        return new CoupleBinding(Guid.NewGuid(), InviterUserId, accepterUserId, CoupleStatus.Active, now, null);
    }

    internal static string GenerateCode() => Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
}

public sealed record CoupleBinding(Guid CoupleId, Guid UserAId, Guid UserBId, string Status, DateTimeOffset BoundAtUtc, DateTimeOffset? UnboundAtUtc)
{
    public bool IsActive => Status == CoupleStatus.Active;

    public bool Contains(Guid userId) => UserAId == userId || UserBId == userId;
}

public sealed record CoupleAudit(string Operation, string UserIdHash, string? CoupleIdHash, string Result, string ErrorCode, DateTimeOffset CreatedAtUtc)
{
    public static CoupleAudit CreateInvitationGenerated(CoupleInvitation invitation, DateTimeOffset now)
    {
        return new CoupleAudit("couple.invitation.create", AIAPP.Observability.TelemetrySanitizer.HashIdentifier(invitation.InviterUserId), null, "ok", "none", now);
    }
}
