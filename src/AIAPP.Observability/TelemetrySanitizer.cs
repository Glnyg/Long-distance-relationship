using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace AIAPP.Observability;

public sealed class TelemetrySanitizer
{
    private static readonly Regex EmailPattern = new(
        @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex LongNumberPattern = new(
        @"(?<!\d)\d{7,}(?!\d)",
        RegexOptions.Compiled);

    private static readonly Regex SecretAssignmentPattern = new(
        @"(?i)\b(access_token|refresh_token|token|authorization|password|verificationcode|验证码)\s*[:=]\s*[^,\s;]+",
        RegexOptions.Compiled);

    private static readonly string[] SensitiveKeyParts =
    [
        "chat",
        "message",
        "text",
        "latitude",
        "longitude",
        "wifi",
        "wi-fi",
        "token",
        "authorization",
        "password",
        "verification",
        "code",
        "prompt",
        "model_input"
    ];

    public string SanitizeText(string? value, int maxLength = 256)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var sanitized = EmailPattern.Replace(value, "[email]");
        sanitized = LongNumberPattern.Replace(sanitized, "[number]");
        sanitized = SecretAssignmentPattern.Replace(sanitized, "$1=[redacted]");

        if (sanitized.Length <= maxLength)
        {
            return sanitized;
        }

        return sanitized[..maxLength];
    }

    public IReadOnlyDictionary<string, object?> SanitizeProperties(IReadOnlyDictionary<string, object?> properties)
    {
        var sanitized = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in properties)
        {
            sanitized[key] = SanitizeProperty(key, value);
        }

        return sanitized;
    }

    public object? SanitizeProperty(string key, object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (IsSensitiveKey(key))
        {
            return "[redacted]";
        }

        return value switch
        {
            Guid guid when IsIdentifierKey(key) => HashIdentifier(guid),
            string text when IsIdentifierKey(key) => HashIdentifier(text),
            string text => SanitizeText(text),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value
        };
    }

    public static string HashIdentifier(Guid value)
    {
        return HashIdentifier(value.ToString("N"));
    }

    public static string HashIdentifier(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }

    private static bool IsIdentifierKey(string key)
    {
        return key.EndsWith("_id", StringComparison.OrdinalIgnoreCase)
            || key.EndsWith("Id", StringComparison.Ordinal)
            || key.Contains("user_id", StringComparison.OrdinalIgnoreCase)
            || key.Contains("couple_id", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSensitiveKey(string key)
    {
        foreach (var part in SensitiveKeyParts)
        {
            if (key.Contains(part, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
