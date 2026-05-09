using System.Text.Json;
using AIAPP.IdentityService;
using AIAPP.Observability;
using Microsoft.EntityFrameworkCore;

namespace AIAPP.IdentityService.Infrastructure.Persistence;

/// <summary>
/// IdentityService 独占 identity schema。其他服务不能直接读写这些表，避免账号和 token 边界被绕过。
/// </summary>
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public DbSet<UserAccountRecord> Users => Set<UserAccountRecord>();

    public DbSet<VerificationCodeRecord> VerificationCodes => Set<VerificationCodeRecord>();

    public DbSet<RefreshTokenSessionRecord> RefreshTokenSessions => Set<RefreshTokenSessionRecord>();

    public DbSet<IdentityAuditEntryRecord> AuditEntries => Set<IdentityAuditEntryRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("identity");
        modelBuilder.Entity<UserAccountRecord>().ToTable("users").HasIndex(item => item.PhoneHash).IsUnique();
        modelBuilder.Entity<VerificationCodeRecord>().ToTable("verification_codes");
        modelBuilder.Entity<RefreshTokenSessionRecord>().ToTable("refresh_token_sessions").HasIndex(item => item.TokenHash).IsUnique();
        modelBuilder.Entity<IdentityAuditEntryRecord>().ToTable("audit_entries");
    }
}

public sealed class UserAccountRecord
{
    public Guid Id { get; set; }

    public string PhoneHash { get; set; } = string.Empty;

    public string ProtectedPhone { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public static UserAccountRecord Create(string phoneHash, string protectedPhone, DateTimeOffset now)
    {
        return new UserAccountRecord
        {
            Id = Guid.NewGuid(),
            PhoneHash = phoneHash,
            ProtectedPhone = protectedPhone,
            CreatedAtUtc = now
        };
    }
}

public sealed class VerificationCodeRecord
{
    public Guid Id { get; set; }

    public string PhoneHash { get; set; } = string.Empty;

    public string CodeHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public DateTimeOffset? UsedAtUtc { get; set; }

    public static VerificationCodeRecord Create(string normalizedPhone, string code, DateTimeOffset now)
    {
        return new VerificationCodeRecord
        {
            Id = Guid.NewGuid(),
            PhoneHash = PhoneNumber.Hash(normalizedPhone),
            CodeHash = TokenHasher.Hash(code),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(5)
        };
    }
}

public sealed class RefreshTokenSessionRecord
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public DateTimeOffset? RevokedAtUtc { get; set; }

    public Guid? ReplacedBySessionId { get; set; }

    public static RefreshTokenSessionRecord Create(Guid userId, string token, DateTimeOffset now, TimeSpan lifetime)
    {
        return new RefreshTokenSessionRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = TokenHasher.Hash(token),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.Add(lifetime)
        };
    }
}

public sealed class IdentityAuditEntryRecord
{
    public Guid Id { get; set; }

    public string Operation { get; set; } = string.Empty;

    public string? UserIdHash { get; set; }

    public string Result { get; set; } = string.Empty;

    public string ErrorCode { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string MetadataJson { get; set; } = "{}";
}

public static class IdentityAudit
{
    public static IdentityAuditEntryRecord Create(string operation, Guid? userId, string result, string errorCode, DateTimeOffset now)
    {
        var userIdHash = userId.HasValue ? TelemetrySanitizer.HashIdentifier(userId.Value) : null;
        return new IdentityAuditEntryRecord
        {
            Id = Guid.NewGuid(),
            Operation = operation,
            UserIdHash = userIdHash,
            Result = result,
            ErrorCode = errorCode,
            CreatedAtUtc = now,
            MetadataJson = JsonSerializer.Serialize(new
            {
                operation,
                user_id_hash = userIdHash,
                result,
                error_code = errorCode
            })
        };
    }
}
