using AmongUsRanked.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace AmongUsRanked.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Player> Players => Set<Player>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<MatchPlayer> MatchPlayers => Set<MatchPlayer>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Player>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasIndex(p => p.FriendCode).IsUnique();   // friend code = identity
            e.Property(p => p.DisplayName).HasMaxLength(64);
        });

        b.Entity<Match>(e =>
        {
            e.HasKey(m => m.Id);                          // string PK == matchId (idempotency)
            e.Property(m => m.Id).HasMaxLength(128);
            e.Property(m => m.SettingsHash).HasMaxLength(128);
            e.HasIndex(m => m.SettingsHash);              // base-rate lookups
            e.HasMany(m => m.Players).WithOne(mp => mp.Match).HasForeignKey(mp => mp.MatchId);
        });

        b.Entity<MatchPlayer>(e =>
        {
            e.HasKey(mp => mp.Id);
            e.HasOne(mp => mp.Player).WithMany().HasForeignKey(mp => mp.PlayerId);
            e.HasIndex(mp => new { mp.MatchId, mp.PlayerId }).IsUnique();
        });
    }
}
