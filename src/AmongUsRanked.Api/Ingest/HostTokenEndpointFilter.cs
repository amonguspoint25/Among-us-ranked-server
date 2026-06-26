namespace AmongUsRanked.Api.Ingest;

/// <summary>Rejects ingest calls lacking a valid X-Host-Token header.</summary>
public sealed class HostTokenEndpointFilter : IEndpointFilter
{
    private readonly string _expected;
    public HostTokenEndpointFilter(IConfiguration config)
        => _expected = config["Ingest:HostToken"] ?? "";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        var provided = ctx.HttpContext.Request.Headers["X-Host-Token"].ToString();
        if (string.IsNullOrEmpty(_expected) || provided != _expected)
            return Results.Unauthorized();
        return await next(ctx);
    }
}
