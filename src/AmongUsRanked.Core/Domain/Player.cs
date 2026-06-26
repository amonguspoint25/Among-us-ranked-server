using AmongUsRanked.Core.Elo;

namespace AmongUsRanked.Core.Domain;

public class Player
{
    public int Id { get; set; }
    public string? FriendCode { get; set; }       // primary cross-game identity (nullable until linked)
    public string DisplayName { get; set; } = "";

    public double CrewElo { get; set; } = EloEngine.StartRating;
    public double ImpostorElo { get; set; } = EloEngine.StartRating;
    public double CombinedElo { get; set; } = EloEngine.StartRating;

    public int CrewGames { get; set; }
    public int ImpostorGames { get; set; }

    // Lifetime display stats (never feed ELO).
    public int Kills { get; set; }
    public int CorrectVotes { get; set; }
    public int IncorrectVotes { get; set; }
    public int TasksCompleted { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
