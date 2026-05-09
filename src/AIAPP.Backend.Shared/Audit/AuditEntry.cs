using AIAPP.Observability;

namespace AIAPP.Backend.Shared.Audit;

public sealed record AuditEntry(
    Guid AuditId,
    string Operation,
    string UserIdHash,
    string? CoupleIdHash,
    string Result,
    string ErrorCode,
    string? ConsentVersion,
    DateTimeOffset CreatedAtUtc)
{
    /// <summary>
    /// 审计只保存元数据和哈希 ID。误把手机号、验证码、token、聊天原文、精确位置或 prompt 放进审计，会变成长期隐私泄露。
    /// </summary>
    public static AuditEntry Create(
        string operation,
        Guid userId,
        Guid? coupleId,
        string result,
        string errorCode,
        string? consentVersion,
        DateTimeOffset createdAtUtc)
    {
        return new AuditEntry(
            Guid.NewGuid(),
            operation,
            TelemetrySanitizer.HashIdentifier(userId),
            coupleId.HasValue ? TelemetrySanitizer.HashIdentifier(coupleId.Value) : null,
            result,
            errorCode,
            consentVersion,
            createdAtUtc);
    }
}

public interface IAuditSink
{
    Task RecordAsync(AuditEntry entry, CancellationToken cancellationToken);
}
