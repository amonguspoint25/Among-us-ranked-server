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
}
