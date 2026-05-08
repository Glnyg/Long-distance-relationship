namespace AIAPP.AiPrivacy;

[Flags]
public enum AiPrivacyDataTypes
{
    None = 0,
    Chat = 1,
    Location = 2,
    DeviceState = 4
}

public enum AiAnalysisFailureCode
{
    None = 0,
    BindingInactive = 1,
    RequesterNotInCouple = 2,
    ConsentMissing = 3,
    InvalidTimeRange = 4,
    ModelUnavailable = 5,
    InvalidModelOutput = 6,
    UnsafeModelOutput = 7
}

public sealed record AiAnalysisRequest(
    Guid CoupleId,
    Guid RequestedByUserId,
    AiPrivacyDataTypes DataTypes,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null);

public sealed record CoupleBinding(
    Guid CoupleId,
    Guid PartnerAUserId,
    Guid PartnerBUserId,
    bool IsActive)
{
    public bool Contains(Guid userId)
    {
        return PartnerAUserId == userId || PartnerBUserId == userId;
    }

    public IReadOnlyList<Guid> PartnerIds => [PartnerAUserId, PartnerBUserId];
}

public sealed record AiConsentGrant(
    Guid UserId,
    bool IsAiAnalysisEnabled,
    AiPrivacyDataTypes GrantedDataTypes,
    string ConsentVersion);

public sealed record PrivateChatMessage(
    Guid UserId,
    DateTimeOffset SentAtUtc,
    string Text);

public sealed record PrivateLocationPoint(
    Guid UserId,
    DateTimeOffset RecordedAtUtc,
    double Latitude,
    double Longitude,
    string? AreaLabel);

public sealed record PrivateDeviceState(
    Guid UserId,
    DateTimeOffset RecordedAtUtc,
    int ScreenOnMinutes,
    int BatteryPercent,
    bool IsCharging,
    string? ActiveNetworkName);

public sealed record AiAnalysisResult(
    Guid ResultId,
    Guid CoupleId,
    Guid RequestedByUserId,
    IReadOnlyList<Guid> VisibleToUserIds,
    string Summary,
    IReadOnlyList<string> Suggestions,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    string ModelId);

public sealed record AiAnalysisOutcome(
    bool Succeeded,
    AiAnalysisResult? Result,
    AiAnalysisFailureCode FailureCode,
    string? FailureMessage)
{
    public static AiAnalysisOutcome Success(AiAnalysisResult result)
    {
        return new AiAnalysisOutcome(true, result, AiAnalysisFailureCode.None, null);
    }

    public static AiAnalysisOutcome Failure(AiAnalysisFailureCode failureCode, string failureMessage)
    {
        return new AiAnalysisOutcome(false, null, failureCode, failureMessage);
    }
}

public sealed record AiAuditEntry(
    Guid AuditId,
    Guid CoupleId,
    Guid RequestedByUserId,
    Guid PartnerAUserId,
    Guid PartnerBUserId,
    AiPrivacyDataTypes DataTypes,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    string PartnerAConsentVersion,
    string PartnerBConsentVersion,
    string ModelId,
    long? InputTokenCount,
    long? OutputTokenCount,
    long? TotalTokenCount,
    Guid? ResultId,
    DateTimeOffset CreatedAtUtc,
    bool Succeeded,
    AiAnalysisFailureCode FailureCode);
