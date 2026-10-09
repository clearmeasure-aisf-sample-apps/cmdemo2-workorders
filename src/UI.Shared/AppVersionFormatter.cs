namespace ClearMeasure.Bootcamp.UI.Shared;

/// <summary>
/// Display formatting for the version of the app, in the site footer and on the login page.
/// </summary>
internal static class AppVersionFormatter
{
    /// <summary>
    /// Returns the version without its build metadata: "2.4.18+7053d58a…" (the SDK appends the commit) -> "2.4.18".
    /// </summary>
    public static string DisplayVersion(string? informationalVersion)
    {
        if (string.IsNullOrEmpty(informationalVersion))
        {
            return string.Empty;
        }

        var metadata = informationalVersion.IndexOf('+');
        return metadata < 0 ? informationalVersion : informationalVersion[..metadata];
    }
}
