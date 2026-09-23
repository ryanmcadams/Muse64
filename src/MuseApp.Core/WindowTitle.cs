namespace MuseApp.Core;

/// <summary>Main window title derived from the page's document title.</summary>
public static class WindowTitle
{
    public const string AppName = "Muse";

    /// <summary>
    /// "&lt;title&gt; — Muse", or the page title alone when it already names Muse
    /// (avoids "Muse — Your Personal AI Agent — Muse"), or "Muse" when empty.
    /// </summary>
    public static string Compose(string? documentTitle)
    {
        var title = documentTitle?.Trim();
        if (string.IsNullOrEmpty(title))
            return AppName;
        return title.Contains(AppName, StringComparison.OrdinalIgnoreCase) ? title : $"{title} — {AppName}";
    }

    /// <summary>
    /// In-app popup title: "&lt;host&gt; — &lt;title&gt;". Popups have no address bar and the page
    /// controls its own title, so the real host comes FIRST (a long or padded title cannot push
    /// it out of the end-truncated title bar) and is shown in punycode (no look-alike hosts).
    /// A non-web page such as a script-written about:blank popup is labeled "about:blank".
    /// </summary>
    public static string ComposePopup(string? documentTitle, string? source)
    {
        var title = documentTitle?.Trim();
        var uri = UrlPolicy.TryParse(source);
        string? location = uri switch
        {
            null => null,
            _ when uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp => uri.IdnHost,
            _ => $"{uri.Scheme}:{uri.AbsolutePath}",
        };

        if (string.IsNullOrEmpty(location))
            return string.IsNullOrEmpty(title) ? AppName : title;
        return string.IsNullOrEmpty(title) ? location : $"{location} — {title}";
    }
}
