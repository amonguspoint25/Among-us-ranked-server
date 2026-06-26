using AmongUsRanked.Api.Data;
using AmongUsRanked.Api.Ingest;
using AmongUsRanked.Core.Contracts;
using AmongUsRanked.Core.Domain;
using AmongUsRanked.Core.Elo;
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

    private static MatchReport SampleReport(string matchId, Team winner)
        => new(
            MatchId: matchId,
            StartedAt: DateTimeOffset.UnixEpoch,
            EndedAt: DateTimeOffset.UnixEpoch.AddMinutes(8),
            Map: "Skeld",
            ImpostorCount: 1,
            SettingsHash: "skeld-1imp",
            GameVersion: "v17",
            Winner: winner,
            Players: new MatchReportPlayer[]
            {
                new("i1#0001", "Red",   Team.Impostor, Survived: true,  Kills: 2, CorrectVotes: 0, IncorrectVotes: 1, TasksCompleted: 0),
                new("c1#0002", "Blue",  Team.Crew,     Survived: false, Kills: 0, CorrectVotes: 1, IncorrectVotes: 0, TasksCompleted: 3),
                new("c2#0003", "Green", Team.Crew,     Survived: true,  Kills: 0, CorrectVotes: 0, IncorrectVotes: 1, TasksCompleted: 5),
            });

    private static MatchIngestService NewService(AppDbContext db)
        => new(db, new NullMatchReportValidator());

    [Fact]
    public async Task Ingest_NewMatch_CreatesPlayers_UpdatesRatings_AndStats()
    {
        await using var db = NewDb();
        var result = await NewService(db).IngestAsync(SampleReport("m1", Team.Impostor));

        Assert.Equal(IngestStatus.Success, result.Status);
        Assert.Equal(3, await db.Players.CountAsync());
        Assert.Equal(1, await db.Matches.CountAsync());
        Assert.Equal(3, await db.MatchPlayers.CountAsync());

        var imp = await db.Players.SingleAsync(p => p.FriendCode == "i1#0001");
        Assert.True(imp.ImpostorElo > 1000, "winning impostor gains Impostor-Elo");
        Assert.Equal(1000, imp.CrewElo);                 // untouched track
        Assert.Equal(1, imp.ImpostorGames);
        Assert.Equal(0, imp.CrewGames);
        Assert.Equal(1, imp.Wins);
        Assert.Equal(2, imp.Kills);                      // lifetime stat recorded
        Assert.Equal((imp.CrewElo + imp.ImpostorElo) / 2, imp.CombinedElo, precision: 9);

        var crew = await db.Players.SingleAsync(p => p.FriendCode == "c1#0002");
        Assert.True(crew.CrewElo < 1000, "losing crew loses Crew-Elo");
        Assert.Equal(1, crew.Losses);
        Assert.Equal(3, crew.TasksCompleted);
    }

    [Fact]
    public async Task Ingest_SecondMatch_UsesUpdatedRatingsAndIncrementsGames()
    {
        await using var db = NewDb();
        var svc = NewService(db);
        await svc.IngestAsync(SampleReport("m1", Team.Impostor));
        await svc.IngestAsync(SampleReport("m2", Team.Crew));

        var crew = await db.Players.SingleAsync(p => p.FriendCode == "c1#0002");
        Assert.Equal(2, crew.CrewGames);                 // two crew games
        Assert.Equal(1, crew.Wins);                      // won the second
        Assert.Equal(1, crew.Losses);                    // lost the first
    }

    [Fact]
    public async Task Ingest_DuplicateMatchId_IsNoOp()
    {
        await using var db = NewDb();
        var svc = NewService(db);
        await svc.IngestAsync(SampleReport("m1", Team.Impostor));
        var impAfterFirst = (await db.Players.SingleAsync(p => p.FriendCode == "i1#0001")).ImpostorElo;

        var second = await svc.IngestAsync(SampleReport("m1", Team.Impostor));

        Assert.Equal(IngestStatus.Duplicate, second.Status);
        Assert.Equal(1, await db.Matches.CountAsync());
        Assert.Equal(impAfterFirst, (await db.Players.SingleAsync(p => p.FriendCode == "i1#0001")).ImpostorElo);
    }

    [Fact]
    public async Task GetImpostorBaseRate_FewMatches_ReturnsDefault()
    {
        await using var db = NewDb();
        var rate = await NewService(db).GetImpostorBaseRateAsync("skeld-1imp");
        Assert.Equal(EloEngine.DefaultImpostorBaseRate, rate);
    }

    [Fact]
    public async Task Ingest_RejectedByValidator_PersistsNothing()
    {
        await using var db = NewDb();
        var svc = new MatchIngestService(db, new RejectAllValidator());
        var result = await svc.IngestAsync(SampleReport("m1", Team.Impostor));

        Assert.Equal(IngestStatus.Rejected, result.Status);
        Assert.Equal(0, await db.Matches.CountAsync());
        Assert.Equal(0, await db.Players.CountAsync());
    }

    private sealed class RejectAllValidator : IMatchReportValidator
    {
        public (bool ok, string? reason) Validate(MatchReport report) => (false, "nope");
    }
}
