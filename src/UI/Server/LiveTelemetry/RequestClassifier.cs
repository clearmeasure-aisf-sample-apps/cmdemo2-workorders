namespace ClearMeasure.Bootcamp.UI.Server.LiveTelemetry;

/// <summary>
/// Classifies an incoming request as Front Door health probe, diagnostic probe or traffic (through Front Door or direct).
/// </summary>
public static class RequestClassifier
{
    /// <summary>Header Azure Front Door adds to its health probes to an origin.</summary>
    public const string FrontDoorHealthProbeHeader = "X-FD-HealthProbe";

    /// <summary>Header Azure Front Door adds to every request it forwards (the profile's Front Door ID).</summary>
    public const string FrontDoorIdHeader = "X-Azure-FDID";

    private static readonly PathString HealthPath = new("/health");
    private static readonly PathString AlivePath = new("/alive");

    // Blazor's static assets live under /_framework and /_content: a browser loading the app is traffic, not a probe.
    private static readonly PathString BlazorFrameworkPath = new("/_framework");
    private static readonly PathString BlazorContentPath = new("/_content");

    /// <summary>
    /// Returns how <paramref name="request"/> is counted.
    /// </summary>
    public static RequestKind Classify(HttpRequest request)
    {
        if (request.Headers[FrontDoorHealthProbeHeader] == "1")
        {
            return RequestKind.FrontDoorProbe;
        }

        if (IsProbePath(request.Path))
        {
            return RequestKind.Probe;
        }

        return request.Headers.ContainsKey(FrontDoorIdHeader) ? RequestKind.FrontDoorTraffic : RequestKind.DirectTraffic;
    }

    /// <summary>
    /// Returns whether <paramref name="path"/> is a diagnostic path: <c>/_*</c> (except Blazor's static assets),
    /// <c>/health</c> or <c>/alive</c>.
    /// </summary>
    internal static bool IsProbePath(PathString path)
    {
        if (path.StartsWithSegments(HealthPath, StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments(AlivePath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var value = path.Value;
        if (value is null || !value.StartsWith("/_", StringComparison.Ordinal))
        {
            return false;
        }

        return !path.StartsWithSegments(BlazorFrameworkPath, StringComparison.OrdinalIgnoreCase)
               && !path.StartsWithSegments(BlazorContentPath, StringComparison.OrdinalIgnoreCase);
    }
}
