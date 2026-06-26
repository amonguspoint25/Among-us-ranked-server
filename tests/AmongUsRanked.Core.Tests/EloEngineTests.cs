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
}
