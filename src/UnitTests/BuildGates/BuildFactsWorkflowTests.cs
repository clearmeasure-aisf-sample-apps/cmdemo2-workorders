using Shouldly;

namespace ClearMeasure.Bootcamp.UnitTests.BuildGates;

/// <summary>
/// The Release workflow, the script it calls and the Build workflow's artifacts have to agree, or
/// <c>GET /_build</c> answers nulls without anything failing.
/// </summary>
[TestFixture]
public class BuildFactsWorkflowTests
{
    private const string StampStep = "name: Stamp the build facts into the App Service package";

    [Test]
    public void ReleaseWorkflow_WhenRead_StampsTheFactsBetweenZippingAndPushingThePackage()
    {
        var yaml = File.ReadAllText(FindRepoFile(Path.Combine(".github", "workflows", "release.yml")));

        var zip = yaml.IndexOf("name: Zip the published UI for App Service", StringComparison.Ordinal);
        var stamp = yaml.IndexOf(StampStep, StringComparison.Ordinal);
        var push = yaml.IndexOf("name: Push the App Service package", StringComparison.Ordinal);

        zip.ShouldBeGreaterThan(-1);
        stamp.ShouldBeGreaterThan(zip);
        push.ShouldBeGreaterThan(stamp);
    }

    [Test]
    public void ReleaseWorkflow_WhenRead_CallsTheScriptWithParametersItHas()
    {
        var yaml = File.ReadAllText(FindRepoFile(Path.Combine(".github", "workflows", "release.yml")));
        var script = File.ReadAllText(FindRepoFile(Path.Combine("scripts", "Write-BuildFacts.ps1")));
        var start = yaml.IndexOf(StampStep, StringComparison.Ordinal);
        start.ShouldBeGreaterThan(-1);
        var step = yaml.Substring(start, yaml.IndexOf("\n      - name:", start, StringComparison.Ordinal) - start);

        step.ShouldContain("if: hashFiles('scripts/Write-BuildFacts.ps1') != ''");
        step.ShouldContain("GH_TOKEN: ${{ github.token }}");
        step.ShouldContain("./scripts/Write-BuildFacts.ps1 -DownloadArtifacts");
        step.ShouldContain("-RunId '${{ github.event.workflow_run.id }}'");
        foreach (var parameter in new[] { "DownloadArtifacts", "Version", "RunId", "BuiltAt", "Package" })
        {
            step.ShouldContain($"-{parameter}");
            script.ShouldMatch($@"\[(string|switch)\]\${parameter}\b");
        }
    }

    [TestCase("test-results-linux")]
    [TestCase("test-results-acceptance")]
    [TestCase("code-coverage-linux")]
    [TestCase("crap-metrics-linux")]
    [TestCase("qodana-report")]
    public void BuildWorkflow_WhenRead_UploadsTheArtifactTheScriptReads(string artifact)
    {
        var yaml = File.ReadAllText(FindRepoFile(Path.Combine(".github", "workflows", "build.yml")));
        var script = File.ReadAllText(FindRepoFile(Path.Combine("scripts", "Write-BuildFacts.ps1")));

        yaml.ShouldContain($"name: {artifact}");
        script.ShouldContain($"'{artifact}'");
    }

    private static string FindRepoFile(string relativePath)
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"{relativePath} not found from test directory.");
    }
}
