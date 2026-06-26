using AmongUsRanked.Core.Elo;

namespace AmongUsRanked.Core.Domain;

public class MatchPlayer
{
    public int Id { get; set; }
    public string MatchId { get; set; } = "";
    public Match Match { get; set; } = null!;
    public int PlayerId { get; set; }
    public Player Player { get; set; } = null!;

    public Team Team { get; set; }
    public bool Survived { get; set; }

    public int Kills { get; set; }
    public int CorrectVotes { get; set; }
    public int IncorrectVotes { get; set; }
    public int TasksCompleted { get; set; }

    public double EloBefore { get; set; }          // rated track (Crew or Impostor)
    public double EloAfter { get; set; }
    public double EloDelta { get; set; }
}
