using System.Security.Claims;
using System.Text.Json;
using AIAPP.Backend.Shared.Api;
using AIAPP.Backend.Shared.Audit;
using AIAPP.Backend.Shared.Auth;
using AIAPP.Observability;
using Microsoft.AspNetCore.Http;

namespace AIAPP.Backend.Shared.Tests;

public sealed class BackendSharedTests
{
    [Fact]
    public void ToProblemDetails_preserves_stable_error_code_chinese_message_and_correlation_id()
    {
        var context = new DefaultHttpContext();
        context.Items[AiAppTelemetryNames.CorrelationIdItemName] = "corr-123";

        var problem = ApiError.Conflict("couple_already_bound", "当前账号已经绑定情侣。")
            .ToProblemDetails(context);

        Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
        Assert.Equal("couple_already_bound", problem.Extensions["error_code"]);
        Assert.Equal("当前账号已经绑定情侣。", problem.Extensions["message"]);
        Assert.Equal("corr-123", problem.Extensions["correlation_id"]);
    }

    [Fact]
    public void CurrentUserAccessor_reads_user_id_from_authenticated_claim()
    {
        var userId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
                authenticationType: "Test"))
        };

        var accessor = new HttpContextCurrentUserAccessor(new HttpContextAccessor { HttpContext = context });

        Assert.True(accessor.TryGetUserId(out var actual));
        Assert.Equal(userId, actual);
    }

    [Fact]
    public void AuditEntry_create_hashes_identifiers_and_omits_private_payload()
    {
        var userId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var coupleId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        var entry = AuditEntry.Create(
            "consent.grant",
            userId,
            coupleId,
            "ok",
            "none",
            "v1",
            new DateTimeOffset(2026, 5, 9, 4, 0, 0, TimeSpan.Zero));

        var json = JsonSerializer.Serialize(entry);

        Assert.Contains(TelemetrySanitizer.HashIdentifier(userId), json);
        Assert.Contains(TelemetrySanitizer.HashIdentifier(coupleId), json);
        Assert.DoesNotContain(userId.ToString(), json);
        Assert.DoesNotContain(coupleId.ToString(), json);
    }
}
