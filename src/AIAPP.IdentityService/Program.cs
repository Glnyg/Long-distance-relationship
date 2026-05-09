using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AIAPP.Backend.Shared.Api;
using AIAPP.Backend.Shared.Auth;
using AIAPP.IdentityService;
using AIAPP.IdentityService.Application.Abstractions;
using AIAPP.IdentityService.Infrastructure.Persistence;
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
            Issuer = "AIAPP.IdentityService",
            Audience = "AIAPP.Android",
            SigningKey = "identity-service-development-signing-key-CHANGE_ME"
        };

        builder.Services.AddAiAppObservability(options => options.ServiceName = "AIAPP.IdentityService");
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUserAccessor, HttpContextCurrentUserAccessor>();
        builder.Services.AddSingleton(jwtOptions);
        builder.Services.AddSingleton<IPhoneProtector, DevelopmentPhoneProtector>();
        builder.Services.AddSingleton<ISmsCodeGenerator, RandomSmsCodeGenerator>();
        builder.Services.AddSingleton<ISmsSender, NoopSmsSender>();
        builder.Services.AddSingleton<IWechatLoginVerifier, DisabledWechatLoginVerifier>();
        builder.Services.AddMediatR(configuration => configuration.RegisterServicesFromAssembly(typeof(Program).Assembly));
        builder.Services.AddDbContext<IdentityDbContext>(options =>
        {
            var connectionString = builder.Configuration.GetConnectionString("Identity")
                ?? builder.Configuration.GetConnectionString("Default")
                ?? "Host=localhost;Database=aiapp;Username=CHANGE_ME;Password=CHANGE_ME";
            options.UseNpgsql(connectionString);
        });

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey));
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
                    IssuerSigningKey = key
                };
            });
        builder.Services.AddAuthorization();

        var app = builder.Build();
        app.UseAiAppCorrelationId();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapHealthChecks("/healthz");

        app.MapPost("/auth/sms/send-code", SendSmsCodeAsync);
        app.MapPost("/auth/sms/login", SmsLoginAsync);
        app.MapPost("/auth/wechat/login", WechatLoginAsync);
        app.MapPost("/auth/token/refresh", RefreshTokenAsync);
        app.MapPost("/auth/logout", LogoutAsync).RequireAuthorization();
        app.MapGet("/auth/me", MeAsync).RequireAuthorization();

        app.Run();
    }

    private static async Task<IResult> SendSmsCodeAsync(
        SmsCodeRequest request,
        IdentityDbContext db,
        ISmsCodeGenerator codeGenerator,
        ISmsSender smsSender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var now = TimeProvider.System.GetUtcNow();
        var normalizedPhone = PhoneNumber.Normalize(request.PhoneNumber);
        var code = codeGenerator.GenerateCode();
        db.VerificationCodes.Add(VerificationCodeRecord.Create(normalizedPhone, code, now));
        db.AuditEntries.Add(IdentityAudit.Create("identity.sms_code.sent", null, "ok", "none", now));
        await db.SaveChangesAsync(cancellationToken);
        await smsSender.SendAsync(normalizedPhone, code, cancellationToken);
        return Results.Ok(SmsCodeResponse.Create(now.AddMinutes(1)));
    }

    private static async Task<IResult> SmsLoginAsync(
        SmsLoginRequest request,
        IdentityDbContext db,
        IPhoneProtector phoneProtector,
        JwtOptions jwtOptions,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var now = TimeProvider.System.GetUtcNow();
        var normalizedPhone = PhoneNumber.Normalize(request.PhoneNumber);
        var phoneHash = PhoneNumber.Hash(normalizedPhone);
        var codeHash = TokenHasher.Hash(request.VerificationCode);
        var code = await db.VerificationCodes
            .Where(item => item.PhoneHash == phoneHash && item.CodeHash == codeHash && item.UsedAtUtc == null && item.ExpiresAtUtc >= now)
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (code is null)
        {
            return ApiError.BadRequest("verification_code_invalid", "验证码无效或已过期。").ToResult(httpContext);
        }

        code.UsedAtUtc = now;
        var user = await db.Users.SingleOrDefaultAsync(item => item.PhoneHash == phoneHash, cancellationToken);
        if (user is null)
        {
            user = UserAccountRecord.Create(phoneHash, phoneProtector.Protect(normalizedPhone), now);
            db.Users.Add(user);
        }

        var tokenPair = IssueTokenPair(user.Id, jwtOptions, now);
        db.RefreshTokenSessions.Add(RefreshTokenSessionRecord.Create(user.Id, tokenPair.RefreshToken, now, TimeSpan.FromDays(jwtOptions.RefreshTokenDays)));
        db.AuditEntries.Add(IdentityAudit.Create("identity.sms_login", user.Id, "ok", "none", now));
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new AuthTokenResponse(tokenPair.AccessToken, tokenPair.RefreshToken, new UserSummary(user.Id)));
    }

    private static async Task<IResult> WechatLoginAsync(
        WechatLoginRequest request,
        IWechatLoginVerifier verifier,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await verifier.VerifyAsync(request.LoginCode, cancellationToken);
        return result.Succeeded
            ? Results.Ok(new { result.OpenId, result.UnionId })
            : ApiError.BadRequest("wechat_login_failed", "微信登录失败，请改用手机号登录。").ToResult(httpContext);
    }

    private static async Task<IResult> RefreshTokenAsync(
        RefreshTokenRequest request,
        IdentityDbContext db,
        JwtOptions jwtOptions,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var now = TimeProvider.System.GetUtcNow();
        var session = await db.RefreshTokenSessions
            .SingleOrDefaultAsync(item => item.TokenHash == TokenHasher.Hash(request.RefreshToken), cancellationToken);
        if (session is null || session.RevokedAtUtc is not null || session.ExpiresAtUtc < now)
        {
            return ApiError.Unauthorized("refresh_token_invalid", "登录状态已失效，请重新登录。").ToResult(httpContext);
        }

        var tokenPair = IssueTokenPair(session.UserId, jwtOptions, now);
        var replacement = RefreshTokenSessionRecord.Create(session.UserId, tokenPair.RefreshToken, now, TimeSpan.FromDays(jwtOptions.RefreshTokenDays));
        session.RevokedAtUtc = now;
        session.ReplacedBySessionId = replacement.Id;
        db.RefreshTokenSessions.Add(replacement);
        db.AuditEntries.Add(IdentityAudit.Create("identity.token_refresh", session.UserId, "ok", "none", now));
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new AuthTokenResponse(tokenPair.AccessToken, tokenPair.RefreshToken, new UserSummary(session.UserId)));
    }

    private static async Task<IResult> LogoutAsync(
        RefreshTokenRequest request,
        IdentityDbContext db,
        CancellationToken cancellationToken)
    {
        var now = TimeProvider.System.GetUtcNow();
        var session = await db.RefreshTokenSessions.SingleOrDefaultAsync(item => item.TokenHash == TokenHasher.Hash(request.RefreshToken), cancellationToken);
        if (session is not null && session.RevokedAtUtc is null)
        {
            session.RevokedAtUtc = now;
            db.AuditEntries.Add(IdentityAudit.Create("identity.logout", session.UserId, "ok", "none", now));
            await db.SaveChangesAsync(cancellationToken);
        }

        return Results.Ok(new { loggedOut = true });
    }

    private static IResult MeAsync(ClaimsPrincipal user)
    {
        return Results.Ok(new { userId = user.FindFirstValue(ClaimTypes.NameIdentifier) });
    }

    private static (string AccessToken, string RefreshToken) IssueTokenPair(Guid userId, JwtOptions options, DateTimeOffset now)
    {
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            options.Issuer,
            options.Audience,
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
            now.UtcDateTime,
            now.AddMinutes(options.AccessTokenMinutes).UtcDateTime,
            credentials);
        return (new JwtSecurityTokenHandler().WriteToken(token), Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
    }
}
