using System.Reflection;
using System.Runtime.InteropServices;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ClearMeasure.Bootcamp.UI.Api.Controllers;

/// <summary>
/// Runtime environment snapshot for operations and support. Environment variable
/// values are never returned.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/status/environment")]
[Route($"{ApiRoutes.VersionedApiPrefix}/status/environment")]
[EnableRateLimiting(ApiRateLimiting.PolicyName)]
public class EnvironmentStatusController : ControllerBase
{
    public const string RedactedValue = EchoController.RedactedValue;
    public const string RedactionProbeVariableName = "TEST_ENV_STATUS_SECRET";

    /// <summary>What a property answers when the process cannot tell its value.</summary>
    public const string UnknownValue = "unknown";

    private const int Sha1HexLength = 40;
    private const int Sha256HexLength = 64;

    private static readonly string[] ReportedEnvironmentVariableNames =
    [
        "ASPNETCORE_ENVIRONMENT",
        "APPLICATIONINSIGHTS_CONNECTION_STRING",
        "DOTNET_ENVIRONMENT",
        "DOTNET_ROOT",
        "DOTNET_SYSTEM_GLOBALIZATION_INVARIANT",
        RedactionProbeVariableName
    ];

    /// <summary>
    /// Returns OS description, processor count, CLR version, and selected env var names.
    /// </summary>
    [HttpGet]
    public IActionResult Get()
    {
        var payload = BuildResponse();
        var etag = ConditionalGetEtag.CreateWeakEtagForJson(payload);
        Response.Headers.ETag = etag.ToString();
        if (ConditionalGetEtag.IfNoneMatchIncludesEtag(Request, etag))
            return StatusCode(StatusCodes.Status304NotModified);
        return ConditionalGetEtag.JsonContent(payload);
    }

    private static EnvironmentStatusResponse BuildResponse()
    {
        var names = new List<string>();
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in ReportedEnvironmentVariableNames)
        {
            if (Environment.GetEnvironmentVariable(name) is null)
            {
                continue;
            }

            names.Add(name);
            variables[name] = RedactedValue;
        }

        var version = Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? UnknownValue;
        var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? UnknownValue;

        return new EnvironmentStatusResponse(
            OsDescription: RuntimeInformation.OSDescription,
            ProcessorCount: Environment.ProcessorCount,
            ClrVersion: Environment.Version.ToString(),
            EnvironmentVariableNames: names,
            EnvironmentVariables: variables,
            Version: version,
            GitSha: GitShaOf(version),
            EnvironmentName: environmentName);
    }

    /// <summary>
    /// The commit of the build. The .NET SDK appends it to the informational version ("2.4.18+7053d58a…") and
    /// records it nowhere else in the assembly. <see cref="UnknownValue"/> when the version has no such suffix or
    /// one that is not a full commit hash: build metadata of another kind is never answered as a commit.
    /// </summary>
    internal static string GitShaOf(string informationalVersion)
    {
        var separator = informationalVersion.IndexOf('+');
        var revision = informationalVersion[(separator + 1)..];
        return separator >= 0 && IsCommitHash(revision) ? revision : UnknownValue;
    }

    private static bool IsCommitHash(string text) =>
        text.Length is Sha1HexLength or Sha256HexLength && text.All(char.IsAsciiHexDigit);
}

/// <summary>
/// JSON payload for <c>GET /api/status/environment</c> and the versioned twin.
/// </summary>
public record EnvironmentStatusResponse(
    string OsDescription,
    int ProcessorCount,
    string ClrVersion,
    IReadOnlyList<string> EnvironmentVariableNames,
    IReadOnlyDictionary<string, string> EnvironmentVariables,
    string Version,
    string GitSha,
    string EnvironmentName);
