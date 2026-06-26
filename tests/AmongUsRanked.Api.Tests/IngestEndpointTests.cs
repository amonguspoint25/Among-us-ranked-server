using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using AmongUsRanked.Api.Data;
using AmongUsRanked.Core.Contracts;
using AmongUsRanked.Core.Elo;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AmongUsRanked.Api.Tests;

public class IngestEndpointTests : IClassFixture<IngestEndpointTests.Factory>
{
    private readonly Factory _factory;
    public IngestEndpointTests(Factory factory) => _factory = factory;

    private const string Token = "test-token";

    private static MatchReport Sample(string id) => new(
        id, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(8),
        "Skeld", 1, "skeld-1imp", "v17", Team.Impostor,
        new MatchReportPlayer[]
        {
            new("i1#0001", "Red",  Team.Impostor, true,  2, 0, 1, 0),
            new("c1#0002", "Blue", Team.Crew,     false, 0, 1, 0, 3),
        });

    [Fact]
    public async Task Post_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/matches", Sample("e1"));
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Post_ValidReport_Returns200_AndPersists()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Host-Token", Token);

        var resp = await client.PostAsJsonAsync("/api/matches", Sample("e2"));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.Matches.AnyAsync(m => m.Id == "e2"));
    }

    [Fact]
    public async Task Post_DuplicateMatch_Returns200_NoOp()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Host-Token", Token);
        await client.PostAsJsonAsync("/api/matches", Sample("e3"));
        var resp = await client.PostAsJsonAsync("/api/matches", Sample("e3"));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Post_EmptyMatchId_Returns400()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Host-Token", Token);
        var resp = await client.PostAsJsonAsync("/api/matches", Sample(""));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Post_NoImpostors_Returns400()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Host-Token", Token);
        var bad = Sample("e4") with
        {
            Players = new MatchReportPlayer[] { new("c1#0002", "Blue", Team.Crew, false, 0, 1, 0, 3) }
        };
        var resp = await client.PostAsJsonAsync("/api/matches", bad);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    public class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development"); // surface exception detail in 500 bodies
            builder.UseSetting("Ingest:HostToken", Token);
            builder.ConfigureServices(services =>
            {
                // Remove the production Npgsql provider registration ENTIRELY before
                // adding InMemory. EF Core 9+ keeps the provider in
                // IDbContextOptionsConfiguration<T>, so removing only
                // DbContextOptions<T> leaves Npgsql registered and trips the
                // "only a single database provider" error on first DB use.
                var toRemove = services.Where(d =>
                    d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                    d.ServiceType == typeof(DbContextOptions) ||
                    d.ServiceType == typeof(AppDbContext) ||
                    (d.ServiceType.FullName?.Contains("IDbContextOptionsConfiguration") ?? false)
                ).ToList();
                foreach (var d in toRemove) services.Remove(d);

                services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase("endpoint-tests"));
            });
        }
    }
}
