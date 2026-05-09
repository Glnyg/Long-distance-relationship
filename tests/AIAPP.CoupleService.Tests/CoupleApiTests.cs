using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using AIAPP.CoupleService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AIAPP.CoupleService.Tests;

public sealed class CoupleApiTests
{
    [Fact]
    public async Task CreateInvitation_ReturnsLongCodeAndQrPayload()
    {
        await using var factory = new CoupleServiceFactory();
        using var client = factory.CreateAuthenticatedClient(Guid.NewGuid());

        var response = await client.PostAsJsonAsync("/couples/invitations", new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateInvitationBody>();
        Assert.NotNull(body);
        Assert.True(body.Code.Length >= 40);
        Assert.StartsWith("aiapp://couples/invitations/", body.QrPayload);
        Assert.EndsWith(body.Code, body.QrPayload);
    }

    [Fact]
    public async Task AcceptInvitation_CreatesCoupleAndInvalidatesInvitation()
    {
        await using var factory = new CoupleServiceFactory();
        var inviterId = Guid.NewGuid();
        var accepterId = Guid.NewGuid();
        using var inviterClient = factory.CreateAuthenticatedClient(inviterId);
        using var accepterClient = factory.CreateAuthenticatedClient(accepterId);

        var invitation = await CreateInvitationAsync(inviterClient);
        var acceptResponse = await accepterClient.PostAsync(
            $"/couples/invitations/{Uri.EscapeDataString(invitation.Code)}/accept",
            content: null);

        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);
        var couple = await acceptResponse.Content.ReadFromJsonAsync<CoupleBody>();
        Assert.NotNull(couple);
        Assert.Equal("active", couple.Status);
        Assert.Equal(inviterId, couple.UserAId);
        Assert.Equal(accepterId, couple.UserBId);

        var secondAcceptClient = factory.CreateAuthenticatedClient(Guid.NewGuid());
        var secondAcceptResponse = await secondAcceptClient.PostAsync(
            $"/couples/invitations/{Uri.EscapeDataString(invitation.Code)}/accept",
            content: null);

        Assert.Equal(HttpStatusCode.BadRequest, secondAcceptResponse.StatusCode);
        var error = await secondAcceptResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("invitation_invalid_or_expired", error?.Extensions["error_code"]?.ToString());

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoupleDbContext>();
        var savedInvitation = await db.Invitations.SingleAsync();
        Assert.NotNull(savedInvitation.AcceptedAtUtc);
        Assert.Equal(1, await db.Couples.CountAsync(c => c.Status == "active"));
    }

    [Fact]
    public async Task AcceptInvitation_WhenUserAlreadyHasActiveCouple_ReturnsConflict()
    {
        await using var factory = new CoupleServiceFactory();
        var firstInviterClient = factory.CreateAuthenticatedClient(Guid.NewGuid());
        var alreadyBoundUserId = Guid.NewGuid();
        var alreadyBoundClient = factory.CreateAuthenticatedClient(alreadyBoundUserId);
        var secondInviterClient = factory.CreateAuthenticatedClient(Guid.NewGuid());

        var firstInvitation = await CreateInvitationAsync(firstInviterClient);
        var firstAcceptResponse = await alreadyBoundClient.PostAsync(
            $"/couples/invitations/{Uri.EscapeDataString(firstInvitation.Code)}/accept",
            content: null);
        Assert.Equal(HttpStatusCode.OK, firstAcceptResponse.StatusCode);

        var secondInvitation = await CreateInvitationAsync(secondInviterClient);
        var secondAcceptResponse = await alreadyBoundClient.PostAsync(
            $"/couples/invitations/{Uri.EscapeDataString(secondInvitation.Code)}/accept",
            content: null);

        Assert.Equal(HttpStatusCode.Conflict, secondAcceptResponse.StatusCode);
        var error = await secondAcceptResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("couple_already_active", error?.Extensions["error_code"]?.ToString());
    }

    [Fact]
    public async Task UnbindCurrentCouple_ThenCurrentReturnsNotFound()
    {
        await using var factory = new CoupleServiceFactory();
        var inviterId = Guid.NewGuid();
        var accepterId = Guid.NewGuid();
        using var inviterClient = factory.CreateAuthenticatedClient(inviterId);
        using var accepterClient = factory.CreateAuthenticatedClient(accepterId);

        var invitation = await CreateInvitationAsync(inviterClient);
        var acceptResponse = await accepterClient.PostAsync(
            $"/couples/invitations/{Uri.EscapeDataString(invitation.Code)}/accept",
            content: null);
        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);

        var unbindResponse = await accepterClient.PostAsync("/couples/current/unbind", content: null);

        Assert.Equal(HttpStatusCode.OK, unbindResponse.StatusCode);
        var unbound = await unbindResponse.Content.ReadFromJsonAsync<CoupleBody>();
        Assert.NotNull(unbound);
        Assert.Equal("inactive", unbound.Status);
        Assert.NotNull(unbound.UnboundAtUtc);

        var currentResponse = await inviterClient.GetAsync("/couples/current");
        Assert.Equal(HttpStatusCode.NotFound, currentResponse.StatusCode);
        var error = await currentResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("couple_not_found", error?.Extensions["error_code"]?.ToString());
    }

    [Fact]
    public async Task AuditRecords_DoNotContainInvitationCode()
    {
        await using var factory = new CoupleServiceFactory();
        using var inviterClient = factory.CreateAuthenticatedClient(Guid.NewGuid());
        using var accepterClient = factory.CreateAuthenticatedClient(Guid.NewGuid());

        var invitation = await CreateInvitationAsync(inviterClient);
        var acceptResponse = await accepterClient.PostAsync(
            $"/couples/invitations/{Uri.EscapeDataString(invitation.Code)}/accept",
            content: null);
        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoupleDbContext>();
        var auditRows = await db.AuditEntries.AsNoTracking().ToListAsync();

        Assert.NotEmpty(auditRows);
        var auditJson = JsonSerializer.Serialize(auditRows);
        Assert.DoesNotContain(invitation.Code, auditJson, StringComparison.Ordinal);
    }

    private static async Task<CreateInvitationBody> CreateInvitationAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/couples/invitations", new { });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateInvitationBody>();
        Assert.NotNull(body);
        return body;
    }

    private sealed record CreateInvitationBody(string Code, string QrPayload, DateTimeOffset ExpiresAtUtc);

    private sealed record CoupleBody(
        Guid CoupleId,
        Guid UserAId,
        Guid UserBId,
        string Status,
        DateTimeOffset BoundAtUtc,
        DateTimeOffset? UnboundAtUtc);
}
