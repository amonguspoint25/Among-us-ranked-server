using AmongUsRanked.Core.Contracts;

namespace AmongUsRanked.Api.Ingest;

/// <summary>Anti-cheat seam. Default is a no-op; real cross-checking plugs in here later.</summary>
public interface IMatchReportValidator
{
    (bool ok, string? reason) Validate(MatchReport report);
}

public sealed class NullMatchReportValidator : IMatchReportValidator
{
    public (bool ok, string? reason) Validate(MatchReport report) => (true, null);
}
