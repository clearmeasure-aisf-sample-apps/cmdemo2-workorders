using System.Reflection;
using System.Text.Json;
using ClearMeasure.Bootcamp.UI.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ClearMeasure.Bootcamp.UI.Shared;

public partial class MainLayout : IAsyncDisposable
{
    /// <summary>
    /// Must stay aligned with <c>@media (max-width: 768px)</c> in <c>MainLayout.razor.css</c> and the
    /// <c>matchMedia</c> argument in <c>mainLayoutNav.js</c>.
    /// </summary>
    public const string NavRailBreakpointMediaQuery = "(max-width: 768px)";

    private const string EnvironmentStatusPath = "/api/status/environment";
    private const string BuildFactsPath = "/_build";

    // What the environment status answers for a value the server cannot tell.
    private const string UnknownValue = "unknown";

    private const int ShortGitShaLength = 7;

    public enum Elements
    {
        NavRailToggle,
        CopyrightFooter,
        FooterNote,
        SoftwareVersion,
        DarkModeToggle,
        GitSha,
        EnvironmentName
    }

    /// <summary>
    /// Calendar year shown in the site copyright line (UTC, matches acceptance tests).
    /// </summary>
    protected int CopyrightYear => DateTime.UtcNow.Year;

    /// <summary>
    /// Version of the running entry assembly, e.g. "1.2.3", never with the commit the SDK appends to it
    /// ("1.2.3+abc1234…"): the footer shows the commit as a field of its own once the server has told it.
    /// </summary>
    protected string AppVersion => AppVersionFormatter.DisplayVersion(
        Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

    [Inject]
    private IJSRuntime Js { get; set; } = null!;

    [Inject]
    private ThemePreferenceService Theme { get; set; } = null!;

    [Inject]
    private HttpClient Http { get; set; } = null!;

    private ElementReference _navToggleButtonRef;
    private DotNetObjectReference<MainLayout>? _dotNetRef;
    private IJSObjectReference? _jsModule;
    private IJSObjectReference? _navToggleHelper;
    private bool _isNarrowViewport;
    private bool _viewportSynced;
    private bool _navVisible = true;
    private string? _gitSha;
    private string? _commitUrl;
    private string? _environmentName;

    private string AppContainerClass => NavRailCss.AppContainerClass(_isNarrowViewport, _navVisible);

    private string SidebarClass => NavRailCss.SidebarClass(_isNarrowViewport, _navVisible);

    private string NavToggleTitle =>
        _navVisible ? "Hide navigation panel" : "Show navigation panel";

    private string NavToggleAriaExpanded => _navVisible ? "true" : "false";

    private string DarkModeToggleTitle => Theme.IsDarkMode ? "Switch to light mode" : "Switch to dark mode";

    [JSInvokable]
    public Task OnViewportChanged(bool isNarrow)
    {
        if (!_viewportSynced)
        {
            _viewportSynced = true;
            if (isNarrow)
                _navVisible = false;
        }

        _isNarrowViewport = isNarrow;
        StateHasChanged();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Reads what the footer shows beside the version: the commit and the environment name the server tells, and
    /// the page of that commit from the build facts. A value nothing tells stays null and the footer leaves it out.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        var status = await GetJsonAsync(EnvironmentStatusPath);
        _gitSha = KnownValue(status, "gitSha");
        _environmentName = KnownValue(status, "environmentName");
        if (_gitSha is null)
        {
            return;
        }

        var buildFacts = await GetJsonAsync(BuildFactsPath);
        _commitUrl = CommitUrl(KnownValue(buildFacts, "commitUrl"), _gitSha);
    }

    // An undefined element when the server does not answer JSON at the path.
    private async Task<JsonElement> GetJsonAsync(string path)
    {
        try
        {
            using var response = await Http.GetAsync(path);
            if (!response.IsSuccessStatusCode)
            {
                return default;
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var document = await JsonDocument.ParseAsync(stream);
            return document.RootElement.Clone();
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            return default;
        }
    }

    private static string? KnownValue(JsonElement json, string propertyName)
    {
        var text = TextOf(json, propertyName);
        return string.IsNullOrWhiteSpace(text) || text == UnknownValue ? null : text;
    }

    private static string? TextOf(JsonElement json, string propertyName) =>
        json.ValueKind == JsonValueKind.Object
        && json.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    // Only the https page of this very commit is a link: build facts of another build do not describe it.
    private static string? CommitUrl(string? commitUrl, string gitSha) =>
        commitUrl is not null
        && commitUrl.StartsWith("https://", StringComparison.Ordinal)
        && commitUrl.EndsWith($"/{gitSha}", StringComparison.Ordinal)
            ? commitUrl
            : null;

    private static string Abbreviate(string gitSha) =>
        gitSha.Length > ShortGitShaLength ? gitSha[..ShortGitShaLength] : gitSha;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;

        try
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            _jsModule = await Js.InvokeAsync<IJSObjectReference>("import",
                "./_content/ClearMeasure.Bootcamp.UI.Shared/js/mainLayoutNav.js");
            _navToggleHelper = await _jsModule.InvokeAsync<IJSObjectReference>("initNavToggle", _dotNetRef,
                NavRailBreakpointMediaQuery);
        }
        catch (JSDisconnectedException)
        {
        }

        try
        {
            await Theme.InitializeAsync();
            Theme.OnChange += OnThemeChanged;
            await InvokeAsync(StateHasChanged);
        }
        catch (JSDisconnectedException)
        {
        }
    }

    private void OnThemeChanged() => InvokeAsync(StateHasChanged);

    private async Task ToggleDarkModeAsync() => await Theme.SetDarkModeAsync(!Theme.IsDarkMode);

    private async Task ToggleNavRailAsync()
    {
        var wasVisible = _navVisible;
        _navVisible = !wasVisible;
        await InvokeAsync(StateHasChanged);

        if (_isNarrowViewport && wasVisible)
        {
            await Task.Yield();
            try
            {
                await _navToggleButtonRef.FocusAsync();
            }
            catch (InvalidOperationException)
            {
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        Theme.OnChange -= OnThemeChanged;

        if (_navToggleHelper is not null)
        {
            try
            {
                await _navToggleHelper.InvokeVoidAsync("dispose");
            }
            catch (JSDisconnectedException)
            {
            }

            await _navToggleHelper.DisposeAsync();
        }

        if (_jsModule is not null)
            await _jsModule.DisposeAsync();

        _dotNetRef?.Dispose();
    }
}

