namespace AIAPP.AiPrivacy;

public sealed class AiPrivacyGatewayOptions
{
    public string ModelId { get; init; } = "domestic-model";

    public int MaxOutputTokens { get; init; } = 1024;

    public float Temperature { get; init; } = 0f;

    public TimeSpan DefaultLookback { get; init; } = TimeSpan.FromDays(7);

    public TimeSpan MaxLookback { get; init; } = TimeSpan.FromDays(30);

    public TimeSpan ResultRetention { get; init; } = TimeSpan.FromDays(30);

    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(30);

    public int MaxRetries { get; init; } = 2;
}
