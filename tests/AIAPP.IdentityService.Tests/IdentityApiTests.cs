using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AIAPP.IdentityService.Application.Abstractions;
using AIAPP.IdentityService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace AIAPP.IdentityService.Tests;

public sealed class IdentityApiTests : IAsyncLifetime
{
    private const string KnownCode = "246810";
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithImage("postgres:16-alpine")
        .WithDatabase("aiapp_identity_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private IdentityServiceFactory? _factory;

    public async Task InitializeAsync()
    {
        if (!DockerAvailability.IsDockerAvailable())
        {
            return;
        }

        await _postgres.StartAsync();
        _factory = new IdentityServiceFactory(_postgres.GetConnectionString());

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        _factory?.Dispose();
        if (DockerAvailability.IsDockerAvailable())
        {
            await _postgres.DisposeAsync();
        }
    }

    [DockerAvailableFact]
    public async Task SendSmsCode_DoesNotReturnVerificationCode()
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/auth/sms/send-code", new
        {
            phoneNumber = "+8613800138000"
        });

        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("nextSendAllowedAtUtc", body);
        Assert.DoesNotContain(KnownCode, body);
        Assert.DoesNotContain("13800138000", body);
    }

    [DockerAvailableFact]
    public async Task SmsLogin_ReturnsTokens_AndStoresOnlyHashedRefreshToken()
    {
        var client = CreateClient();
        const string phoneNumber = "+8613800138001";
        await SendCodeAsync(client, phoneNumber);

        var loginResponse = await client.PostAsJsonAsync("/auth/sms/login", new
        {
            phoneNumber,
            verificationCode = KnownCode
        });

        var loginJson = await ReadJsonAsync(loginResponse);
        var accessToken = loginJson.RootElement.GetProperty("accessToken").GetString();
        var refreshToken = loginJson.RootElement.GetProperty("refreshToken").GetString();

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(accessToken));
        Assert.False(string.IsNullOrWhiteSpace(refreshToken));

        await using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var storedRefreshToken = await db.RefreshTokenSessions.SingleAsync();

        Assert.NotEqual(refreshToken, storedRefreshToken.TokenHash);
        Assert.DoesNotContain(refreshToken!, storedRefreshToken.TokenHash);
        Assert.Null(storedRefreshToken.RevokedAtUtc);
    }

    [DockerAvailableFact]
    public async Task RefreshToken_RotatesStoredHash()
    {
        var client = CreateClient();
        var loginJson = await LoginBySmsAsync(client, "+8613800138002");
        var oldRefreshToken = loginJson.RootElement.GetProperty("refreshToken").GetString();

        var refreshResponse = await client.PostAsJsonAsync("/auth/token/refresh", new
        {
            refreshToken = oldRefreshToken
        });
        var refreshJson = await ReadJsonAsync(refreshResponse);
        var newRefreshToken = refreshJson.RootElement.GetProperty("refreshToken").GetString();

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        Assert.NotEqual(oldRefreshToken, newRefreshToken);

        await using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var sessions = await db.RefreshTokenSessions.OrderBy(session => session.CreatedAtUtc).ToListAsync();

        Assert.Equal(2, sessions.Count);
        Assert.NotNull(sessions[0].RevokedAtUtc);
        Assert.Equal(sessions[1].Id, sessions[0].ReplacedBySessionId);
        Assert.Null(sessions[1].RevokedAtUtc);
        Assert.DoesNotContain(oldRefreshToken!, sessions[0].TokenHash);
        Assert.DoesNotContain(newRefreshToken!, sessions[1].TokenHash);
    }

    [DockerAvailableFact]
    public async Task Me_RequiresAuthentication()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [DockerAvailableFact]
    public async Task AuditMetadataJson_DoesNotContainPhoneCodeOrTokens()
    {
        var client = CreateClient();
        const string phoneNumber = "+8613800138003";
        var loginJson = await LoginBySmsAsync(client, phoneNumber);
        var accessToken = loginJson.RootElement.GetProperty("accessToken").GetString();
        var firstRefreshToken = loginJson.RootElement.GetProperty("refreshToken").GetString();

        var refreshResponse = await client.PostAsJsonAsync("/auth/token/refresh", new
        {
            refreshToken = firstRefreshToken
        });
        var refreshJson = await ReadJsonAsync(refreshResponse);
        var secondRefreshToken = refreshJson.RootElement.GetProperty("refreshToken").GetString();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var logoutResponse = await client.PostAsJsonAsync("/auth/logout", new
        {
            refreshToken = secondRefreshToken
        });
        Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);

        await using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var auditJson = string.Join(Environment.NewLine, await db.AuditEntries.Select(entry => entry.MetadataJson).ToListAsync());

        Assert.DoesNotContain(phoneNumber, auditJson);
        Assert.DoesNotContain("13800138003", auditJson);
        Assert.DoesNotContain(KnownCode, auditJson);
        Assert.DoesNotContain(accessToken!, auditJson);
        Assert.DoesNotContain(firstRefreshToken!, auditJson);
        Assert.DoesNotContain(secondRefreshToken!, auditJson);
    }

    private HttpClient CreateClient()
    {
        return (_factory ?? throw new InvalidOperationException("测试工厂尚未初始化。")).CreateClient();
    }

    private AsyncServiceScope CreateScope()
    {
        return (_factory ?? throw new InvalidOperationException("测试工厂尚未初始化。")).Services.CreateAsyncScope();
    }

    private static async Task SendCodeAsync(HttpClient client, string phoneNumber)
    {
        var response = await client.PostAsJsonAsync("/auth/sms/send-code", new
        {
            phoneNumber
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<JsonDocument> LoginBySmsAsync(HttpClient client, string phoneNumber)
    {
        await SendCodeAsync(client, phoneNumber);
        var response = await client.PostAsJsonAsync("/auth/sms/login", new
        {
            phoneNumber,
            verificationCode = KnownCode
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadJsonAsync(response);
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }

    private sealed class IdentityServiceFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Identity"] = connectionString,
                    ["Jwt:Issuer"] = "AIAPP.Tests",
                    ["Jwt:Audience"] = "AIAPP.Tests.Android",
                    ["Jwt:SigningKey"] = "identity-tests-signing-key-with-32-characters",
                    ["Jwt:AccessTokenMinutes"] = "30",
                    ["Jwt:RefreshTokenDays"] = "30",
                    ["IdentitySecurity:HashPepper"] = "identity-tests-hash-pepper-with-32-characters"
                });
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISmsCodeGenerator>();
                services.RemoveAll<ISmsSender>();
                services.RemoveAll<IWechatLoginVerifier>();
                services.AddSingleton<ISmsCodeGenerator>(new FixedSmsCodeGenerator(KnownCode));
                services.AddSingleton<ISmsSender, CapturingSmsSender>();
                services.AddSingleton<IWechatLoginVerifier, TestWechatLoginVerifier>();
            });
        }
    }

    private sealed class FixedSmsCodeGenerator(string code) : ISmsCodeGenerator
    {
        public string GenerateCode()
        {
            return code;
        }
    }

    private sealed class CapturingSmsSender : ISmsSender
    {
        public List<string> SentCodes { get; } = [];

        public Task SendAsync(string normalizedPhoneNumber, string code, CancellationToken cancellationToken)
        {
            SentCodes.Add(code);
            return Task.CompletedTask;
        }
    }

    private sealed class TestWechatLoginVerifier : IWechatLoginVerifier
    {
        public Task<WechatLoginVerificationResult> VerifyAsync(string loginCode, CancellationToken cancellationToken)
        {
            return Task.FromResult(WechatLoginVerificationResult.Success($"openid-{loginCode}", $"unionid-{loginCode}"));
        }
    }
}
