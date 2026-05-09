using System.Security.Claims;
using System.Text;
using AIAPP.Backend.Shared.Api;
using AIAPP.Backend.Shared.Auth;
using AIAPP.CoupleService;
using AIAPP.CoupleService.Infrastructure.Persistence;
using AIAPP.Observability;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions
        {
            Issuer = "AIAPP.Tests",
            Audience = "AIAPP.Android.Tests",
            SigningKey = "test-signing-key-for-couple-service-32bytes"
        };

        builder.Services.AddAiAppObservability(options => options.ServiceName = "AIAPP.CoupleService");
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUserAccessor, HttpContextCurrentUserAccessor>();
        builder.Services.AddSingleton(jwtOptions);
        builder.Services.AddMediatR(configuration => configuration.RegisterServicesFromAssembly(typeof(Program).Assembly));
        builder.Services.AddDbContext<CoupleDbContext>(options =>
        {
            var connectionString = builder.Configuration.GetConnectionString("CoupleDb")
                ?? builder.Configuration.GetConnectionString("Default")
                ?? "Host=localhost;Database=aiapp;Username=CHANGE_ME;Password=CHANGE_ME";
            options.UseNpgsql(connectionString);
        });

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey))
                };
            });
        builder.Services.AddAuthorization();

        var app = builder.Build();
        app.UseAiAppCorrelationId();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapHealthChecks("/healthz");

        app.MapPost("/couples/invitations", CreateInvitationAsync).RequireAuthorization();
        app.MapPost("/couples/invitations/{code}/accept", AcceptInvitationAsync).RequireAuthorization();
        app.MapGet("/couples/current", GetCurrentAsync).RequireAuthorization();
        app.MapPost("/couples/current/unbind", UnbindCurrentAsync).RequireAuthorization();

        app.Run();
    }

    private static async Task<IResult> CreateInvitationAsync(
        ClaimsPrincipal principal,
        CoupleDbContext db,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        if (await HasActiveCoupleAsync(db, userId, cancellationToken))
        {
            return ApiError.Conflict("couple_already_active", "当前账号已经存在有效情侣关系。").ToResult(httpContext);
        }

        var now = TimeProvider.System.GetUtcNow();
        var invitation = CoupleInvitationRecord.Create(userId, now);
        db.Invitations.Add(invitation);
        db.AuditEntries.Add(CoupleAuditEntryRecord.Create("couple.invitation.create", userId, null, "ok", "none", now));
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new CreateInvitationResponse(invitation.Code, invitation.QrPayload, invitation.ExpiresAtUtc));
    }

    private static async Task<IResult> AcceptInvitationAsync(
        string code,
        ClaimsPrincipal principal,
        CoupleDbContext db,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var accepterId = GetUserId(principal);
        var now = TimeProvider.System.GetUtcNow();
        var invitation = await db.Invitations.SingleOrDefaultAsync(item => item.Code == code, cancellationToken);
        if (invitation is null || invitation.AcceptedAtUtc is not null || invitation.ExpiresAtUtc < now)
        {
            return ApiError.BadRequest("invitation_invalid_or_expired", "绑定邀请无效或已过期。").ToResult(httpContext);
        }

        if (await HasActiveCoupleAsync(db, invitation.InviterUserId, cancellationToken) || await HasActiveCoupleAsync(db, accepterId, cancellationToken))
        {
            return ApiError.Conflict("couple_already_active", "当前账号已经存在有效情侣关系。").ToResult(httpContext);
        }

        invitation.AcceptedAtUtc = now;
        var couple = CoupleRecord.Create(invitation.InviterUserId, accepterId, now);
        db.Couples.Add(couple);
        db.AuditEntries.Add(CoupleAuditEntryRecord.Create("couple.invitation.accept", accepterId, couple.CoupleId, "ok", "none", now));
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(CoupleBody.FromRecord(couple));
    }

    private static async Task<IResult> GetCurrentAsync(ClaimsPrincipal principal, CoupleDbContext db, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        var couple = await FindActiveCoupleAsync(db, userId, cancellationToken);
        return couple is null
            ? ApiError.NotFound("couple_not_found", "当前没有有效情侣关系。").ToResult(httpContext)
            : Results.Ok(CoupleBody.FromRecord(couple));
    }

    private static async Task<IResult> UnbindCurrentAsync(ClaimsPrincipal principal, CoupleDbContext db, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        var couple = await FindActiveCoupleAsync(db, userId, cancellationToken);
        if (couple is null)
        {
            return ApiError.NotFound("couple_not_found", "当前没有有效情侣关系。").ToResult(httpContext);
        }

        var now = TimeProvider.System.GetUtcNow();
        couple.Unbind(now);
        db.AuditEntries.Add(CoupleAuditEntryRecord.Create("couple.unbind", userId, couple.CoupleId, "ok", "none", now));
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(CoupleBody.FromRecord(couple));
    }

    private static Guid GetUserId(ClaimsPrincipal principal)
    {
        return Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Missing user id claim."));
    }

    private static Task<bool> HasActiveCoupleAsync(CoupleDbContext db, Guid userId, CancellationToken cancellationToken)
    {
        return db.Couples.AnyAsync(item => item.Status == CoupleStatus.Active && (item.UserAId == userId || item.UserBId == userId), cancellationToken);
    }

    private static Task<CoupleRecord?> FindActiveCoupleAsync(CoupleDbContext db, Guid userId, CancellationToken cancellationToken)
    {
        return db.Couples.SingleOrDefaultAsync(item => item.Status == CoupleStatus.Active && (item.UserAId == userId || item.UserBId == userId), cancellationToken);
    }
}
