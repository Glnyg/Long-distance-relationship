namespace AIAPP.AiPrivacy;

public interface IAiConsentStore
{
    Task<CoupleBinding?> GetActiveBindingAsync(Guid coupleId, CancellationToken cancellationToken);

    Task<AiConsentGrant?> GetConsentAsync(Guid coupleId, Guid userId, CancellationToken cancellationToken);
}

public interface IAiPrivateDataSource
{
    Task<IReadOnlyList<PrivateChatMessage>> GetChatMessagesAsync(
        Guid coupleId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PrivateLocationPoint>> GetLocationPointsAsync(
        Guid coupleId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PrivateDeviceState>> GetDeviceStatesAsync(
        Guid coupleId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken);
}

public interface IAiAnalysisResultStore
{
    Task SaveAsync(AiAnalysisResult result, CancellationToken cancellationToken);

    Task<int> DeleteExpiredAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken);
}

public interface IAiAuditSink
{
    Task RecordAsync(AiAuditEntry entry, CancellationToken cancellationToken);
}
