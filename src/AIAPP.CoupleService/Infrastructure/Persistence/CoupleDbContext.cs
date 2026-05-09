using AIAPP.CoupleService;
using AIAPP.Observability;
using Microsoft.EntityFrameworkCore;

namespace AIAPP.CoupleService.Infrastructure.Persistence;

/// <summary>
/// CoupleService 独占 couple schema。绑定关系是情侣数据访问的基础，其他服务只能通过接口查询，不能直接改表。
/// </summary>
public sealed class CoupleDbContext(DbContextOptions<CoupleDbContext> options) : DbContext(options)
{
    public DbSet<CoupleInvitationRecord> Invitations => Set<CoupleInvitationRecord>();

    public DbSet<CoupleRecord> Couples => Set<CoupleRecord>();

    public DbSet<CoupleAuditEntryRecord> AuditEntries => Set<CoupleAuditEntryRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("couple");
        modelBuilder.Entity<CoupleInvitationRecord>().ToTable("invitations").HasIndex(item => item.Code).IsUnique();
        modelBuilder.Entity<CoupleRecord>().ToTable("couples").HasKey(item => item.CoupleId);
        modelBuilder.Entity<CoupleAuditEntryRecord>().ToTable("audit_entries");
    }
}

public sealed class CoupleInvitationRecord
{
    public Guid Id { get; set; }

    public Guid InviterUserId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string QrPayload { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public DateTimeOffset? AcceptedAtUtc { get; set; }

    public static CoupleInvitationRecord Create(Guid inviterUserId, DateTimeOffset now)
    {
        var code = CoupleInvitation.GenerateCode();
        return new CoupleInvitationRecord
        {
            Id = Guid.NewGuid(),
            InviterUserId = inviterUserId,
            Code = code,
            QrPayload = $"aiapp://couples/invitations/{code}",
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(15)
        };
    }
}

public sealed class CoupleRecord
{
    public Guid CoupleId { get; set; }

    public Guid UserAId { get; set; }

    public Guid UserBId { get; set; }

    public string Status { get; set; } = CoupleStatus.Active;

    public DateTimeOffset BoundAtUtc { get; set; }

    public DateTimeOffset? UnboundAtUtc { get; set; }

    public static CoupleRecord Create(Guid inviterUserId, Guid accepterUserId, DateTimeOffset now)
    {
        return new CoupleRecord
        {
            CoupleId = Guid.NewGuid(),
            UserAId = inviterUserId,
            UserBId = accepterUserId,
            Status = CoupleStatus.Active,
            BoundAtUtc = now
        };
    }

    public void Unbind(DateTimeOffset now)
    {
        Status = CoupleStatus.Inactive;
        UnboundAtUtc = now;
    }
}

public sealed class CoupleAuditEntryRecord
{
    public Guid Id { get; set; }

    public string Operation { get; set; } = string.Empty;

    public string UserIdHash { get; set; } = string.Empty;

    public string? CoupleIdHash { get; set; }

    public string Result { get; set; } = string.Empty;

    public string ErrorCode { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public static CoupleAuditEntryRecord Create(string operation, Guid userId, Guid? coupleId, string result, string errorCode, DateTimeOffset now)
    {
        return new CoupleAuditEntryRecord
        {
            Id = Guid.NewGuid(),
            Operation = operation,
            UserIdHash = TelemetrySanitizer.HashIdentifier(userId),
            CoupleIdHash = coupleId.HasValue ? TelemetrySanitizer.HashIdentifier(coupleId.Value) : null,
            Result = result,
            ErrorCode = errorCode,
            CreatedAtUtc = now
        };
    }
}
