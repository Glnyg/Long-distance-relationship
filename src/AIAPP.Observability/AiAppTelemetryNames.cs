namespace AIAPP.Observability;

public static class AiAppTelemetryNames
{
    public const string CorrelationIdHeaderName = "X-Correlation-Id";
    public const string CorrelationIdItemName = "aiapp.correlation_id";

    public static class ActivitySources
    {
        public const string Api = "AIAPP.Api";
        public const string Identity = "AIAPP.Identity";
        public const string Couple = "AIAPP.Couple";
        public const string Consent = "AIAPP.Consent";
        public const string Location = "AIAPP.Location";
        public const string DeviceState = "AIAPP.DeviceState";
        public const string Messaging = "AIAPP.Messaging";
        public const string Rtc = "AIAPP.Rtc";
        public const string AiPrivacy = "AIAPP.AiPrivacy";
        public const string Notifications = "AIAPP.Notifications";
        public const string Audit = "AIAPP.Audit";

        public static readonly string[] All =
        [
            Api,
            Identity,
            Couple,
            Consent,
            Location,
            DeviceState,
            Messaging,
            Rtc,
            AiPrivacy,
            Notifications,
            Audit
        ];
    }

    public static class Meters
    {
        public const string Server = "AIAPP.Server";
        public const string AiPrivacy = "AIAPP.AiPrivacy";
        public const string Android = "AIAPP.Android";

        public static readonly string[] All =
        [
            Server,
            AiPrivacy,
            Android
        ];
    }

    public static class Metrics
    {
        public const string AiAnalysisStarted = "ai_privacy.analysis.started";
        public const string AiAnalysisCompleted = "ai_privacy.analysis.completed";
        public const string AiAnalysisFailed = "ai_privacy.analysis.failed";
        public const string AiModelCallDuration = "ai_privacy.model_call.duration";
        public const string AiExpiredResultsDeleted = "ai_privacy.expired_results.deleted";
    }

    public static class Tags
    {
        public const string ServiceName = "service_name";
        public const string Operation = "operation";
        public const string CorrelationId = "correlation_id";
        public const string Result = "result";
        public const string ErrorCode = "error_code";
        public const string DurationMs = "duration_ms";
        public const string UserIdHash = "user_id_hash";
        public const string CoupleIdHash = "couple_id_hash";
        public const string DataType = "data_type";
        public const string ConsentVersion = "consent_version";
        public const string ModelId = "model_id";
        public const string TimeRangeDays = "time_range_days";
    }
}
