using AmongUsRanked.Api.Data;
using AmongUsRanked.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AmongUsRanked.Api.Tests;

public class MatchIngestServiceTests
{
    private static AppDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"db-{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task DbContext_CanPersistAndReadPlayer()
    {
        await using var db = NewDb();
        db.Players.Add(new Player { FriendCode = "abc#1234", DisplayName = "Red" });
        await db.SaveChangesAsync();

        var loaded = await db.Players.SingleAsync(p => p.FriendCode == "abc#1234");
        Assert.Equal("Red", loaded.DisplayName);
        Assert.Equal(1000, loaded.CrewElo);      // default start rating
        Assert.Equal(1000, loaded.ImpostorElo);
    }
}
