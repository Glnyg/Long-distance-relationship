namespace AIAPP.ConsentService.Domain;

public sealed record ConsentDataType(string Value)
{
    public static readonly ConsentDataType LocationCurrent = new("location_current");
    public static readonly ConsentDataType LocationTrail = new("location_trail");
    public static readonly ConsentDataType DeviceState = new("device_state");
    public static readonly ConsentDataType ChatAi = new("chat_ai");
    public static readonly ConsentDataType LocationAi = new("location_ai");
    public static readonly ConsentDataType DeviceAi = new("device_ai");
    public static readonly ConsentDataType LockChallenge = new("lock_challenge");
    public static readonly ConsentDataType Notifications = new("notifications");

    public static IReadOnlyList<ConsentDataType> All { get; } =
    [
        LocationCurrent,
        LocationTrail,
        DeviceState,
        ChatAi,
        LocationAi,
        DeviceAi,
        LockChallenge,
        Notifications
    ];

    public static bool TryParse(string value, out ConsentDataType? dataType)
    {
        dataType = All.FirstOrDefault(item => string.Equals(item.Value, value, StringComparison.OrdinalIgnoreCase));
        return dataType is not null;
    }
}

public sealed class ConsentGrant
{
    private ConsentGrant(Guid coupleId, Guid userId, ConsentDataType dataType, DateTimeOffset now)
    {
        CoupleId = coupleId;
        UserId = userId;
        DataType = dataType;
        IsActive = true;
        ChangedAtUtc = now;
        ConsentVersion = NewVersion();
    }

    public Guid CoupleId { get; }

    public Guid UserId { get; }

    public ConsentDataType DataType { get; }

    public bool IsActive { get; private set; }

    public string ConsentVersion { get; private set; }

    public DateTimeOffset ChangedAtUtc { get; private set; }

    public static ConsentGrant Create(Guid coupleId, Guid userId, ConsentDataType dataType, DateTimeOffset now) => new(coupleId, userId, dataType, now);

    public void Revoke(DateTimeOffset now)
    {
        IsActive = false;
        ChangedAtUtc = now;
        ConsentVersion = NewVersion();
    }

    private static string NewVersion() => $"consent-{Guid.NewGuid():N}";
}

public sealed record ConsentAudit(string Operation, string DataType, string ConsentVersion, string Result, string ErrorCode)
{
    public static ConsentAudit FromGrant(string operation, ConsentGrant grant, string result, string errorCode, DateTimeOffset createdAtUtc)
    {
        return new ConsentAudit(operation, grant.DataType.Value, grant.ConsentVersion, result, errorCode);
    }
}
