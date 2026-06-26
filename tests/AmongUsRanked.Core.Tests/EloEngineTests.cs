using AmongUsRanked.Core.Elo;
using Xunit;

namespace AmongUsRanked.Core.Tests;

public class EloEngineTests
{
    [Fact]
    public void ExpectedImpostorScore_EqualRatings_AndBalancedBaseRate_IsHalf()
    {
        // p0 = 0.5 => bias = 0; equal ratings => expectation 0.5
        var e = EloEngine.ExpectedImpostorScore(1000, 1000, 0.5);
        Assert.Equal(0.5, e, precision: 6);
    }

    [Fact]
    public void ExpectedImpostorScore_LowerBaseRate_PushesExpectationBelowHalf()
    {
        // Impostors win less often at equal ratings when p0 < 0.5
        var e = EloEngine.ExpectedImpostorScore(1000, 1000, 0.30);
        Assert.True(e < 0.5, $"expected < 0.5 but was {e}");
        Assert.Equal(0.30, e, precision: 6); // at equal ratings, expectation collapses to p0
    }

    [Fact]
    public void ExpectedImpostorScore_HigherImpostorRating_IncreasesExpectation()
    {
        var baseline = EloEngine.ExpectedImpostorScore(1000, 1000, 0.30);
        var stronger = EloEngine.ExpectedImpostorScore(1200, 1000, 0.30);
        Assert.True(stronger > baseline);
    }

    [Theory]
    [InlineData(0, 0, true)]    // brand new
    [InlineData(24, 10, true)]  // crew under 25
    [InlineData(30, 4, true)]   // impostor under 5
    [InlineData(25, 5, false)]  // both at threshold => stable
    [InlineData(40, 12, false)] // well past => stable
    public void IsProvisional_FollowsThresholds(int crewGames, int impostorGames, bool expected)
    {
        Assert.Equal(expected, EloEngine.IsProvisional(crewGames, impostorGames));
    }

    [Theory]
    [InlineData(0, 0, 48.0)]
    [InlineData(25, 5, 24.0)]
    public void KFactor_IsProvisionalOrStable(int crewGames, int impostorGames, double expected)
    {
        Assert.Equal(expected, EloEngine.KFactor(crewGames, impostorGames));
    }

    private static EloPlayer P(string code, double crew = 1000, double imp = 1000, int cg = 50, int ig = 20)
        => new(code, crew, imp, cg, ig);

    [Fact]
    public void Rate_ImpostorWin_RaisesImpostorTrack_LowersCrewTrack()
    {
        var input = new EloMatchInput(
            Crew: new[] { P("c1"), P("c2") },
            Impostors: new[] { P("i1") },
            Winner: Team.Impostor,
            ImpostorBaseRate: 0.30);

        var rated = EloEngine.Rate(input);

        var imp = rated.Single(r => r.FriendCode == "i1");
        Assert.Equal(Team.Impostor, imp.Team);
        Assert.True(imp.EloDelta > 0, $"impostor should gain, delta={imp.EloDelta}");
        Assert.Equal(imp.EloBefore + imp.EloDelta, imp.EloAfter, precision: 9);

        foreach (var c in rated.Where(r => r.Team == Team.Crew))
            Assert.True(c.EloDelta < 0, $"crew should lose, delta={c.EloDelta}");
    }

    [Fact]
    public void Rate_OnlyRatedTrackMoves_CrewUsesCrewElo_ImpostorUsesImpostorElo()
    {
        var input = new EloMatchInput(
            Crew: new[] { new EloPlayer("c1", CrewElo: 1100, ImpostorElo: 1, CrewGames: 50, ImpostorGames: 20) },
            Impostors: new[] { new EloPlayer("i1", CrewElo: 1, ImpostorElo: 900, CrewGames: 50, ImpostorGames: 20) },
            Winner: Team.Crew,
            ImpostorBaseRate: 0.30);

        var rated = EloEngine.Rate(input);

        Assert.Equal(1100, rated.Single(r => r.FriendCode == "c1").EloBefore); // crew rated on CrewElo
        Assert.Equal(900, rated.Single(r => r.FriendCode == "i1").EloBefore);  // impostor rated on ImpostorElo
    }

    [Fact]
    public void Rate_ProvisionalPlayer_MovesByLargerK()
    {
        var provisional = new EloMatchInput(
            new[] { new EloPlayer("c1", 1000, 1000, CrewGames: 0, ImpostorGames: 0) },
            new[] { P("i1") }, Team.Crew, 0.30);
        var stable = new EloMatchInput(
            new[] { new EloPlayer("c1", 1000, 1000, CrewGames: 100, ImpostorGames: 50) },
            new[] { P("i1") }, Team.Crew, 0.30);

        var dProv = Math.Abs(EloEngine.Rate(provisional).Single(r => r.FriendCode == "c1").EloDelta);
        var dStable = Math.Abs(EloEngine.Rate(stable).Single(r => r.FriendCode == "c1").EloDelta);

        Assert.True(dProv > dStable, $"provisional delta {dProv} should exceed stable delta {dStable}");
    }

    [Fact]
    public void Rate_EmptyTeam_Throws()
    {
        var input = new EloMatchInput(Array.Empty<EloPlayer>(), new[] { P("i1") }, Team.Crew, 0.30);
        Assert.Throws<ArgumentException>(() => EloEngine.Rate(input));
    }
}
