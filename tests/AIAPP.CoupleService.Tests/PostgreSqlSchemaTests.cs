using AIAPP.CoupleService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace AIAPP.CoupleService.Tests;

public sealed class PostgreSqlSchemaTests
{
    [Fact]
    public async Task CoupleDbContext_CreatesCoupleSchema_WhenDockerIsAvailable()
    {
        if (!await DockerAvailability.IsAvailableAsync())
        {
            // 本地没有 Docker 时不把环境问题当成业务失败；CI 有 Docker 时会真正跑 PostgreSQL schema 检查。
            return;
        }

        await using var container = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("aiapp_couple")
            .WithUsername("aiapp")
            .WithPassword("aiapp")
            .Build();

        await container.StartAsync();

        var options = new DbContextOptionsBuilder<CoupleDbContext>()
            .UseNpgsql(container.GetConnectionString())
            .Options;

        await using var db = new CoupleDbContext(options);
        await db.Database.EnsureCreatedAsync();

        await using var connection = new NpgsqlConnection(container.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "select count(*) from information_schema.tables where table_schema = 'couple' and table_name in ('invitations', 'couples', 'audit_entries')",
            connection);

        var tableCount = (long)(await command.ExecuteScalarAsync() ?? 0L);
        Assert.Equal(3L, tableCount);
    }
}
