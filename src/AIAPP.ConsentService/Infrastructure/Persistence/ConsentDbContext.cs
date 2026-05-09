using System.Text.Json;
using AIAPP.ConsentService.Domain;
using AIAPP.Observability;
using Microsoft.EntityFrameworkCore;

namespace AIAPP.ConsentService.Infrastructure.Persistence;

/// <summary>
/// ConsentService 独占 consent schema。授权版本是敏感写入和 AI 分析的前置条件，误改会造成越权共享。
/// </summary>
public sealed class ConsentDbContext(DbContextOptions<ConsentDbContext> options) : DbContext(options)
{
    public DbSet<ConsentGrantRecord> ConsentGrants => Set<ConsentGrantRecord>();

    public DbSet<ConsentAuditEntryRecord> AuditEntries => Set<ConsentAuditEntryRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("consent");
        modelBuilder.Entity<ConsentGrantRecord>().ToTable("consent_grants")
            .HasIndex(item => new { item.CoupleId, item.UserId, item.DataType })
            .IsUnique();
        modelBuilder.Entity<ConsentAuditEntryRecord>().ToTable("local_audit_entries");
    }
}

public sealed class ConsentGrantRecord
{
    public Guid Id { get; set; }

    public Guid CoupleId { get; set; }

    public Guid UserId { get; set; }

    public string DataType { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public string ConsentVersion { get; set; } = string.Empty;

    public DateTimeOffset ChangedAtUtc { get; set; }

    public static ConsentGrantRecord Create(Guid coupleId, Guid userId, ConsentDataType dataType, bool active, DateTimeOffset now)
    {
        return new ConsentGrantRecord
        {
            Id = Guid.NewGuid(),
            CoupleId = coupleId,
            UserId = userId,
            DataType = dataType.Value,
            IsActive = active,
            ConsentVersion = NewVersion(),
            ChangedAtUtc = now
        };
    }

    public void Change(bool active, DateTimeOffset now)
    {
        IsActive = active;
        ConsentVersion = NewVersion();
        ChangedAtUtc = now;
    }

    private static string NewVersion() => $"consent-{Guid.NewGuid():N}";
}

public sealed record ConsentBody(string DataType, bool IsActive, string ConsentVersion, DateTimeOffset? ChangedAtUtc)
{
    public static ConsentBody From(ConsentDataType type, ConsentGrantRecord? grant)
    {
        return new ConsentBody(type.Value, grant?.IsActive ?? false, grant?.ConsentVersion ?? string.Empty, grant?.ChangedAtUtc);
    }
}

public sealed class ConsentAuditEntryRecord
{
    public Guid Id { get; set; }

    public string Operation { get; set; } = string.Empty;

    public string UserIdHash { get; set; } = string.Empty;

    public string CoupleIdHash { get; set; } = string.Empty;

    public string DataType { get; set; } = string.Empty;

    public string ConsentVersion { get; set; } = string.Empty;

    public string Result { get; set; } = string.Empty;

    public string ErrorCode { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string MetadataJson { get; set; } = "{}";

    public static ConsentAuditEntryRecord FromGrant(string operation, ConsentGrantRecord grant, string result, string errorCode, DateTimeOffset now)
    {
        var metadata = new
        {
            operation,
            user_id_hash = TelemetrySanitizer.HashIdentifier(grant.UserId),
            couple_id_hash = TelemetrySanitizer.HashIdentifier(grant.CoupleId),
            data_type = grant.DataType,
            consent_version = grant.ConsentVersion,
            result,
            error_code = errorCode
        };

        return new ConsentAuditEntryRecord
        {
            Id = Guid.NewGuid(),
            Operation = operation,
            UserIdHash = metadata.user_id_hash,
            CoupleIdHash = metadata.couple_id_hash,
            DataType = grant.DataType,
            ConsentVersion = grant.ConsentVersion,
            Result = result,
            ErrorCode = errorCode,
            CreatedAtUtc = now,
            MetadataJson = JsonSerializer.Serialize(metadata)
        };
    }
}
