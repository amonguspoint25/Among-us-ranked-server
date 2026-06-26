using AmongUsRanked.Core.Elo;

namespace AmongUsRanked.Core.Contracts;

/// <summary>The authoritative end-of-game report the Ranked Host mod POSTs.</summary>
public sealed record MatchReport(
    string MatchId,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    string Map,
    int ImpostorCount,
    string SettingsHash,
    string GameVersion,
    Team Winner,
    IReadOnlyList<MatchReportPlayer> Players);

public sealed record MatchReportPlayer(
    string FriendCode,
    string DisplayName,
    Team Team,
    bool Survived,
    int Kills,
    int CorrectVotes,
    int IncorrectVotes,
    int TasksCompleted);
