using System.Globalization;
using System.Security.Cryptography;

namespace AIAPP.IdentityService.Application.Abstractions;

public interface ISmsCodeGenerator
{
    string GenerateCode();
}

public interface ISmsSender
{
    Task SendAsync(string normalizedPhoneNumber, string code, CancellationToken cancellationToken);
}

public interface IWechatLoginVerifier
{
    Task<WechatLoginVerificationResult> VerifyAsync(string loginCode, CancellationToken cancellationToken);
}

public sealed record WechatLoginVerificationResult(bool Succeeded, string? OpenId, string? UnionId)
{
    public static WechatLoginVerificationResult Success(string openId, string? unionId) => new(true, openId, unionId);

    public static WechatLoginVerificationResult Failure() => new(false, null, null);
}

public sealed class RandomSmsCodeGenerator : ISmsCodeGenerator
{
    public string GenerateCode() => RandomNumberGenerator.GetInt32(100000, 999999).ToString(CultureInfo.InvariantCulture);
}

public sealed class NoopSmsSender : ISmsSender
{
    public Task SendAsync(string normalizedPhoneNumber, string code, CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class DisabledWechatLoginVerifier : IWechatLoginVerifier
{
    public Task<WechatLoginVerificationResult> VerifyAsync(string loginCode, CancellationToken cancellationToken)
    {
        return Task.FromResult(WechatLoginVerificationResult.Failure());
    }
}
