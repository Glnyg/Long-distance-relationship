using System.Security.Claims;
using System.Text;
using AIAPP.Backend.Shared.Api;
using AIAPP.Backend.Shared.Auth;
using AIAPP.ConsentService.Domain;
using AIAPP.ConsentService.Infrastructure.Persistence;
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
            Issuer = "AIAPP.ConsentService",
            Audience = "AIAPP.Android",
            SigningKey = "consent-service-development-signing-key-CHANGE_ME"
        };

        builder.Services.AddAiAppObservability(options => options.ServiceName = "AIAPP.ConsentService");
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUserAccessor, HttpContextCurrentUserAccessor>();
        builder.Services.AddSingleton(jwtOptions);
        builder.Services.AddMediatR(configuration => configuration.RegisterServicesFromAssembly(typeof(Program).Assembly));
        builder.Services.AddDbContext<ConsentDbContext>(options =>
        {
            var connectionString = builder.Configuration.GetConnectionString("Consent")
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

        if (app.Environment.IsEnvironment("Testing"))
        {
            using var scope = app.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<ConsentDbContext>().Database.EnsureCreated();
        }

        app.UseAiAppCorrelationId();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapHealthChecks("/healthz");

        app.MapGet("/consents/current", GetCurrentAsync).RequireAuthorization();
        app.MapPut("/consents/{dataType}", GrantAsync).RequireAuthorization();
        app.MapDelete("/consents/{dataType}", RevokeAsync).RequireAuthorization();

        app.Run();
    }

    private static async Task<IResult> GetCurrentAsync(ClaimsPrincipal principal, ConsentDbContext db, CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        var coupleId = GetCoupleId(principal);
        var grants = await db.ConsentGrants
            .Where(item => item.CoupleId == coupleId && item.UserId == userId)
            .ToListAsync(cancellationToken);
        var byType = grants.ToDictionary(item => item.DataType, StringComparer.OrdinalIgnoreCase);
        var consents = ConsentDataType.All.Select(type =>
        {
            byType.TryGetValue(type.Value, out var grant);
            return ConsentBody.From(type, grant);
        }).ToArray();

        return Results.Ok(new { coupleId, userId, consents });
    }

    private static Task<IResult> GrantAsync(string dataType, ClaimsPrincipal principal, ConsentDbContext db, HttpContext httpContext, CancellationToken cancellationToken)
    {
        return ChangeConsentAsync(dataType, principal, db, httpContext, active: true, cancellationToken);
    }

    private static Task<IResult> RevokeAsync(string dataType, ClaimsPrincipal principal, ConsentDbContext db, HttpContext httpContext, CancellationToken cancellationToken)
    {
        return ChangeConsentAsync(dataType, principal, db, httpContext, active: false, cancellationToken);
    }

    private static async Task<IResult> ChangeConsentAsync(
        string dataType,
        ClaimsPrincipal principal,
        ConsentDbContext db,
        HttpContext httpContext,
        bool active,
        CancellationToken cancellationToken)
    {
        if (!ConsentDataType.TryParse(dataType, out var parsed))
        {
            return ApiError.BadRequest("consent.data_type.invalid", "授权类型不存在。").ToResult(httpContext);
        }

        var now = TimeProvider.System.GetUtcNow();
        var userId = GetUserId(principal);
        var coupleId = GetCoupleId(principal);
        var consentType = parsed!;
        var grant = await db.ConsentGrants.SingleOrDefaultAsync(
            item => item.CoupleId == coupleId && item.UserId == userId && item.DataType == consentType.Value,
            cancellationToken);
        if (grant is null)
        {
            grant = ConsentGrantRecord.Create(coupleId, userId, consentType, active, now);
            db.ConsentGrants.Add(grant);
        }
        else
        {
            grant.Change(active, now);
        }

        db.AuditEntries.Add(ConsentAuditEntryRecord.FromGrant(active ? "consent.grant" : "consent.revoke", grant, "ok", "none", now));
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ConsentBody.From(consentType, grant));
    }

    private static Guid GetUserId(ClaimsPrincipal principal)
    {
        return Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")
            ?? throw new InvalidOperationException("Missing user id claim."));
    }

    private static Guid GetCoupleId(ClaimsPrincipal principal)
    {
        return Guid.Parse(principal.FindFirstValue("couple_id") ?? throw new InvalidOperationException("Missing couple id claim."));
    }
}
