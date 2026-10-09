using System.Globalization;
using System.Net;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Text.Json;
using Bunit;
using ClearMeasure.Bootcamp.Core;
using ClearMeasure.Bootcamp.Core.Model;
using ClearMeasure.Bootcamp.Core.Services;
using ClearMeasure.Bootcamp.UI.Shared;
using ClearMeasure.Bootcamp.UI.Shared.Authentication;
using ClearMeasure.Bootcamp.UI.Shared.Components;
using ClearMeasure.Bootcamp.UI.Shared.Services;
using ClearMeasure.Bootcamp.UnitTests.UI.Shared.Pages;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Palermo.BlazorMvc;
using Shouldly;
using ClearMeasure.Bootcamp.UnitTests.UI.Client.Authentication;

namespace ClearMeasure.Bootcamp.UnitTests.UI.Shared;

[TestFixture]
public class MainLayoutTests
{
    private const string Commit = "abc1234def5678901abc1234def5678901abc123";
    private const string ShortCommit = "abc1234";
    private const string CommitUrl = "https://github.com/example-org/workorders/commit/" + Commit;
    private const string BuildFactsOfCommit = "{\"commit\":\"" + Commit + "\",\"commitUrl\":\"" + CommitUrl + "\"}";
    private const string DisplayedVersion = "2.4.18";
    private const string InformationalVersion = DisplayedVersion + "+" + Commit;
    private const string FieldSeparator = "·";
    private const string SoftwareVersionSelector = $"[data-testid='{nameof(MainLayout.Elements.SoftwareVersion)}']";
    private const string GitShaSelector = $"[data-testid='{nameof(MainLayout.Elements.GitSha)}']";
    private const string EnvironmentNameSelector = $"[data-testid='{nameof(MainLayout.Elements.EnvironmentName)}']";

    private Assembly? _entryAssembly;

    [SetUp]
    public void RememberEntryAssembly() => _entryAssembly = Assembly.GetEntryAssembly();

    [TearDown]
    public void RestoreEntryAssembly() => Assembly.SetEntryAssembly(_entryAssembly);

    [Test]
    public async Task ShouldRenderNavRailToggleWithExpandedStateByDefault()
    {
        await using var ctx = CreateContext();

        var component = ctx.Render<CascadingAuthenticationState>(p => p.AddChildContent<MainLayout>());
        var layout = component.FindComponent<MainLayout>();

        var toggle = layout.Find($"[data-testid='{nameof(MainLayout.Elements.NavRailToggle)}']");
        toggle.GetAttribute("aria-expanded").ShouldBe("true");
        toggle.GetAttribute("aria-controls").ShouldBe("app-navigation-rail");
        toggle.GetAttribute("title")!.ShouldContain("Hide");
        toggle.GetAttribute("aria-label")!.ShouldContain("Hide");
        layout.Find("#app-navigation-rail").ClassList.ShouldContain("modern-sidebar");
        layout.Find(".modern-app").ClassList.ShouldNotContain("rail-collapsed");
    }

    [Test]
    public async Task ShouldToggleNavRailCollapseAndUpdateAriaOnWideLayout()
    {
        await using var ctx = CreateContext();

        var component = ctx.Render<CascadingAuthenticationState>(p => p.AddChildContent<MainLayout>());
        var layout = component.FindComponent<MainLayout>();
        await component.WaitForAssertionAsync(() =>
        {
            layout.Find($"[data-testid='{nameof(MainLayout.Elements.NavRailToggle)}']").ShouldNotBeNull();
        });

        await component.InvokeAsync(() => layout.Instance.OnViewportChanged(false));

        var toggle = layout.Find($"[data-testid='{nameof(MainLayout.Elements.NavRailToggle)}']");
        await toggle.ClickAsync(new());

        toggle.GetAttribute("aria-expanded").ShouldBe("false");
        toggle.GetAttribute("title")!.ShouldContain("Show");
        layout.Find(".modern-app").ClassList.ShouldContain("rail-collapsed");
        layout.Find("#app-navigation-rail").ClassList.ShouldContain("rail-hidden");

        await toggle.ClickAsync(new());

        toggle.GetAttribute("aria-expanded").ShouldBe("true");
        toggle.GetAttribute("title")!.ShouldContain("Hide");
        layout.Find(".modern-app").ClassList.ShouldNotContain("rail-collapsed");
        layout.Find("#app-navigation-rail").ClassList.ShouldNotContain("rail-hidden");
    }

    [Test]
    public async Task ShouldRenderCorrectIconForNavVisibility()
    {
        await using var ctx = CreateContext();

        var component = ctx.Render<CascadingAuthenticationState>(p => p.AddChildContent<MainLayout>());
        var layout = component.FindComponent<MainLayout>();
        await component.WaitForAssertionAsync(() =>
        {
            layout.Find($"[data-testid='{nameof(MainLayout.Elements.NavRailToggle)}']").ShouldNotBeNull();
        });

        await component.InvokeAsync(() => layout.Instance.OnViewportChanged(false));

        var toggle = layout.Find($"[data-testid='{nameof(MainLayout.Elements.NavRailToggle)}']");
        toggle.InnerHtml.ShouldContain("bi-chevron-double-left");

        await toggle.ClickAsync(new());

        toggle.InnerHtml.ShouldContain("bi-list");
    }

    [Test]
    public async Task ShouldUseOverlayOpenClassOnNarrowViewportWhenNavVisible()
    {
        await using var ctx = CreateContext();

        var component = ctx.Render<CascadingAuthenticationState>(p => p.AddChildContent<MainLayout>());
        var layout = component.FindComponent<MainLayout>();
        await component.WaitForAssertionAsync(() =>
        {
            layout.Find($"[data-testid='{nameof(MainLayout.Elements.NavRailToggle)}']").ShouldNotBeNull();
        });

        await component.InvokeAsync(() => layout.Instance.OnViewportChanged(true));

        var rail = layout.Find("#app-navigation-rail");
        rail.ClassList.ShouldNotContain("open");

        var toggle = layout.Find($"[data-testid='{nameof(MainLayout.Elements.NavRailToggle)}']");
        await toggle.ClickAsync(new());

        rail.ClassList.ShouldContain("open");
        toggle.GetAttribute("aria-expanded").ShouldBe("true");
    }

    [Test]
    public void ShouldUseDocumentedNavRailBreakpointMediaQuery()
    {
        MainLayout.NavRailBreakpointMediaQuery.ShouldBe("(max-width: 768px)");
    }

    [Test]
    public async Task MainLayout_AfterFirstRender_ShouldCallThemeInitialize_WhenImplemented()
    {
        await using var ctx = CreateContext();
        var themeModule = ctx.JSInterop.SetupModule(ThemePreferenceService.ThemeJsModulePath);
        themeModule.Setup<string>("getTheme").SetResult("light");
        themeModule.SetupVoid("syncDomFromTheme", _ => true).SetVoidResult();

        ctx.Render<CascadingAuthenticationState>(p => p.AddChildContent<MainLayout>());

        themeModule.VerifyInvoke("getTheme");
    }

    [Test]
    public async Task ShouldRenderLoginLink_WithBlinkClass_WhenUserIsNotAuthenticated()
    {
        await using var ctx = CreateContext();

        var component = ctx.Render<CascadingAuthenticationState>(p => p.AddChildContent<MainLayout>());
        var layout = component.FindComponent<MainLayout>();

        var loginAnchor = layout.Find($"a[data-testid='{nameof(LoginLink.Elements.LoginLink)}']");
        loginAnchor.GetAttribute("data-testid").ShouldBe(nameof(LoginLink.Elements.LoginLink));
        loginAnchor.ClassList.ShouldContain("login-link-blink");
        loginAnchor.GetAttribute("id").ShouldBe("login-link-blink");
        loginAnchor.TextContent.Trim().ShouldBe("Login");
        loginAnchor.GetAttribute("href").ShouldBe("/login");
    }

    [Test]
    public async Task ShouldExposeBlinkIdOnSameAnchorAsBlinkClassAndTestId_WhenUserIsNotAuthenticated()
    {
        await using var ctx = CreateContext();

        var component = ctx.Render<CascadingAuthenticationState>(p => p.AddChildContent<MainLayout>());
        var layout = component.FindComponent<MainLayout>();

        var anchorsById = layout.FindAll("#login-link-blink");
        anchorsById.Count.ShouldBe(1);

        var loginAnchor = anchorsById[0];
        loginAnchor.TagName.ShouldBe("A");
        loginAnchor.ClassList.ShouldContain("login-link-blink");
        loginAnchor.GetAttribute("data-testid").ShouldBe(nameof(LoginLink.Elements.LoginLink));
        loginAnchor.GetAttribute("href").ShouldBe("/login");
    }

    [Test]
    public async Task ShouldNotExposeBlinkId_WhenUserIsAuthenticated()
    {
        await using var ctx = CreateContext(authenticateAsUser: "hsimpson");

        var component = ctx.Render<CascadingAuthenticationState>(p => p.AddChildContent<MainLayout>());
        var layout = component.FindComponent<MainLayout>();

        layout.FindAll("#login-link-blink").Count.ShouldBe(0);
        layout.Find($"[data-testid='{nameof(Logout.Elements.LogoutLink)}']").ShouldNotBeNull();
    }

    [Test]
    public async Task ShouldPreserveLoginLinkHref_WhenBlinkClassApplied()
    {
        await using var ctx = CreateContext();

        var component = ctx.Render<CascadingAuthenticationState>(p => p.AddChildContent<MainLayout>());
        var layout = component.FindComponent<MainLayout>();

        var loginAnchor = layout.Find($"a[data-testid='{nameof(LoginLink.Elements.LoginLink)}']");
        loginAnchor.ClassList.ShouldContain("login-link-blink");
        loginAnchor.GetAttribute("href").ShouldBe("/login");
    }

    [Test]
    public async Task ShouldNotRenderLoginLink_WhenUserIsAuthenticated()
    {
        await using var ctx = CreateContext(authenticateAsUser: "hsimpson");

        var component = ctx.Render<CascadingAuthenticationState>(p => p.AddChildContent<MainLayout>());
        var layout = component.FindComponent<MainLayout>();

        layout.FindAll($"a[data-testid='{nameof(LoginLink.Elements.LoginLink)}']").Count.ShouldBe(0);
        layout.Find($"[data-testid='{nameof(Logout.Elements.LogoutLink)}']").ShouldNotBeNull();
    }

    [Test]
    public async Task ShouldNotExposeBlinkClass_WhenUserIsAuthenticated()
    {
        await using var ctx = CreateContext(authenticateAsUser: "hsimpson");

        var component = ctx.Render<CascadingAuthenticationState>(p => p.AddChildContent<MainLayout>());
        var layout = component.FindComponent<MainLayout>();

        layout.FindAll(".login-link-blink").Count.ShouldBe(0);
        layout.Find($"[data-testid='{nameof(Logout.Elements.LogoutLink)}']").ShouldNotBeNull();
    }

    [Test]
    public async Task ShouldRenderCopyrightFooter_WithCurrentYear_OrganizationAndLink_WhenNotAuthenticated()
    {
        await using var ctx = CreateContext();

        var component = ctx.Render<CascadingAuthenticationState>(p => p.AddChildContent<MainLayout>());
        var layout = component.FindComponent<MainLayout>();

        var footer = layout.Find($"[data-testid='{nameof(MainLayout.Elements.CopyrightFooter)}']");
        footer.TagName.ShouldBe("FOOTER");
        layout.FindAll("#app-navigation-rail footer").Count.ShouldBe(0);

        var yearText = DateTime.UtcNow.Year.ToString(CultureInfo.InvariantCulture);
        footer.TextContent.ShouldContain(yearText);
        footer.TextContent.ShouldContain("ClearMeasure Labs");

        var link = layout.Find($"[data-testid='{nameof(MainLayout.Elements.CopyrightFooter)}'] .site-footer-link");
        link.GetAttribute("href")!.TrimEnd('/').ShouldBe("https://clearmeasure.com");
        link.TextContent.Trim().ShouldBe("ClearMeasure Labs");
    }

    [Test]
    public async Task ShouldRenderCopyrightFooter_WhenAuthenticated()
    {
        await using var ctx = CreateContext(authenticateAsUser: "hsimpson");

        var component = ctx.Render<CascadingAuthenticationState>(p => p.AddChildContent<MainLayout>());
        var layout = component.FindComponent<MainLayout>();

        var footer = layout.Find($"[data-testid='{nameof(MainLayout.Elements.CopyrightFooter)}']");
        var yearText = DateTime.UtcNow.Year.ToString(CultureInfo.InvariantCulture);
        footer.TextContent.ShouldContain(yearText);
        footer.TextContent.ShouldContain("ClearMeasure Labs");
        layout.Find($"[data-testid='{nameof(MainLayout.Elements.CopyrightFooter)}'] .site-footer-link").GetAttribute("href")!.TrimEnd('/').ShouldBe("https://clearmeasure.com");
    }

    [Test]
    public async Task ShouldRenderSoftwareVersion_WithinSiteFooter()
    {
        await using var ctx = CreateContext();

        var component = ctx.Render<CascadingAuthenticationState>(p => p.AddChildContent<MainLayout>());
        var layout = component.FindComponent<MainLayout>();

        var versionSpan = layout.Find($"[data-testid='{nameof(MainLayout.Elements.SoftwareVersion)}']");
        versionSpan.TextContent.Trim().ShouldNotBeEmpty();

        var footer = layout.Find($"[data-testid='{nameof(MainLayout.Elements.CopyrightFooter)}']");
        footer.QuerySelector($"[data-testid='{nameof(MainLayout.Elements.SoftwareVersion)}']").ShouldNotBeNull();
    }

    [Test]
    public async Task ShouldRenderSoftwareVersion_WhenUserIsAuthenticated()
    {
        await using var ctx = CreateContext(authenticateAsUser: "hsimpson");

        var component = ctx.Render<CascadingAuthenticationState>(p => p.AddChildContent<MainLayout>());
        var layout = component.FindComponent<MainLayout>();

        var versionSpan = layout.Find($"[data-testid='{nameof(MainLayout.Elements.SoftwareVersion)}']");
        versionSpan.ShouldNotBeNull();

        var footer = layout.Find($"[data-testid='{nameof(MainLayout.Elements.CopyrightFooter)}']");
        footer.QuerySelector($"[data-testid='{nameof(MainLayout.Elements.SoftwareVersion)}']").ShouldNotBeNull();
    }

    [Test]
    public async Task ShouldRenderFooterNote_WithinSiteFooter()
    {
        await using var ctx = CreateContext();

        var component = ctx.Render<CascadingAuthenticationState>(p => p.AddChildContent<MainLayout>());
        var layout = component.FindComponent<MainLayout>();

        var note = layout.Find($"[data-testid='{nameof(MainLayout.Elements.FooterNote)}']");
        note.TextContent.Trim().ShouldBe("Submit a new work order any time — requests are typically reviewed within one business day. Thank you for serving!");

        var footer = layout.Find($"[data-testid='{nameof(MainLayout.Elements.CopyrightFooter)}']");
        footer.QuerySelector($"[data-testid='{nameof(MainLayout.Elements.FooterNote)}']").ShouldNotBeNull();
    }

    [Test]
    public async Task ShouldRenderCompanyLink_WithAccessibleAttributes_WhenExternalLinkUsesNewTab()
    {
        await using var ctx = CreateContext();

        var component = ctx.Render<CascadingAuthenticationState>(p => p.AddChildContent<MainLayout>());
        var layout = component.FindComponent<MainLayout>();

        var link = layout.Find($"[data-testid='{nameof(MainLayout.Elements.CopyrightFooter)}'] .site-footer-link");
        link.GetAttribute("target").ShouldBe("_blank");
        var rel = link.GetAttribute("rel");
        rel.ShouldNotBeNull();
        rel.ShouldContain("noopener");
        rel.ShouldContain("noreferrer");
        link.TextContent.Trim().ShouldNotContain("://");
    }

    [Test]
    public async Task ShouldInvokeFocusOnNavRailToggleWhenClosingOverlayOnNarrowViewport()
    {
        await using var ctx = CreateContext();

        var component = ctx.Render<CascadingAuthenticationState>(p => p.AddChildContent<MainLayout>());
        var layout = component.FindComponent<MainLayout>();
        await component.WaitForAssertionAsync(() =>
        {
            layout.Find($"[data-testid='{nameof(MainLayout.Elements.NavRailToggle)}']").ShouldNotBeNull();
        });

        await component.InvokeAsync(() => layout.Instance.OnViewportChanged(true));

        var toggle = layout.Find($"[data-testid='{nameof(MainLayout.Elements.NavRailToggle)}']");
        await toggle.ClickAsync(new());
        await toggle.ClickAsync(new());

        ctx.JSInterop.VerifyFocusAsyncInvoke();
    }

    [Test]
    public async Task ShouldRenderBackdrop_WhenNarrowAndNavOpen()
    {
        await using var ctx = CreateContext();

        var component = ctx.Render<CascadingAuthenticationState>(p => p.AddChildContent<MainLayout>());
        var layout = component.FindComponent<MainLayout>();
        await component.WaitForAssertionAsync(() =>
        {
            layout.Find($"[data-testid='{nameof(MainLayout.Elements.NavRailToggle)}']").ShouldNotBeNull();
        });

        await component.InvokeAsync(() => layout.Instance.OnViewportChanged(true));

        var toggle = layout.Find($"[data-testid='{nameof(MainLayout.Elements.NavRailToggle)}']");
        await toggle.ClickAsync(new());

        layout.FindAll(".nav-backdrop").Count.ShouldBe(1);
        layout.Find("#app-navigation-rail").ClassList.ShouldContain("open");
    }

    [Test]
    public async Task ShouldNotRenderBackdrop_WhenNarrowAndNavClosed()
    {
        await using var ctx = CreateContext();

        var component = ctx.Render<CascadingAuthenticationState>(p => p.AddChildContent<MainLayout>());
        var layout = component.FindComponent<MainLayout>();
        await component.WaitForAssertionAsync(() =>
        {
            layout.Find($"[data-testid='{nameof(MainLayout.Elements.NavRailToggle)}']").ShouldNotBeNull();
        });

        await component.InvokeAsync(() => layout.Instance.OnViewportChanged(true));

        // Nav auto-hides on first narrow signal — backdrop should not be present
        layout.FindAll(".nav-backdrop").Count.ShouldBe(0);
    }

    [Test]
    public async Task ShouldNotRenderBackdrop_OnWideViewport()
    {
        await using var ctx = CreateContext();

        var component = ctx.Render<CascadingAuthenticationState>(p => p.AddChildContent<MainLayout>());
        var layout = component.FindComponent<MainLayout>();
        await component.WaitForAssertionAsync(() =>
        {
            layout.Find($"[data-testid='{nameof(MainLayout.Elements.NavRailToggle)}']").ShouldNotBeNull();
        });

        await component.InvokeAsync(() => layout.Instance.OnViewportChanged(false));

        layout.FindAll(".nav-backdrop").Count.ShouldBe(0);
    }

    [Test]
    public async Task ShouldDismissNavWhenBackdropClicked()
    {
        await using var ctx = CreateContext();

        var component = ctx.Render<CascadingAuthenticationState>(p => p.AddChildContent<MainLayout>());
        var layout = component.FindComponent<MainLayout>();
        await component.WaitForAssertionAsync(() =>
        {
            layout.Find($"[data-testid='{nameof(MainLayout.Elements.NavRailToggle)}']").ShouldNotBeNull();
        });

        await component.InvokeAsync(() => layout.Instance.OnViewportChanged(true));

        var toggle = layout.Find($"[data-testid='{nameof(MainLayout.Elements.NavRailToggle)}']");
        await toggle.ClickAsync(new());

        layout.FindAll(".nav-backdrop").Count.ShouldBe(1);

        var backdrop = layout.Find(".nav-backdrop");
        await backdrop.ClickAsync(new());

        layout.Find("#app-navigation-rail").ClassList.ShouldNotContain("open");
        layout.FindAll(".nav-backdrop").Count.ShouldBe(0);
    }


    [Test]
    public async Task DarkModeToggle_ShouldRender_WithSunIcon_WhenLightMode()
    {
        await using var ctx = CreateContext();

        var component = ctx.Render<CascadingAuthenticationState>(p => p.AddChildContent<MainLayout>());
        var layout = component.FindComponent<MainLayout>();

        var button = layout.Find($"[data-testid='{nameof(MainLayout.Elements.DarkModeToggle)}']");
        button.ShouldNotBeNull();
        button.InnerHtml.ShouldContain("bi-sun");
    }

    [Test]
    public async Task DarkModeToggle_ShouldRender_WithMoonIcon_WhenDarkMode()
    {
        await using var ctx = CreateContext();
        var themeModule = ctx.JSInterop.SetupModule(ThemePreferenceService.ThemeJsModulePath);
        themeModule.Setup<string>("getTheme").SetResult("dark");
        themeModule.SetupVoid("syncDomFromTheme", _ => true).SetVoidResult();
        themeModule.SetupVoid("setTheme", _ => true).SetVoidResult();

        var component = ctx.Render<CascadingAuthenticationState>(p => p.AddChildContent<MainLayout>());
        var layout = component.FindComponent<MainLayout>();
        await component.WaitForAssertionAsync(() =>
        {
            layout.Find($"[data-testid='{nameof(MainLayout.Elements.DarkModeToggle)}']").InnerHtml.ShouldContain("bi-moon");
        });
    }

    [Test]
    public async Task DarkModeToggle_Click_ShouldFlipIsDarkMode()
    {
        await using var ctx = CreateContext();
        var themeModule = ctx.JSInterop.SetupModule(ThemePreferenceService.ThemeJsModulePath);
        themeModule.Setup<string>("getTheme").SetResult("light");
        themeModule.SetupVoid("syncDomFromTheme", _ => true).SetVoidResult();
        themeModule.SetupVoid("setTheme", _ => true).SetVoidResult();

        var component = ctx.Render<CascadingAuthenticationState>(p => p.AddChildContent<MainLayout>());
        var layout = component.FindComponent<MainLayout>();
        await component.WaitForAssertionAsync(() =>
        {
            layout.Find($"[data-testid='{nameof(MainLayout.Elements.DarkModeToggle)}']").ShouldNotBeNull();
        });

        var button = layout.Find($"[data-testid='{nameof(MainLayout.Elements.DarkModeToggle)}']");
        button.InnerHtml.ShouldContain("bi-sun");

        await button.ClickAsync(new());

        await component.WaitForAssertionAsync(() =>
        {
            layout.Find($"[data-testid='{nameof(MainLayout.Elements.DarkModeToggle)}']").InnerHtml.ShouldContain("bi-moon");
        });
        ctx.Services.GetRequiredService<ThemePreferenceService>().IsDarkMode.ShouldBeTrue();
    }

    [Test]
    public async Task DarkModeToggle_ShouldBeVisible_WhenUserIsNotAuthenticated()
    {
        await using var ctx = CreateContext();

        var component = ctx.Render<CascadingAuthenticationState>(p => p.AddChildContent<MainLayout>());
        var layout = component.FindComponent<MainLayout>();

        layout.Find($"[data-testid='{nameof(MainLayout.Elements.DarkModeToggle)}']").ShouldNotBeNull();
    }

    [Test]
    public async Task DarkModeToggle_ShouldBeVisible_WhenUserIsAuthenticated()
    {
        await using var ctx = CreateContext(authenticateAsUser: "hsimpson");

        var component = ctx.Render<CascadingAuthenticationState>(p => p.AddChildContent<MainLayout>());
        var layout = component.FindComponent<MainLayout>();

        layout.Find($"[data-testid='{nameof(MainLayout.Elements.DarkModeToggle)}']").ShouldNotBeNull();
    }


    [Test]
    public async Task ShouldLinkGitSha_ToCommitUrlOfBuildFacts_WhenBuildFactsNameThatCommit()
    {
        await using var ctx = CreateContext(gitSha: Commit, buildFactsJson: BuildFactsOfCommit);

        var layout = RenderLayout(ctx);

        var anchor = layout.Find(GitShaSelector);
        anchor.TagName.ShouldBe("A");
        anchor.GetAttribute("href").ShouldBe(CommitUrl);
        anchor.GetAttribute("target").ShouldBe("_blank");
        anchor.GetAttribute("rel").ShouldBe("noopener noreferrer");
    }

    [Test]
    public async Task ShouldTruncateGitSha_ToSevenChars_ForDisplayText()
    {
        await using var ctx = CreateContext(gitSha: Commit, buildFactsJson: BuildFactsOfCommit);

        var layout = RenderLayout(ctx);

        layout.Find(GitShaSelector).TextContent.Trim().ShouldBe(ShortCommit);
    }

    [Test]
    public async Task ShouldRenderGitSha_AsReturned_WhenShorterThanSevenChars()
    {
        await using var ctx = CreateContext(gitSha: "abc12");

        var layout = RenderLayout(ctx);

        layout.Find(GitShaSelector).TextContent.Trim().ShouldBe("abc12");
    }

    [TestCase(null)]
    [TestCase("{\"commit\":null,\"commitUrl\":null}")]
    [TestCase("{\"commitUrl\":\"https://github.com/example-org/workorders/commit/0000000000000000000000000000000000000000\"}")]
    [TestCase("{\"commitUrl\":\"http://github.com/example-org/workorders/commit/" + Commit + "\"}")]
    [TestCase("{not valid json")]
    [TestCase("[]")]
    public async Task ShouldRenderGitSha_WithoutLink_WhenBuildFactsDoNotLinkThatCommit(string? buildFactsJson)
    {
        await using var ctx = CreateContext(gitSha: Commit, buildFactsJson: buildFactsJson);

        var layout = RenderLayout(ctx);

        var element = layout.Find(GitShaSelector);
        element.TagName.ShouldBe("SPAN");
        element.TextContent.Trim().ShouldBe(ShortCommit);
        layout.FindAll($"{SoftwareVersionSelector} a").Count.ShouldBe(0);
    }

    [Test]
    public async Task ShouldRenderEnvironmentName_FromEndpointResponse()
    {
        await using var ctx = CreateContext(environmentName: "Staging");

        var layout = RenderLayout(ctx);

        layout.Find(EnvironmentNameSelector).TextContent.Trim().ShouldBe("Staging");
        layout.FindAll(GitShaSelector).Count.ShouldBe(0);
    }

    [Test]
    public async Task ShouldRenderGitShaThenEnvironmentName_AfterVersion_WhenEndpointReturnsBoth()
    {
        await using var ctx = CreateContext(gitSha: Commit, environmentName: "Staging");

        var layout = RenderLayout(ctx);

        var parts = FooterVersionParts(layout);
        parts.Length.ShouldBe(3);
        parts[0].ShouldNotBeEmpty();
        parts[1].ShouldBe(ShortCommit);
        parts[2].ShouldBe("Staging");
    }

    [Test]
    public async Task ShouldLeaveOutGitShaAndEnvironmentName_WhenEndpointReturnsUnknown()
    {
        await using var ctx = CreateContext();

        var layout = RenderLayout(ctx);

        AssertFooterShowsVersionOnly(layout);
    }

    [Test]
    public async Task ShouldLeaveOutGitShaAndEnvironmentName_WhenEndpointFails()
    {
        await using var ctx = CreateContext(simulateHttpError: true);

        var layout = RenderLayout(ctx);

        AssertFooterShowsVersionOnly(layout);
    }

    [Test]
    public async Task ShouldLeaveOutGitShaAndEnvironmentName_WhenServerIsNotReachable()
    {
        await using var ctx = CreateContext(simulateConnectionFailure: true);

        var layout = RenderLayout(ctx);

        AssertFooterShowsVersionOnly(layout);
    }

    [TestCase("{\"version\":\"1.0.0\"}")]
    [TestCase("{\"gitSha\":null,\"environmentName\":null}")]
    [TestCase("{\"gitSha\":\"\",\"environmentName\":\" \"}")]
    [TestCase("{\"gitSha\":7053,\"environmentName\":true}")]
    [TestCase("{not valid json")]
    [TestCase("[]")]
    public async Task ShouldLeaveOutGitShaAndEnvironmentName_WhenResponseDoesNotNameThem(string rawJsonBody)
    {
        await using var ctx = CreateContext(rawJsonBody: rawJsonBody);

        var layout = RenderLayout(ctx);

        AssertFooterShowsVersionOnly(layout);
    }

    [Test]
    public async Task ShouldRenderVersionWithoutBuildMetadata_WhileEnvironmentStatusIsPending_ThenLinkGitSha()
    {
        StubEntryAssembly(InformationalVersion);
        var environmentStatus = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var ctx = CreateContext(
            gitSha: Commit, buildFactsJson: BuildFactsOfCommit, environmentStatusGate: environmentStatus.Task);

        var layout = RenderLayout(ctx);

        FooterVersionParts(layout).ShouldBe([DisplayedVersion]);
        layout.FindAll(GitShaSelector).Count.ShouldBe(0);

        environmentStatus.SetResult();

        await layout.WaitForAssertionAsync(() =>
        {
            var anchor = layout.Find(GitShaSelector);
            anchor.TagName.ShouldBe("A");
            anchor.GetAttribute("href").ShouldBe(CommitUrl);
        });
        FooterVersionParts(layout).ShouldBe([DisplayedVersion, ShortCommit]);
    }

    [TestCase(HttpStatusCode.ServiceUnavailable)]
    [TestCase(HttpStatusCode.TooManyRequests)]
    public async Task ShouldRenderVersionWithoutBuildMetadata_WhenEnvironmentStatusFails(HttpStatusCode status)
    {
        StubEntryAssembly(InformationalVersion);
        await using var ctx = CreateContext(simulateHttpError: true, httpErrorStatus: status);

        var layout = RenderLayout(ctx);

        FooterVersionParts(layout).ShouldBe([DisplayedVersion]);
        layout.FindAll(GitShaSelector).Count.ShouldBe(0);
    }

    private static IRenderedComponent<MainLayout> RenderLayout(BunitContext ctx) =>
        ctx.Render<CascadingAuthenticationState>(p => p.AddChildContent<MainLayout>()).FindComponent<MainLayout>();

    private static string[] FooterVersionParts(IRenderedComponent<MainLayout> layout) =>
        layout.Find(SoftwareVersionSelector).TextContent
            .Split(FieldSeparator, StringSplitOptions.TrimEntries);

    private static void AssertFooterShowsVersionOnly(IRenderedComponent<MainLayout> layout)
    {
        layout.FindAll(GitShaSelector).Count.ShouldBe(0);
        layout.FindAll(EnvironmentNameSelector).Count.ShouldBe(0);

        var parts = FooterVersionParts(layout);
        parts.Length.ShouldBe(1);
        parts[0].ShouldNotBeEmpty();
        parts[0].ShouldNotContain("unknown");
    }

    // The footer shows the version of the entry assembly, and the test host's has no build metadata to leave out.
    // The runtime takes only a loaded assembly as the entry assembly: a type of the dynamic one gives it.
    private static void StubEntryAssembly(string informationalVersion)
    {
        var attribute = new CustomAttributeBuilder(
            typeof(AssemblyInformationalVersionAttribute).GetConstructor([typeof(string)])!,
            [informationalVersion]);
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName(nameof(StubEntryAssembly)), AssemblyBuilderAccess.Run, [attribute]);
        var type = assembly.DefineDynamicModule(nameof(StubEntryAssembly)).DefineType(nameof(StubEntryAssembly));
        Assembly.SetEntryAssembly(type.CreateType().Assembly);
    }

    private static BunitContext CreateContext(
        string? authenticateAsUser = null,
        string? gitSha = null,
        string? environmentName = null,
        bool simulateHttpError = false,
        string? rawJsonBody = null,
        string? buildFactsJson = null,
        bool simulateConnectionFailure = false,
        HttpStatusCode httpErrorStatus = HttpStatusCode.InternalServerError,
        Task? environmentStatusGate = null)
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        var bunitAuth = ctx.AddAuthorization();
        if (authenticateAsUser != null)
        {
            bunitAuth.SetAuthorized(authenticateAsUser);
        }

        ctx.Services.AddSingleton<IUiBus>(new StubUiBus());
        ctx.Services.AddSingleton<IBus>(new StubBus());
        ctx.Services.AddSingleton<IUserSession>(new StubUserSession());
        ctx.Services.AddSingleton(ctx.JSInterop.JSRuntime);
        ctx.Services.AddSingleton<ThemePreferenceService>();
        var customAuth = new CustomAuthenticationStateProvider(new StubUserSessionStore());
        if (authenticateAsUser != null)
        {
            customAuth.Login(authenticateAsUser).GetAwaiter().GetResult();
        }

        ctx.Services.AddSingleton(customAuth);

        var environmentStatusJson = simulateHttpError
            ? null
            : rawJsonBody ?? JsonSerializer.Serialize(
                new EnvironmentStatusStub(
                    Version: "1.0.0",
                    GitSha: gitSha ?? "unknown",
                    EnvironmentName: environmentName ?? "unknown"),
                JsonSerializerOptions.Web);
        HttpMessageHandler handler = simulateConnectionFailure
            ? new StubUnreachableServerHandler()
            : new StubServerHandler(environmentStatusJson, buildFactsJson, httpErrorStatus, environmentStatusGate);
        ctx.Services.AddSingleton(new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost/")
        });

        return ctx;
    }

    private sealed class StubUserSession : IUserSession
    {
        public Task<Employee?> GetCurrentUserAsync() => Task.FromResult<Employee?>(null);
    }

    // ReSharper disable NotAccessedPositionalProperty.Local -- properties consumed via JSON serialization reflection
    private sealed record EnvironmentStatusStub(string Version, string GitSha, string EnvironmentName);
    // ReSharper restore NotAccessedPositionalProperty.Local

    /// <summary>
    /// Answers the two documents the footer reads; a null document is a failed request, and any other path is one too.
    /// The environment status is answered only once its gate completes, when it has one.
    /// </summary>
    private sealed class StubServerHandler(
        string? environmentStatusJson,
        string? buildFactsJson,
        HttpStatusCode failureStatus,
        Task? environmentStatusGate) : HttpMessageHandler
    {
        private const string EnvironmentStatusPath = "/api/status/environment";

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath;
            if (path == EnvironmentStatusPath && environmentStatusGate is not null)
            {
                await environmentStatusGate;
            }

            var json = path switch
            {
                EnvironmentStatusPath => environmentStatusJson,
                "/_build" => buildFactsJson,
                _ => null
            };
            if (json is null)
            {
                return new HttpResponseMessage(failureStatus);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class StubUnreachableServerHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException("Connection refused"));
    }
}
