using System.Diagnostics;
using System.Text.Json;
using AIAPP.AiPrivacy;
using AIAPP.Observability;
using Microsoft.Extensions.AI;

namespace AIAPP.AiPrivacy.Tests;

public sealed class AiPrivacyGatewayTests
{
    private static readonly Guid CoupleId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid UserA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset Now = new(2026, 5, 9, 4, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AnalyzeAsync_rejects_request_when_partner_has_not_granted_all_requested_data_types()
    {
        var consentStore = new StubConsentStore(
            new CoupleBinding(CoupleId, UserA, UserB, true),
            new AiConsentGrant(UserA, true, AiPrivacyDataTypes.Chat | AiPrivacyDataTypes.Location, "a-v1"),
            new AiConsentGrant(UserB, true, AiPrivacyDataTypes.Chat, "b-v1"));
        var dataSource = new RecordingPrivateDataSource();
        var chatClient = new RecordingChatClient(SafeJson());
        var gateway = CreateGateway(consentStore, dataSource, chatClient);

        var outcome = await gateway.AnalyzeAsync(new AiAnalysisRequest(
            CoupleId,
            UserA,
            AiPrivacyDataTypes.Chat | AiPrivacyDataTypes.Location,
            Now.AddDays(-1),
            Now));

        Assert.False(outcome.Succeeded);
        Assert.Equal(AiAnalysisFailureCode.ConsentMissing, outcome.FailureCode);
        Assert.Equal(0, dataSource.ChatCalls + dataSource.LocationCalls + dataSource.DeviceStateCalls);
        Assert.Equal(0, chatClient.CallCount);
    }

    [Fact]
    public async Task AnalyzeAsync_only_loads_and_prompts_requested_authorized_data_types()
    {
        var dataSource = new RecordingPrivateDataSource
        {
            ChatMessages =
            [
                new PrivateChatMessage(UserA, Now.AddHours(-2), "chat secret 13800138000")
            ],
            LocationPoints =
            [
                new PrivateLocationPoint(UserA, Now.AddHours(-1), 31.2304, 121.4737, "Shanghai")
            ],
            DeviceStates =
            [
                new PrivateDeviceState(UserA, Now.AddHours(-1), 120, 66, true, "HomeWiFiSecret")
            ]
        };
        var chatClient = new RecordingChatClient(SafeJson());
        var gateway = CreateGateway(CreateAllConsentStore(), dataSource, chatClient);

        var outcome = await gateway.AnalyzeAsync(new AiAnalysisRequest(
            CoupleId,
            UserA,
            AiPrivacyDataTypes.Chat,
            Now.AddDays(-1),
            Now));

        Assert.True(outcome.Succeeded);
        Assert.Equal(1, dataSource.ChatCalls);
        Assert.Equal(0, dataSource.LocationCalls);
        Assert.Equal(0, dataSource.DeviceStateCalls);
        Assert.Equal(1, chatClient.CallCount);
        Assert.Contains("chat secret", chatClient.PromptText);
        Assert.DoesNotContain("31.2304", chatClient.PromptText);
        Assert.DoesNotContain("HomeWiFiSecret", chatClient.PromptText);
        Assert.Equal(0f, chatClient.CapturedOptions?.Temperature);
        Assert.Equal(800, chatClient.CapturedOptions?.MaxOutputTokens);
        Assert.Equal("domestic-model-v1", chatClient.CapturedOptions?.ModelId);
    }

    [Fact]
    public async Task AnalyzeAsync_saves_only_shared_result_with_30_day_expiry_and_no_prompt_snapshot()
    {
        var dataSource = new RecordingPrivateDataSource
        {
            ChatMessages =
            [
                new PrivateChatMessage(UserA, Now.AddHours(-2), "do not persist this prompt input")
            ]
        };
        var resultStore = new RecordingResultStore();
        var gateway = CreateGateway(CreateAllConsentStore(), dataSource, new RecordingChatClient(SafeJson()), resultStore);

        var outcome = await gateway.AnalyzeAsync(new AiAnalysisRequest(
            CoupleId,
            UserA,
            AiPrivacyDataTypes.Chat,
            Now.AddDays(-1),
            Now));

        Assert.True(outcome.Succeeded);
        var saved = Assert.Single(resultStore.Saved);
        Assert.Equal(Now.AddDays(30), saved.ExpiresAtUtc);
        Assert.Equal([UserA, UserB], saved.VisibleToUserIds.OrderBy(id => id).ToArray());
        Assert.DoesNotContain("do not persist this prompt input", JsonSerializer.Serialize(saved));
    }

    [Fact]
    public async Task AnalyzeAsync_records_only_audit_metadata_without_private_payload()
    {
        var dataSource = new RecordingPrivateDataSource
        {
            ChatMessages =
            [
                new PrivateChatMessage(UserA, Now.AddHours(-2), "private-chat-secret")
            ],
            LocationPoints =
            [
                new PrivateLocationPoint(UserA, Now.AddHours(-1), 31.2304, 121.4737, "ExactHome")
            ],
            DeviceStates =
            [
                new PrivateDeviceState(UserA, Now.AddHours(-1), 44, 80, false, "HomeWiFiSecret")
            ]
        };
        var auditSink = new RecordingAuditSink();
        var gateway = CreateGateway(CreateAllConsentStore(), dataSource, new RecordingChatClient(SafeJson()), auditSink: auditSink);

        var outcome = await gateway.AnalyzeAsync(new AiAnalysisRequest(
            CoupleId,
            UserA,
            AiPrivacyDataTypes.Chat | AiPrivacyDataTypes.Location | AiPrivacyDataTypes.DeviceState,
            Now.AddDays(-1),
            Now));

        Assert.True(outcome.Succeeded);
        var auditJson = JsonSerializer.Serialize(Assert.Single(auditSink.Entries));
        Assert.Contains("domestic-model-v1", auditJson);
        Assert.DoesNotContain("private-chat-secret", auditJson);
        Assert.DoesNotContain("31.2304", auditJson);
        Assert.DoesNotContain("121.4737", auditJson);
        Assert.DoesNotContain("HomeWiFiSecret", auditJson);
    }

    [Fact]
    public async Task AnalyzeAsync_emits_privacy_safe_trace_metadata()
    {
        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AiAppTelemetryNames.ActivitySources.AiPrivacy,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activities.Add
        };
        ActivitySource.AddActivityListener(listener);

        var dataSource = new RecordingPrivateDataSource
        {
            ChatMessages =
            [
                new PrivateChatMessage(UserA, Now.AddHours(-2), "private-chat-secret")
            ],
            LocationPoints =
            [
                new PrivateLocationPoint(UserA, Now.AddHours(-1), 31.2304, 121.4737, "ExactHome")
            ],
            DeviceStates =
            [
                new PrivateDeviceState(UserA, Now.AddHours(-1), 44, 80, false, "HomeWiFiSecret")
            ]
        };
        var gateway = CreateGateway(CreateAllConsentStore(), dataSource, new RecordingChatClient(SafeJson()));

        var outcome = await gateway.AnalyzeAsync(new AiAnalysisRequest(
            CoupleId,
            UserA,
            AiPrivacyDataTypes.Chat | AiPrivacyDataTypes.Location | AiPrivacyDataTypes.DeviceState,
            Now.AddDays(-1),
            Now));

        Assert.True(outcome.Succeeded);
        var operationNames = activities.Select(activity => activity.OperationName).ToArray();
        Assert.Contains("ai_privacy.load_binding", operationNames);
        Assert.Contains("ai_privacy.load_consents", operationNames);
        Assert.Contains("ai_privacy.build_prompt", operationNames);
        Assert.Contains("ai_privacy.model_call", operationNames);
        Assert.Contains("ai_privacy.save_result", operationNames);
        Assert.Contains("ai_privacy.record_audit", operationNames);

        var tagText = string.Join(
            "|",
            activities.SelectMany(activity => activity.Tags).Select(tag => $"{tag.Key}={tag.Value}"));

        Assert.Contains(AiAppTelemetryNames.Tags.CoupleIdHash, tagText);
        Assert.Contains(AiAppTelemetryNames.Tags.UserIdHash, tagText);
        Assert.DoesNotContain(CoupleId.ToString(), tagText);
        Assert.DoesNotContain(UserA.ToString(), tagText);
        Assert.DoesNotContain("private-chat-secret", tagText);
        Assert.DoesNotContain("31.2304", tagText);
        Assert.DoesNotContain("121.4737", tagText);
        Assert.DoesNotContain("HomeWiFiSecret", tagText);
    }

    [Fact]
    public async Task AnalyzeAsync_returns_explainable_failure_when_model_returns_invalid_json()
    {
        var resultStore = new RecordingResultStore();
        var auditSink = new RecordingAuditSink();
        var gateway = CreateGateway(
            CreateAllConsentStore(),
            new RecordingPrivateDataSource(),
            new RecordingChatClient("not json"),
            resultStore,
            auditSink);

        var outcome = await gateway.AnalyzeAsync(new AiAnalysisRequest(
            CoupleId,
            UserA,
            AiPrivacyDataTypes.Chat,
            Now.AddDays(-1),
            Now));

        Assert.False(outcome.Succeeded);
        Assert.Equal(AiAnalysisFailureCode.InvalidModelOutput, outcome.FailureCode);
        Assert.Empty(resultStore.Saved);
        Assert.Equal(AiAnalysisFailureCode.InvalidModelOutput, Assert.Single(auditSink.Entries).FailureCode);
    }

    [Fact]
    public async Task AnalyzeAsync_retries_model_timeout_before_returning_success()
    {
        var chatClient = new TimeoutThenSuccessChatClient(SafeJson());
        var gateway = CreateGateway(CreateAllConsentStore(), new RecordingPrivateDataSource(), chatClient);

        var outcome = await gateway.AnalyzeAsync(new AiAnalysisRequest(
            CoupleId,
            UserA,
            AiPrivacyDataTypes.Chat,
            Now.AddDays(-1),
            Now));

        Assert.True(outcome.Succeeded);
        Assert.Equal(2, chatClient.CallCount);
    }

    [Fact]
    public async Task DeleteExpiredResultsAsync_delegates_retention_cutoff_to_store()
    {
        var resultStore = new RecordingResultStore();
        var gateway = CreateGateway(CreateAllConsentStore(), new RecordingPrivateDataSource(), new RecordingChatClient(SafeJson()), resultStore);

        var deleted = await gateway.DeleteExpiredResultsAsync();

        Assert.Equal(3, deleted);
        Assert.Equal(Now, resultStore.LastDeleteCutoffUtc);
    }

    private static AiPrivacyGateway CreateGateway(
        IAiConsentStore consentStore,
        IAiPrivateDataSource dataSource,
        IChatClient chatClient,
        IAiAnalysisResultStore? resultStore = null,
        IAiAuditSink? auditSink = null)
    {
        return new AiPrivacyGateway(
            consentStore,
            dataSource,
            chatClient,
            resultStore ?? new RecordingResultStore(),
            auditSink ?? new RecordingAuditSink(),
            new FixedTimeProvider(Now),
            new AiPrivacyGatewayOptions
            {
                ModelId = "domestic-model-v1",
                MaxOutputTokens = 800,
                Temperature = 0f,
                DefaultLookback = TimeSpan.FromDays(7),
                MaxLookback = TimeSpan.FromDays(30),
                ResultRetention = TimeSpan.FromDays(30),
                RequestTimeout = TimeSpan.FromSeconds(5),
                MaxRetries = 1
            });
    }

    private static StubConsentStore CreateAllConsentStore()
    {
        return new StubConsentStore(
            new CoupleBinding(CoupleId, UserA, UserB, true),
            new AiConsentGrant(UserA, true, AiPrivacyDataTypes.Chat | AiPrivacyDataTypes.Location | AiPrivacyDataTypes.DeviceState, "a-v1"),
            new AiConsentGrant(UserB, true, AiPrivacyDataTypes.Chat | AiPrivacyDataTypes.Location | AiPrivacyDataTypes.DeviceState, "b-v1"));
    }

    private static string SafeJson()
    {
        return """
            {
              "summary": "shared summary",
              "suggestions": ["talk kindly"],
              "safetyLabel": "safe"
            }
            """;
    }

    private sealed class StubConsentStore(
        CoupleBinding? binding,
        AiConsentGrant? userAConsent,
        AiConsentGrant? userBConsent) : IAiConsentStore
    {
        public Task<CoupleBinding?> GetActiveBindingAsync(Guid coupleId, CancellationToken cancellationToken)
        {
            return Task.FromResult(binding is { IsActive: true } && binding.CoupleId == coupleId ? binding : null);
        }

        public Task<AiConsentGrant?> GetConsentAsync(Guid coupleId, Guid userId, CancellationToken cancellationToken)
        {
            var consent = userId == UserA ? userAConsent : userId == UserB ? userBConsent : null;
            return Task.FromResult(consent);
        }
    }

    private sealed class RecordingPrivateDataSource : IAiPrivateDataSource
    {
        public IReadOnlyList<PrivateChatMessage> ChatMessages { get; init; } = [];
        public IReadOnlyList<PrivateLocationPoint> LocationPoints { get; init; } = [];
        public IReadOnlyList<PrivateDeviceState> DeviceStates { get; init; } = [];
        public int ChatCalls { get; private set; }
        public int LocationCalls { get; private set; }
        public int DeviceStateCalls { get; private set; }

        public Task<IReadOnlyList<PrivateChatMessage>> GetChatMessagesAsync(Guid coupleId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken)
        {
            ChatCalls++;
            return Task.FromResult(ChatMessages);
        }

        public Task<IReadOnlyList<PrivateLocationPoint>> GetLocationPointsAsync(Guid coupleId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken)
        {
            LocationCalls++;
            return Task.FromResult(LocationPoints);
        }

        public Task<IReadOnlyList<PrivateDeviceState>> GetDeviceStatesAsync(Guid coupleId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken)
        {
            DeviceStateCalls++;
            return Task.FromResult(DeviceStates);
        }
    }

    private sealed class RecordingChatClient(string responseText) : IChatClient
    {
        public int CallCount { get; private set; }
        public string PromptText { get; private set; } = string.Empty;
        public ChatOptions? CapturedOptions { get; private set; }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            CallCount++;
            CapturedOptions = options;
            PromptText = string.Join("\n", messages.Select(message => message.Text));
            var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText))
            {
                ModelId = options?.ModelId,
                Usage = new UsageDetails
                {
                    InputTokenCount = 123,
                    OutputTokenCount = 45,
                    TotalTokenCount = 168
                }
            };
            return Task.FromResult(response);
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
        {
            return null;
        }

        public void Dispose()
        {
        }
    }

    private sealed class TimeoutThenSuccessChatClient(string responseText) : IChatClient
    {
        public int CallCount { get; private set; }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (CallCount == 1)
            {
                throw new OperationCanceledException("simulated model timeout");
            }

            var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText))
            {
                ModelId = options?.ModelId,
                Usage = new UsageDetails
                {
                    InputTokenCount = 10,
                    OutputTokenCount = 5,
                    TotalTokenCount = 15
                }
            };
            return Task.FromResult(response);
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
        {
            return null;
        }

        public void Dispose()
        {
        }
    }

    private sealed class RecordingResultStore : IAiAnalysisResultStore
    {
        public List<AiAnalysisResult> Saved { get; } = [];
        public DateTimeOffset? LastDeleteCutoffUtc { get; private set; }

        public Task SaveAsync(AiAnalysisResult result, CancellationToken cancellationToken)
        {
            Saved.Add(result);
            return Task.CompletedTask;
        }

        public Task<int> DeleteExpiredAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken)
        {
            LastDeleteCutoffUtc = cutoffUtc;
            return Task.FromResult(3);
        }
    }

    private sealed class RecordingAuditSink : IAiAuditSink
    {
        public List<AiAuditEntry> Entries { get; } = [];

        public Task RecordAsync(AiAuditEntry entry, CancellationToken cancellationToken)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return now;
        }
    }
}
