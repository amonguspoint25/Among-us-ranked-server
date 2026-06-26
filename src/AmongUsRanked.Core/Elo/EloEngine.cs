namespace AmongUsRanked.Core.Elo;

/// <summary>Pure, dependency-free dual-Elo rating engine. No I/O.</summary>
public static class EloEngine
{
    public const double StartRating = 1000.0;
    public const double ProvisionalK = 48.0;
    public const double StableK = 24.0;
    public const int CrewStableThreshold = 25;
    public const int ImpostorStableThreshold = 5;
    public const double DefaultImpostorBaseRate = 0.30;

    /// <summary>
    /// Probability the impostor team wins, given average ratings and the empirical
    /// impostor base-rate p0 (which shifts the logistic so equal-rated teams predict
    /// the real base-rate, not 50/50).
    /// </summary>
    public static double ExpectedImpostorScore(double avgImpostorElo, double avgCrewElo, double impostorBaseRate)
    {
        double bias = 400.0 * Math.Log10(impostorBaseRate / (1.0 - impostorBaseRate));
        double diff = (avgImpostorElo - avgCrewElo) + bias;
        return 1.0 / (1.0 + Math.Pow(10.0, -diff / 400.0));
    }

    public static bool IsProvisional(int crewGames, int impostorGames)
        => crewGames < CrewStableThreshold || impostorGames < ImpostorStableThreshold;

    public static double KFactor(int crewGames, int impostorGames)
        => IsProvisional(crewGames, impostorGames) ? ProvisionalK : StableK;

    /// <summary>
    /// Rate a finished match. Outcome-driven: only the winner matters. Each crew
    /// member's Crew track and each impostor's Impostor track is updated by its own
    /// K-factor toward the actual result.
    /// </summary>
    public static IReadOnlyList<RatedPlayer> Rate(EloMatchInput input)
    {
        if (input.Crew.Count == 0 || input.Impostors.Count == 0)
            throw new ArgumentException("Both teams must have at least one player.", nameof(input));

        double avgImp = input.Impostors.Average(p => p.ImpostorElo);
        double avgCrew = input.Crew.Average(p => p.CrewElo);
        double eImp = ExpectedImpostorScore(avgImp, avgCrew, input.ImpostorBaseRate);
        double eCrew = 1.0 - eImp;
        double sImp = input.Winner == Team.Impostor ? 1.0 : 0.0;
        double sCrew = 1.0 - sImp;

        var results = new List<RatedPlayer>(input.Crew.Count + input.Impostors.Count);

        foreach (var p in input.Impostors)
        {
            double k = KFactor(p.CrewGames, p.ImpostorGames);
            double after = p.ImpostorElo + k * (sImp - eImp);
            results.Add(new RatedPlayer(p.FriendCode, Team.Impostor, p.ImpostorElo, after, after - p.ImpostorElo));
        }

        foreach (var p in input.Crew)
        {
            double k = KFactor(p.CrewGames, p.ImpostorGames);
            double after = p.CrewElo + k * (sCrew - eCrew);
            results.Add(new RatedPlayer(p.FriendCode, Team.Crew, p.CrewElo, after, after - p.CrewElo));
        }

        return results;
    }
}
