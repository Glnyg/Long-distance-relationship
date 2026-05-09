using AIAPP.Observability;

namespace AIAPP.Observability.Tests;

public sealed class TelemetrySanitizerTests
{
    [Fact]
    public void SanitizeText_removes_common_private_values()
    {
        var sanitizer = new TelemetrySanitizer();

        var sanitized = sanitizer.SanitizeText("phone 13800138000 email a@example.com token=secret-value");

        Assert.DoesNotContain("13800138000", sanitized);
        Assert.DoesNotContain("a@example.com", sanitized);
        Assert.DoesNotContain("secret-value", sanitized);
        Assert.Contains("[number]", sanitized);
        Assert.Contains("[email]", sanitized);
        Assert.Contains("token=[redacted]", sanitized);
    }

    [Fact]
    public void SanitizeProperties_redacts_sensitive_keys_and_hashes_identifiers()
    {
        var userId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var sanitizer = new TelemetrySanitizer();

        var sanitized = sanitizer.SanitizeProperties(new Dictionary<string, object?>
        {
            ["user_id"] = userId,
            ["prompt"] = "private prompt",
            ["wifi_name"] = "HomeWiFi",
            ["operation"] = "ai_privacy.analyze"
        });

        Assert.NotEqual(userId, sanitized["user_id"]);
        Assert.Equal(TelemetrySanitizer.HashIdentifier(userId), sanitized["user_id"]);
        Assert.Equal("[redacted]", sanitized["prompt"]);
        Assert.Equal("[redacted]", sanitized["wifi_name"]);
        Assert.Equal("ai_privacy.analyze", sanitized["operation"]);
    }
}
