using AmongUsRanked.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

var app = builder.Build();

app.MapGet("/health", () => Results.Ok("ok"));

app.Run();

public partial class Program { } // exposed for WebApplicationFactory in tests
