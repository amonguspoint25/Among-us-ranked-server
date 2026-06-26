using AmongUsRanked.Core.Elo;

namespace AmongUsRanked.Core.Domain;

public class Match
{
    public string Id { get; set; } = "";          // == MatchReport.MatchId (PK, idempotency)
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset EndedAt { get; set; }
    public string Map { get; set; } = "";
    public int ImpostorCount { get; set; }
    public string SettingsHash { get; set; } = "";
    public Team WinningTeam { get; set; }
    public string GameVersion { get; set; } = "";
    public List<MatchPlayer> Players { get; set; } = new();
}
