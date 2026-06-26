using AmongUsRanked.Api.Data;
using AmongUsRanked.Api.Ingest;
using AmongUsRanked.Core.Contracts;
using AmongUsRanked.Core.Elo;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddSingleton<IMatchReportValidator, NullMatchReportValidator>();
builder.Services.AddScoped<MatchIngestService>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok("ok"));

app.MapPost("/api/matches", async (MatchReport report, MatchIngestService svc, CancellationToken ct) =>
{
    if (!IsWellFormed(report, out var error))
        return Results.BadRequest(new { error });

    var result = await svc.IngestAsync(report, ct);
    return result.Status switch
    {
        IngestStatus.Success or IngestStatus.Duplicate => Results.Ok(new { result.Status }),
        IngestStatus.Rejected => Results.UnprocessableEntity(new { error = result.Message }),
        _ => Results.StatusCode(500),
    };
})
.AddEndpointFilter<HostTokenEndpointFilter>();

app.Run();

static bool IsWellFormed(MatchReport r, out string error)
{
    error = "";
    if (r is null) { error = "missing body"; return false; }
    if (string.IsNullOrWhiteSpace(r.MatchId)) { error = "matchId required"; return false; }
    if (r.Players is null || r.Players.Count == 0) { error = "players required"; return false; }
    if (r.Players.All(p => p.Team != Team.Impostor)) { error = "no impostors"; return false; }
    if (r.Players.All(p => p.Team != Team.Crew)) { error = "no crew"; return false; }
    if (r.Players.Any(p => string.IsNullOrWhiteSpace(p.FriendCode))) { error = "player friendCode required"; return false; }
    return true;
}

public partial class Program { } // exposed for WebApplicationFactory in tests
