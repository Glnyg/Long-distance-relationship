using AIAPP.Observability;

namespace AIAPP.Backend.Shared.Privacy;

public static class PrivacySafeProperties
{
    public static IReadOnlyDictionary<string, object?> ForUser(Guid userId)
    {
        return new Dictionary<string, object?>
        {
            [AiAppTelemetryNames.Tags.UserIdHash] = TelemetrySanitizer.HashIdentifier(userId)
        };
    }

    public static IReadOnlyDictionary<string, object?> ForCouple(Guid coupleId)
    {
        return new Dictionary<string, object?>
        {
            [AiAppTelemetryNames.Tags.CoupleIdHash] = TelemetrySanitizer.HashIdentifier(coupleId)
        };
    }
}
