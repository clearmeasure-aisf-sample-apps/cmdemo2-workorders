using System.Globalization;
using ClearMeasure.Bootcamp.UI.Api.Controllers;
using ClearMeasure.Bootcamp.UI.Shared;

namespace ClearMeasure.Bootcamp.AcceptanceTests.App;

[TestFixture]
public class CopyrightFooterTests : AcceptanceTestBase
{
    [Test, Retry(2)]
    public async Task ShouldShowCopyrightFooter_OnLandingPage_WhenAnonymous()
    {
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var footer = Page.GetByTestId(nameof(MainLayout.Elements.CopyrightFooter));
        await footer.WaitForAsync();
        await Expect(footer).ToBeVisibleAsync();

        var yearText = DateTime.UtcNow.Year.ToString(CultureInfo.InvariantCulture);
        await Expect(footer).ToContainTextAsync(yearText);
        await Expect(footer).ToContainTextAsync("ClearMeasure Labs");

        var link = footer.Locator("a[href*='clearmeasure.com']").First;
        await Expect(link).ToBeVisibleAsync();
        var href = (await link.GetAttributeAsync("href"))!.ToLowerInvariant();
        href.ShouldStartWith("http");
        href.ShouldContain("clearmeasure.com");
    }

    [Test, Retry(2)]
    public async Task ShouldShowCopyrightFooter_OnAuthenticatedRoute_AfterLogin()
    {
        await LoginAsCurrentUser();
        await Click(nameof(NavMenu.Elements.Search));
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var footer = Page.GetByTestId(nameof(MainLayout.Elements.CopyrightFooter));
        await Expect(footer).ToBeVisibleAsync();
        var yearText = DateTime.UtcNow.Year.ToString(CultureInfo.InvariantCulture);
        await Expect(footer).ToContainTextAsync(yearText);
        await Expect(footer).ToContainTextAsync("ClearMeasure Labs");
    }

    [Test, Retry(2)]
    public async Task ShouldShowCopyrightFooter_OnNotFoundRoute()
    {
        await Page.GotoAsync("/this-route-does-not-exist-1842");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var footer = Page.GetByTestId(nameof(MainLayout.Elements.CopyrightFooter));
        await footer.WaitForAsync();
        await footer.ScrollIntoViewIfNeededAsync();
        await Expect(footer).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Alert)).ToContainTextAsync("Sorry, there's nothing at this address.");
    }

    [Test, Retry(2)]
    public async Task ShouldShowFooterNote_OnLandingPage_WhenAnonymous()
    {
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var footerNote = Page.GetByTestId(nameof(MainLayout.Elements.FooterNote));
        await footerNote.WaitForAsync();
        await Expect(footerNote).ToBeVisibleAsync();
        await Expect(footerNote).ToContainTextAsync("Submit a new work order any time");
        await Expect(footerNote).ToContainTextAsync("Thank you for serving!");
    }

    [Test, Retry(2)]
    public async Task ShouldShowSoftwareVersion_OnLandingPage_WhenAnonymous()
    {
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var versionSpan = Page.GetByTestId(nameof(MainLayout.Elements.SoftwareVersion));
        await versionSpan.WaitForAsync();
        await Expect(versionSpan).ToBeVisibleAsync();
        var text = await versionSpan.InnerTextAsync();
        text.Trim().ShouldNotBeEmpty();
        text.ShouldNotContain(EnvironmentStatusController.UnknownValue);
        text.ShouldNotContain("+");
    }

    [Test, Retry(2)]
    public async Task ShouldShowGitSha_InFooter_OnLandingPage()
    {
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.GetByTestId(nameof(MainLayout.Elements.SoftwareVersion)).WaitForAsync();

        var commit = await ServerStatusValue("gitSha");
        var gitSha = Page.GetByTestId(nameof(MainLayout.Elements.GitSha));
        if (commit is null)
        {
            await Expect(gitSha).ToHaveCountAsync(0);
            return;
        }

        await Expect(gitSha).ToHaveTextAsync(commit[..7]);
        await ExpectLinkToCommitPage(gitSha, commit);
    }

    [Test, Retry(2)]
    public async Task ShouldShowEnvironmentName_InFooter_OnLandingPage()
    {
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.GetByTestId(nameof(MainLayout.Elements.SoftwareVersion)).WaitForAsync();

        var environmentName = await ServerStatusValue("environmentName");
        var envName = Page.GetByTestId(nameof(MainLayout.Elements.EnvironmentName));
        if (environmentName is null)
        {
            await Expect(envName).ToHaveCountAsync(0);
            return;
        }

        await Expect(envName).ToHaveTextAsync(environmentName);
    }

    [Test, Retry(2)]
    public async Task ShouldShowVersionWithoutCommit_WhileEnvironmentStatusIsPending_ThenGitShaWithItsLink()
    {
        var statusRequested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var statusReleased = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await Page.RouteAsync("**/api/status/environment", async route =>
        {
            statusRequested.TrySetResult();
            await statusReleased.Task;
            await route.ContinueAsync();
        });

        await Page.ReloadAsync();
        await statusRequested.Task.WaitAsync(TimeSpan.FromSeconds(60));

        var versionSpan = Page.GetByTestId(nameof(MainLayout.Elements.SoftwareVersion));
        var gitSha = Page.GetByTestId(nameof(MainLayout.Elements.GitSha));
        await Expect(versionSpan).ToBeVisibleAsync();
        await Expect(gitSha).ToHaveCountAsync(0);
        var pendingText = await versionSpan.InnerTextAsync();
        pendingText.Trim().ShouldNotBeEmpty();
        pendingText.ShouldNotContain("+");

        statusReleased.SetResult();

        var commit = await ServerStatusValue("gitSha");
        if (commit is null)
        {
            await Expect(gitSha).ToHaveCountAsync(0);
            return;
        }

        await Expect(gitSha).ToHaveTextAsync(commit[..7]);
        await ExpectLinkToCommitPage(gitSha, commit);
        (await versionSpan.InnerTextAsync()).ShouldNotContain("+");
    }

    /// <summary>
    /// What the server under test tells of itself at <c>/api/status/environment</c>, or null when it cannot tell:
    /// the footer shows the first and leaves out the second, in a local build and in a deployed environment alike.
    /// </summary>
    private async Task<string?> ServerStatusValue(string propertyName)
    {
        var response = await Page.APIRequest.GetAsync("/api/status/environment");
        var status = await response.JsonAsync();
        var value = status?.GetProperty(propertyName).GetString();
        return value == EnvironmentStatusController.UnknownValue ? null : value;
    }

    /// <summary>
    /// A released build links the commit to its page, which its build facts name at <c>/_build</c>; a local build
    /// names none, and its footer shows the commit without a link.
    /// </summary>
    private async Task ExpectLinkToCommitPage(ILocator gitSha, string commit)
    {
        var response = await Page.APIRequest.GetAsync("/_build");
        var buildFacts = await response.JsonAsync();
        var commitUrl = buildFacts?.GetProperty("commitUrl").GetString();
        if (commitUrl is null || !commitUrl.EndsWith($"/commit/{commit}", StringComparison.Ordinal))
        {
            (await gitSha.GetAttributeAsync("href")).ShouldBeNull();
            return;
        }

        await Expect(gitSha).ToHaveAttributeAsync("href", commitUrl);
    }
}
