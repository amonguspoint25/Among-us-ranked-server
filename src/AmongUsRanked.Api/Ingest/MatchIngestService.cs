using AmongUsRanked.Api.Data;
using AmongUsRanked.Core.Contracts;
using AmongUsRanked.Core.Domain;
using AmongUsRanked.Core.Elo;
using Microsoft.EntityFrameworkCore;

namespace AmongUsRanked.Api.Ingest;

public enum IngestStatus { Success, Duplicate, Rejected }

public sealed record IngestResult(IngestStatus Status, string? Message);

public sealed class MatchIngestService
{
    private const int BaseRateWindow = 100;   // rolling matches per settings
    private const int BaseRateMinSample = 10; // below this, use the default p0

    private readonly AppDbContext _db;
    private readonly IMatchReportValidator _validator;

    public MatchIngestService(AppDbContext db, IMatchReportValidator validator)
    {
        _db = db;
        _validator = validator;
    }

    public async Task<double> GetImpostorBaseRateAsync(string settingsHash, CancellationToken ct = default)
    {
        var recent = await _db.Matches
            .Where(m => m.SettingsHash == settingsHash)
            .OrderByDescending(m => m.EndedAt)
            .Take(BaseRateWindow)
            .Select(m => m.WinningTeam)
            .ToListAsync(ct);

        if (recent.Count < BaseRateMinSample)
            return EloEngine.DefaultImpostorBaseRate;

        int impostorWins = recent.Count(t => t == Team.Impostor);
        return (double)impostorWins / recent.Count;
    }

    public async Task<IngestResult> IngestAsync(MatchReport report, CancellationToken ct = default)
    {
        var (ok, reason) = _validator.Validate(report);
        if (!ok)
            return new IngestResult(IngestStatus.Rejected, reason);

        if (await _db.Matches.AnyAsync(m => m.Id == report.MatchId, ct))
            return new IngestResult(IngestStatus.Duplicate, null);

        // Load or create players keyed by friend code.
        var codes = report.Players.Select(p => p.FriendCode).ToList();
        var existing = await _db.Players
            .Where(p => p.FriendCode != null && codes.Contains(p.FriendCode))
            .ToDictionaryAsync(p => p.FriendCode!, ct);

        var players = new Dictionary<string, Player>();
        foreach (var rp in report.Players)
        {
            if (!existing.TryGetValue(rp.FriendCode, out var player))
            {
                player = new Player
                {
                    FriendCode = rp.FriendCode,
                    DisplayName = rp.DisplayName,
                    CreatedAt = report.EndedAt,
                };
                _db.Players.Add(player);
            }
            player.DisplayName = rp.DisplayName; // keep latest seen name
            players[rp.FriendCode] = player;
        }

        // Build the engine input from current ratings.
        double p0 = await GetImpostorBaseRateAsync(report.SettingsHash, ct);
        EloPlayer ToEloPlayer(MatchReportPlayer rp)
        {
            var pl = players[rp.FriendCode];
            return new EloPlayer(rp.FriendCode, pl.CrewElo, pl.ImpostorElo, pl.CrewGames, pl.ImpostorGames);
        }
        var input = new EloMatchInput(
            Crew: report.Players.Where(p => p.Team == Team.Crew).Select(ToEloPlayer).ToList(),
            Impostors: report.Players.Where(p => p.Team == Team.Impostor).Select(ToEloPlayer).ToList(),
            Winner: report.Winner,
            ImpostorBaseRate: p0);

        var rated = EloEngine.Rate(input).ToDictionary(r => r.FriendCode);

        // Build the Match aggregate.
        var match = new Match
        {
            Id = report.MatchId,
            StartedAt = report.StartedAt,
            EndedAt = report.EndedAt,
            Map = report.Map,
            ImpostorCount = report.ImpostorCount,
            SettingsHash = report.SettingsHash,
            WinningTeam = report.Winner,
            GameVersion = report.GameVersion,
        };

        foreach (var rp in report.Players)
        {
            var pl = players[rp.FriendCode];
            var r = rated[rp.FriendCode];
            bool won = rp.Team == report.Winner;

            // Apply the rated track back to the player.
            if (rp.Team == Team.Crew) { pl.CrewElo = r.EloAfter; pl.CrewGames++; }
            else { pl.ImpostorElo = r.EloAfter; pl.ImpostorGames++; }
            pl.CombinedElo = (pl.CrewElo + pl.ImpostorElo) / 2.0;

            // Lifetime display stats (never feed ELO).
            pl.Kills += rp.Kills;
            pl.CorrectVotes += rp.CorrectVotes;
            pl.IncorrectVotes += rp.IncorrectVotes;
            pl.TasksCompleted += rp.TasksCompleted;
            if (won) pl.Wins++; else pl.Losses++;
            pl.UpdatedAt = report.EndedAt;

            match.Players.Add(new MatchPlayer
            {
                Match = match,
                Player = pl,
                Team = rp.Team,
                Survived = rp.Survived,
                Kills = rp.Kills,
                CorrectVotes = rp.CorrectVotes,
                IncorrectVotes = rp.IncorrectVotes,
                TasksCompleted = rp.TasksCompleted,
                EloBefore = r.EloBefore,
                EloAfter = r.EloAfter,
                EloDelta = r.EloDelta,
            });
        }

        _db.Matches.Add(match);

        // Single SaveChanges == one transaction (atomic). A racing duplicate trips
        // the Match PK and surfaces as DbUpdateException -> treat as Duplicate.
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return new IngestResult(IngestStatus.Duplicate, null);
        }

        return new IngestResult(IngestStatus.Success, null);
    }
}
