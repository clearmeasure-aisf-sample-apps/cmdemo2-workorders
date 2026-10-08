using System.Runtime.InteropServices;
using System.Text.Json;
using ClearMeasure.Bootcamp.UI.Api;
using ClearMeasure.Bootcamp.UI.Api.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shouldly;

namespace ClearMeasure.Bootcamp.UnitTests.UI.Api;

[TestFixture]
public class EnvironmentStatusControllerTests
{
    private const string SecretValue = "unit-test-env-status-secret-value";
    private const string Sha1Commit = "7053d58a94b4a107a79fd7d6598808d3d8fe4643";
    private const string Sha256Commit = Sha1Commit + "0123456789abcdef01234567";

    [Test]
    public void Get_Should_ReturnJson_WithOsDescriptionProcessorCountClrVersionAndEnvVarNames()
    {
        var result = CreateController().Get();

        var payload = AssertOkPayload(result);
        payload.OsDescription.ShouldBe(RuntimeInformation.OSDescription);
        payload.ProcessorCount.ShouldBe(Environment.ProcessorCount);
        payload.ProcessorCount.ShouldBeGreaterThan(0);
        payload.ClrVersion.ShouldBe(Environment.Version.ToString());
        payload.ClrVersion.ShouldNotBeNullOrEmpty();
        payload.EnvironmentVariableNames.ShouldNotBeNull();
        payload.EnvironmentVariables.ShouldNotBeNull();
        payload.EnvironmentVariableNames.Count.ShouldBe(payload.EnvironmentVariables.Count);
    }

    [Test]
    public void Get_Should_Return304_When_IfNoneMatchMatchesPayloadEtag()
    {
        var controller = CreateController();
        var first = controller.Get();
        first.ShouldBeOfType<ContentResult>();
        var etag = controller.Response.Headers.ETag.ToString();
        etag.ShouldNotBeNullOrEmpty();

        controller.Request.Headers.IfNoneMatch = etag;
        var second = controller.Get();

        var status = second.ShouldBeOfType<StatusCodeResult>();
        status.StatusCode.ShouldBe(StatusCodes.Status304NotModified);
    }

    [Test]
    public void Get_Should_OmitEnvironmentVariableValues_When_SecretEnvVarPresent()
    {
        using var probe = RedactionProbe.Install(SecretValue);

        var result = CreateController().Get();
        var payload = AssertOkPayload(result);

        payload.EnvironmentVariableNames.ShouldContain(EnvironmentStatusController.RedactionProbeVariableName);
        payload.EnvironmentVariables[EnvironmentStatusController.RedactionProbeVariableName]
            .ShouldBe(EnvironmentStatusController.RedactedValue);
        payload.EnvironmentVariables.Values.ShouldAllBe(value => value == EnvironmentStatusController.RedactedValue);
    }

    [Test]
    public void Get_Should_NotEchoSecretSubstrings_InAnyProperty()
    {
        using var probe = RedactionProbe.Install(SecretValue);

        var result = CreateController().Get();
        var content = result.ShouldBeOfType<ContentResult>();
        content.Content.ShouldNotBeNull();
        content.Content!.ShouldNotContain(SecretValue);
    }

    [Test]
    public void Get_Should_ReturnVersion_WhenAssemblyAttributePresent()
    {
        var result = CreateController().Get();

        var payload = AssertOkPayload(result);
        payload.Version.ShouldNotBeEmpty();
    }

    [Test]
    public void Get_Should_ReturnGitSha_OfVersion()
    {
        var result = CreateController().Get();

        var payload = AssertOkPayload(result);
        payload.GitSha.ShouldBe(EnvironmentStatusController.GitShaOf(payload.Version));
    }

    [TestCase("2.4.18+" + Sha1Commit, Sha1Commit)]
    [TestCase("3.0.0-rc.1+" + Sha1Commit, Sha1Commit)]
    [TestCase("2.4.18+" + Sha256Commit, Sha256Commit)]
    public void GitShaOf_Should_ReturnCommit_When_VersionEndsWithFullCommitHash(
        string informationalVersion,
        string expected)
    {
        EnvironmentStatusController.GitShaOf(informationalVersion).ShouldBe(expected);
    }

    [TestCase("2.4.18")]
    [TestCase("2.4.18+")]
    [TestCase("2.4.18+local")]
    [TestCase("2.4.18+20261008")]
    [TestCase("2.4.18+7053d58")]
    [TestCase("2.4.18+g053d58a94b4a107a79fd7d6598808d3d8fe4643")]
    [TestCase(Sha1Commit)]
    [TestCase(EnvironmentStatusController.UnknownValue)]
    public void GitShaOf_Should_ReturnUnknown_When_VersionHasNoFullCommitHash(string informationalVersion)
    {
        EnvironmentStatusController.GitShaOf(informationalVersion)
            .ShouldBe(EnvironmentStatusController.UnknownValue);
    }

    private static EnvironmentStatusController CreateController() =>
        new()
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

    private static EnvironmentStatusResponse AssertOkPayload(IActionResult result)
    {
        var content = result.ShouldBeOfType<ContentResult>();
        content.ContentType.ShouldNotBeNull();
        content.ContentType!.ShouldContain("application/json");
        content.Content.ShouldNotBeNull();
        var payload = JsonSerializer.Deserialize<EnvironmentStatusResponse>(
            content.Content!,
            ConditionalGetEtag.JsonSerializerOptions);
        payload.ShouldNotBeNull();
        return payload;
    }

    private sealed class RedactionProbe : IDisposable
    {
        private readonly string? _previous;

        private RedactionProbe(string? previous) => _previous = previous;

        public static RedactionProbe Install(string value)
        {
            var previous = Environment.GetEnvironmentVariable(
                EnvironmentStatusController.RedactionProbeVariableName);
            Environment.SetEnvironmentVariable(
                EnvironmentStatusController.RedactionProbeVariableName, value);
            return new RedactionProbe(previous);
        }

        public void Dispose() =>
            Environment.SetEnvironmentVariable(
                EnvironmentStatusController.RedactionProbeVariableName, _previous);
    }
}
