using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Testcontainers.PostgreSql;

namespace AIAPP.ConsentService.Tests;

public sealed class ConsentApiTests
{
    [DockerFact]
    public async Task Consent_flow_persists_versions_and_writes_privacy_safe_audit()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .Build();

        await postgres.StartAsync();

        using var app = ConsentServiceApplication.Create(postgres.GetConnectionString());
        using var client = app.CreateClient();

        var userId = Guid.NewGuid();
        var coupleId = Guid.NewGuid();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwt.Create(userId, coupleId));
        client.DefaultRequestHeaders.Add("X-Correlation-Id", "consent-flow-test");

        var enableResponse = await client.SendAsync(CreateSensitivePut("/consents/location_current"));
        var enableJson = await ReadJsonAsync(enableResponse);
        Assert.Equal(HttpStatusCode.OK, enableResponse.StatusCode);
        Assert.Equal("location_current", enableJson["dataType"]!.GetValue<string>());
        Assert.True(enableJson["isActive"]!.GetValue<bool>());
        var enabledVersion = enableJson["consentVersion"]!.GetValue<string>();
        Assert.False(string.IsNullOrWhiteSpace(enabledVersion));

        var currentResponse = await client.GetAsync("/consents/current");
        var currentJson = await ReadJsonAsync(currentResponse);
        Assert.Equal(HttpStatusCode.OK, currentResponse.StatusCode);
        var locationCurrent = FindConsent(currentJson, "location_current");
        Assert.True(locationCurrent["isActive"]!.GetValue<bool>());
        Assert.Equal(enabledVersion, locationCurrent["consentVersion"]!.GetValue<string>());

        var revokeResponse = await client.DeleteAsync("/consents/location_current");
        var revokeJson = await ReadJsonAsync(revokeResponse);
        Assert.Equal(HttpStatusCode.OK, revokeResponse.StatusCode);
        Assert.False(revokeJson["isActive"]!.GetValue<bool>());
        var revokedVersion = revokeJson["consentVersion"]!.GetValue<string>();
        Assert.NotEqual(enabledVersion, revokedVersion);

        var currentAfterRevokeResponse = await client.GetAsync("/consents/current");
        var currentAfterRevokeJson = await ReadJsonAsync(currentAfterRevokeResponse);
        Assert.False(FindConsent(currentAfterRevokeJson, "location_current")["isActive"]!.GetValue<bool>());

        var invalidResponse = await client.SendAsync(CreateSensitivePut("/consents/chat"));
        var invalidJson = await ReadJsonAsync(invalidResponse);
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
        Assert.Equal("consent.data_type.invalid", invalidJson["error_code"]!.GetValue<string>());

        await AssertAuditMetadataDoesNotContainPrivacyRawTextAsync(postgres.GetConnectionString());
    }

    private static HttpRequestMessage CreateSensitivePut(string uri)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, uri);
        request.Content = new StringContent(
            """
            {
              "chatText": "今晚的聊天原文不应该进入审计",
              "latitude": 39.908823,
              "longitude": 116.397470,
              "wifiName": "HomeWifi-Private",
              "verificationCode": "123456",
              "token": "secret-token",
              "prompt": "请分析我们的关系"
            }
            """,
            Encoding.UTF8,
            "application/json");
        return request;
    }

    private static async Task<JsonObject> ReadJsonAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        var node = await JsonNode.ParseAsync(stream);
        return Assert.IsType<JsonObject>(node);
    }

    private static JsonObject FindConsent(JsonObject root, string dataType)
    {
        var consents = Assert.IsType<JsonArray>(root["consents"]);
        foreach (var item in consents)
        {
            var consent = Assert.IsType<JsonObject>(item);
            if (consent["dataType"]?.GetValue<string>() == dataType)
            {
                return consent;
            }
        }

        throw new InvalidOperationException($"Consent '{dataType}' was not returned.");
    }

    private static async Task AssertAuditMetadataDoesNotContainPrivacyRawTextAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select metadata_json::text
            from consent.local_audit_entries
            order by created_at_utc asc
            """;

        var metadataDocuments = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            metadataDocuments.Add(reader.GetString(0));
        }

        Assert.NotEmpty(metadataDocuments);
        var auditJson = string.Join(Environment.NewLine, metadataDocuments);
        Assert.Contains("data_type", auditJson);
        Assert.Contains("consent_version", auditJson);
        Assert.DoesNotContain("今晚的聊天原文", auditJson);
        Assert.DoesNotContain("39.908823", auditJson);
        Assert.DoesNotContain("116.397470", auditJson);
        Assert.DoesNotContain("HomeWifi-Private", auditJson);
        Assert.DoesNotContain("123456", auditJson);
        Assert.DoesNotContain("secret-token", auditJson);
        Assert.DoesNotContain("请分析我们的关系", auditJson);
    }

    private sealed class ConsentServiceApplication : IDisposable
    {
        private readonly IDisposable _baseFactory;
        private readonly IDisposable _configuredFactory;

        private ConsentServiceApplication(IDisposable baseFactory, IDisposable configuredFactory)
        {
            _baseFactory = baseFactory;
            _configuredFactory = configuredFactory;
        }

        public static ConsentServiceApplication Create(string connectionString)
        {
            var assembly = typeof(ConsentApiTests).Assembly;
            _ = assembly;
            var entryPointAssembly = System.Reflection.Assembly.Load("AIAPP.ConsentService");
            var entryPointType = entryPointAssembly.GetType("Program", throwOnError: true)!;
            var factoryType = typeof(WebApplicationFactory<>).MakeGenericType(entryPointType);
            var baseFactory = (IDisposable)Activator.CreateInstance(factoryType)!;

            var withBuilder = factoryType.GetMethods()
                .Single(method => method.Name == "WithWebHostBuilder" && method.GetParameters().Length == 1);
            var configuredFactory = (IDisposable)withBuilder.Invoke(
                baseFactory,
                [
                    new Action<IWebHostBuilder>(builder =>
                    {
                        builder.UseEnvironment("Testing");
                        builder.ConfigureAppConfiguration((_, configuration) =>
                        {
                            configuration.AddInMemoryCollection(new Dictionary<string, string?>
                            {
                                ["ConnectionStrings:Consent"] = connectionString,
                                ["Jwt:Issuer"] = TestJwt.Issuer,
                                ["Jwt:Audience"] = TestJwt.Audience,
                                ["Jwt:SigningKey"] = TestJwt.SigningKey
                            });
                        });
                    })
                ])!;

            return new ConsentServiceApplication(baseFactory, configuredFactory);
        }

        public HttpClient CreateClient()
        {
            var method = _configuredFactory.GetType().GetMethod("CreateClient", Type.EmptyTypes);
            Assert.NotNull(method);
            return (HttpClient)method.Invoke(_configuredFactory, null)!;
        }

        public void Dispose()
        {
            _configuredFactory.Dispose();
            _baseFactory.Dispose();
        }
    }

    private static class TestJwt
    {
        public const string Issuer = "AIAPP.Tests";
        public const string Audience = "AIAPP.Android.Tests";
        public const string SigningKey = "CHANGE_ME_TEST_SIGNING_KEY_1234567890";

        public static string Create(Guid userId, Guid coupleId)
        {
            var now = DateTimeOffset.UtcNow;
            var header = new Dictionary<string, object?>
            {
                ["alg"] = "HS256",
                ["typ"] = "JWT"
            };
            var payload = new Dictionary<string, object?>
            {
                ["iss"] = Issuer,
                ["aud"] = Audience,
                ["exp"] = now.AddMinutes(30).ToUnixTimeSeconds(),
                ["nbf"] = now.AddMinutes(-1).ToUnixTimeSeconds(),
                ["iat"] = now.ToUnixTimeSeconds(),
                ["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"] = userId.ToString(),
                ["couple_id"] = coupleId.ToString()
            };

            var signingInput = $"{Base64Url(JsonSerializer.SerializeToUtf8Bytes(header))}.{Base64Url(JsonSerializer.SerializeToUtf8Bytes(payload))}";
            var signature = HMACSHA256.HashData(Encoding.UTF8.GetBytes(SigningKey), Encoding.ASCII.GetBytes(signingInput));
            return $"{signingInput}.{Base64Url(signature)}";
        }

        private static string Base64Url(byte[] bytes)
        {
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }
    }
}
