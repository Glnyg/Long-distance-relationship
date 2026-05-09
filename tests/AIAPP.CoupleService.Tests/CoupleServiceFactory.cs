using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using AIAPP.CoupleService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AIAPP.CoupleService.Tests;

public sealed class CoupleServiceFactory : WebApplicationFactory<Program>
{
    internal const string Issuer = "AIAPP.Tests";
    internal const string Audience = "AIAPP.Android.Tests";
    internal const string SigningKey = "test-signing-key-for-couple-service-32bytes";

    private readonly string _databaseName = $"couple-tests-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration(configuration =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = Issuer,
                ["Jwt:Audience"] = Audience,
                ["Jwt:SigningKey"] = SigningKey,
                ["ConnectionStrings:CoupleDb"] = "Host=localhost;Database=aiapp_couple_tests;Username=aiapp;Password=CHANGE_ME"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<CoupleDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<CoupleDbContext>>();
            services.AddDbContext<CoupleDbContext>(options => options.UseInMemoryDatabase(_databaseName));
        });
    }

    public HttpClient CreateAuthenticatedClient(Guid userId)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateJwt(userId));
        return client;
    }

    private static string CreateJwt(Guid userId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            Subject = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString())
            ]),
            Expires = DateTime.UtcNow.AddMinutes(30),
            SigningCredentials = credentials
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
