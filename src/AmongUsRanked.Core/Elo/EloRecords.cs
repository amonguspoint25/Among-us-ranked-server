namespace AmongUsRanked.Core.Elo;

/// <summary>A player's current ratings + game counts entering a match.</summary>
public sealed record EloPlayer(
    string FriendCode,
    double CrewElo,
    double ImpostorElo,
    int CrewGames,
    int ImpostorGames);

/// <summary>The result of rating a match for one player (the rated track only).</summary>
public sealed record RatedPlayer(
    string FriendCode,
    Team Team,
    double EloBefore,
    double EloAfter,
    double EloDelta);

/// <summary>One match's inputs: the two teams, who won, and the impostor base-rate.</summary>
public sealed record EloMatchInput(
    IReadOnlyList<EloPlayer> Crew,
    IReadOnlyList<EloPlayer> Impostors,
    Team Winner,
    double ImpostorBaseRate);
