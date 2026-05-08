using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;

namespace AIAPP.AiPrivacy;

public sealed class AiPrivacyGateway
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IAiConsentStore _consentStore;
    private readonly IAiPrivateDataSource _dataSource;
    private readonly IChatClient _chatClient;
    private readonly IAiAnalysisResultStore _resultStore;
    private readonly IAiAuditSink _auditSink;
    private readonly TimeProvider _timeProvider;
    private readonly AiPrivacyGatewayOptions _options;

    public AiPrivacyGateway(
        IAiConsentStore consentStore,
        IAiPrivateDataSource dataSource,
        IChatClient chatClient,
        IAiAnalysisResultStore resultStore,
        IAiAuditSink auditSink,
        TimeProvider timeProvider,
        AiPrivacyGatewayOptions options)
    {
        _consentStore = consentStore ?? throw new ArgumentNullException(nameof(consentStore));
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        _resultStore = resultStore ?? throw new ArgumentNullException(nameof(resultStore));
        _auditSink = auditSink ?? throw new ArgumentNullException(nameof(auditSink));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<AiAnalysisOutcome> AnalyzeAsync(AiAnalysisRequest request, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var toUtc = request.ToUtc ?? now;
        var fromUtc = request.FromUtc ?? toUtc.Subtract(_options.DefaultLookback);

        if (!IsValidRange(fromUtc, toUtc, request.DataTypes))
        {
            return AiAnalysisOutcome.Failure(AiAnalysisFailureCode.InvalidTimeRange, "The AI analysis time range is invalid.");
        }

        var binding = await _consentStore.GetActiveBindingAsync(request.CoupleId, cancellationToken).ConfigureAwait(false);
        if (binding is null)
        {
            return AiAnalysisOutcome.Failure(AiAnalysisFailureCode.BindingInactive, "The couple binding is not active.");
        }

        if (!binding.Contains(request.RequestedByUserId))
        {
            return AiAnalysisOutcome.Failure(AiAnalysisFailureCode.RequesterNotInCouple, "The requester is not part of the couple binding.");
        }

        var consentA = await _consentStore.GetConsentAsync(request.CoupleId, binding.PartnerAUserId, cancellationToken).ConfigureAwait(false);
        var consentB = await _consentStore.GetConsentAsync(request.CoupleId, binding.PartnerBUserId, cancellationToken).ConfigureAwait(false);
        if (!ConsentAllows(consentA, request.DataTypes) || !ConsentAllows(consentB, request.DataTypes))
        {
            await RecordAuditAsync(
                binding,
                request,
                fromUtc,
                toUtc,
                consentA,
                consentB,
                null,
                null,
                false,
                AiAnalysisFailureCode.ConsentMissing,
                cancellationToken).ConfigureAwait(false);

            return AiAnalysisOutcome.Failure(AiAnalysisFailureCode.ConsentMissing, "Both partners must separately consent to all requested AI data types.");
        }

        var prompt = await BuildPromptAsync(binding, request, fromUtc, toUtc, cancellationToken).ConfigureAwait(false);
        var chatOptions = new ChatOptions
        {
            Temperature = _options.Temperature,
            MaxOutputTokens = _options.MaxOutputTokens,
            ModelId = _options.ModelId,
            ResponseFormat = ChatResponseFormat.Json
        };

        ChatResponse response;
        try
        {
            response = await GetResponseWithRetryAsync(prompt, chatOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            await RecordAuditAsync(
                binding,
                request,
                fromUtc,
                toUtc,
                consentA,
                consentB,
                null,
                null,
                false,
                AiAnalysisFailureCode.ModelUnavailable,
                cancellationToken).ConfigureAwait(false);

            return AiAnalysisOutcome.Failure(AiAnalysisFailureCode.ModelUnavailable, "The AI model is temporarily unavailable.");
        }

        var modelOutput = TryParseModelOutput(response.Text);
        if (modelOutput is null)
        {
            await RecordAuditAsync(
                binding,
                request,
                fromUtc,
                toUtc,
                consentA,
                consentB,
                response,
                null,
                false,
                AiAnalysisFailureCode.InvalidModelOutput,
                cancellationToken).ConfigureAwait(false);

            return AiAnalysisOutcome.Failure(AiAnalysisFailureCode.InvalidModelOutput, "The AI model returned an invalid response format.");
        }

        if (!string.Equals(modelOutput.SafetyLabel, "safe", StringComparison.OrdinalIgnoreCase))
        {
            await RecordAuditAsync(
                binding,
                request,
                fromUtc,
                toUtc,
                consentA,
                consentB,
                response,
                null,
                false,
                AiAnalysisFailureCode.UnsafeModelOutput,
                cancellationToken).ConfigureAwait(false);

            return AiAnalysisOutcome.Failure(AiAnalysisFailureCode.UnsafeModelOutput, "The AI model returned content that failed safety checks.");
        }

        var result = new AiAnalysisResult(
            Guid.NewGuid(),
            request.CoupleId,
            request.RequestedByUserId,
            binding.PartnerIds,
            modelOutput.Summary.Trim(),
            modelOutput.Suggestions.Select(suggestion => suggestion.Trim()).Where(static suggestion => suggestion.Length > 0).ToArray(),
            now,
            now.Add(_options.ResultRetention),
            response.ModelId ?? _options.ModelId);

        await _resultStore.SaveAsync(result, cancellationToken).ConfigureAwait(false);
        await RecordAuditAsync(
            binding,
            request,
            fromUtc,
            toUtc,
            consentA,
            consentB,
            response,
            result.ResultId,
            true,
            AiAnalysisFailureCode.None,
            cancellationToken).ConfigureAwait(false);

        return AiAnalysisOutcome.Success(result);
    }

    public Task<int> DeleteExpiredResultsAsync(CancellationToken cancellationToken = default)
    {
        return _resultStore.DeleteExpiredAsync(_timeProvider.GetUtcNow(), cancellationToken);
    }

    private bool IsValidRange(DateTimeOffset fromUtc, DateTimeOffset toUtc, AiPrivacyDataTypes dataTypes)
    {
        return dataTypes != AiPrivacyDataTypes.None
            && fromUtc < toUtc
            && toUtc - fromUtc <= _options.MaxLookback;
    }

    private static bool ConsentAllows(AiConsentGrant? consent, AiPrivacyDataTypes requestedDataTypes)
    {
        return consent is { IsAiAnalysisEnabled: true }
            && (consent.GrantedDataTypes & requestedDataTypes) == requestedDataTypes;
    }

    private async Task<string> BuildPromptAsync(
        CoupleBinding binding,
        AiAnalysisRequest request,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        builder.AppendLine("You are analyzing a consenting adult couple's shared private data.");
        builder.AppendLine("Return strict JSON with: summary, suggestions, safetyLabel.");
        builder.AppendLine("Do not infer identity, diagnose, score the relationship, or mention data not present.");
        builder.AppendLine(CultureInfo.InvariantCulture, $"WindowUtc: {fromUtc:O} - {toUtc:O}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"DataTypes: {request.DataTypes}");

        if (request.DataTypes.HasFlag(AiPrivacyDataTypes.Chat))
        {
            var messages = await _dataSource.GetChatMessagesAsync(request.CoupleId, fromUtc, toUtc, cancellationToken).ConfigureAwait(false);
            AppendChatSection(builder, binding, messages);
        }

        if (request.DataTypes.HasFlag(AiPrivacyDataTypes.Location))
        {
            var points = await _dataSource.GetLocationPointsAsync(request.CoupleId, fromUtc, toUtc, cancellationToken).ConfigureAwait(false);
            AppendLocationSection(builder, points);
        }

        if (request.DataTypes.HasFlag(AiPrivacyDataTypes.DeviceState))
        {
            var states = await _dataSource.GetDeviceStatesAsync(request.CoupleId, fromUtc, toUtc, cancellationToken).ConfigureAwait(false);
            AppendDeviceStateSection(builder, states);
        }

        return builder.ToString();
    }

    private static void AppendChatSection(StringBuilder builder, CoupleBinding binding, IReadOnlyList<PrivateChatMessage> messages)
    {
        builder.AppendLine("ChatMessages:");
        foreach (var message in messages.OrderBy(static message => message.SentAtUtc).TakeLast(100))
        {
            builder.Append(CultureInfo.InvariantCulture, $"- {message.SentAtUtc:O} ");
            builder.Append(GetAlias(binding, message.UserId));
            builder.Append(": ");
            builder.AppendLine(RedactText(message.Text));
        }
    }

    private static void AppendLocationSection(StringBuilder builder, IReadOnlyList<PrivateLocationPoint> points)
    {
        builder.AppendLine("LocationSummary:");
        builder.AppendLine(CultureInfo.InvariantCulture, $"- pointCount: {points.Count}");
        var areas = points
            .Select(static point => point.AreaLabel)
            .Where(static label => !string.IsNullOrWhiteSpace(label))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToArray();
        if (areas.Length > 0)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"- areas: {string.Join(", ", areas)}");
        }
    }

    private static void AppendDeviceStateSection(StringBuilder builder, IReadOnlyList<PrivateDeviceState> states)
    {
        builder.AppendLine("DeviceStateSummary:");
        builder.AppendLine(CultureInfo.InvariantCulture, $"- sampleCount: {states.Count}");
        if (states.Count == 0)
        {
            return;
        }

        builder.AppendLine(CultureInfo.InvariantCulture, $"- averageBatteryPercent: {states.Average(static state => state.BatteryPercent):F1}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"- totalScreenOnMinutes: {states.Sum(static state => state.ScreenOnMinutes)}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"- chargingSampleCount: {states.Count(static state => state.IsCharging)}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"- hasNetworkConnectionSamples: {states.Any(static state => !string.IsNullOrWhiteSpace(state.ActiveNetworkName))}");
    }

    private async Task<ChatResponse> GetResponseWithRetryAsync(
        string prompt,
        ChatOptions chatOptions,
        CancellationToken cancellationToken)
    {
        Exception? lastException = null;
        for (var attempt = 0; attempt <= _options.MaxRetries; attempt++)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_options.RequestTimeout);
            try
            {
                return await _chatClient.GetResponseAsync(
                    [new ChatMessage(ChatRole.User, prompt)],
                    chatOptions.Clone(),
                    timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                lastException = new TimeoutException("AI model request timed out.");
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                lastException = ex;
            }
        }

        throw lastException ?? new InvalidOperationException("AI model call failed.");
    }

    private static ModelOutput? TryParseModelOutput(string text)
    {
        try
        {
            var normalized = StripJsonFence(text);
            var output = JsonSerializer.Deserialize<ModelOutput>(normalized, JsonOptions);
            if (output is null || string.IsNullOrWhiteSpace(output.Summary) || string.IsNullOrWhiteSpace(output.SafetyLabel))
            {
                return null;
            }

            return output;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task RecordAuditAsync(
        CoupleBinding binding,
        AiAnalysisRequest request,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        AiConsentGrant? consentA,
        AiConsentGrant? consentB,
        ChatResponse? response,
        Guid? resultId,
        bool succeeded,
        AiAnalysisFailureCode failureCode,
        CancellationToken cancellationToken)
    {
        var usage = response?.Usage;
        var entry = new AiAuditEntry(
            Guid.NewGuid(),
            request.CoupleId,
            request.RequestedByUserId,
            binding.PartnerAUserId,
            binding.PartnerBUserId,
            request.DataTypes,
            fromUtc,
            toUtc,
            consentA?.ConsentVersion ?? string.Empty,
            consentB?.ConsentVersion ?? string.Empty,
            response?.ModelId ?? _options.ModelId,
            usage?.InputTokenCount,
            usage?.OutputTokenCount,
            usage?.TotalTokenCount,
            resultId,
            _timeProvider.GetUtcNow(),
            succeeded,
            failureCode);

        await _auditSink.RecordAsync(entry, cancellationToken).ConfigureAwait(false);
    }

    private static string GetAlias(CoupleBinding binding, Guid userId)
    {
        if (userId == binding.PartnerAUserId)
        {
            return "PartnerA";
        }

        return userId == binding.PartnerBUserId ? "PartnerB" : "UnknownPartner";
    }

    private static string RedactText(string value)
    {
        var withoutEmails = Regex.Replace(value, @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", "[email]", RegexOptions.IgnoreCase);
        return Regex.Replace(withoutEmails, @"(?<!\d)\d{7,}(?!\d)", "[number]");
    }

    private static string StripJsonFence(string text)
    {
        var trimmed = text.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var firstNewLine = trimmed.IndexOf('\n', StringComparison.Ordinal);
        var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        if (firstNewLine < 0 || lastFence <= firstNewLine)
        {
            return trimmed;
        }

        return trimmed[(firstNewLine + 1)..lastFence].Trim();
    }

    private sealed record ModelOutput(
        string Summary,
        IReadOnlyList<string> Suggestions,
        string SafetyLabel);
}
