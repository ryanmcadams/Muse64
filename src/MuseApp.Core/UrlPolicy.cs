namespace MuseApp.Core;

/// <summary>What to do with a top-level navigation in a Muse window.</summary>
public enum TopLevelDecision
{
    /// <summary>Navigate normally.</summary>
    Allow,

    /// <summary>Navigate, but tell the user they have left Muse (https host outside the trusted set).</summary>
    AllowOffSite,

    /// <summary>Cancel the navigation.</summary>
    Block,
}

/// <summary>Where a <c>window.open</c> / <c>target=_blank</c> request should go.</summary>
public enum PopupDecision
{
    /// <summary>Host it in an in-app popup window that keeps <c>window.opener</c> (OAuth, login).</summary>
    InApp,

    /// <summary>Hand it to the user's default browser.</summary>
    ExternalBrowser,

    /// <summary>Ignore it.</summary>
    Drop,
}

/// <summary>
/// URL decisions for the Muse host. The page can run arbitrary third-party content
/// (the "Secure VM" iframe may call window.open on anything), so nothing reaches
/// ShellExecute or an in-app window without passing through here.
/// </summary>
public static class UrlPolicy
{
    public static readonly Uri Home = new("https://muse.ai");

    // Exact host or any subdomain. Muse/Meta first-party hosts, then identity and
    // payment providers whose popups must keep window.opener to hand results back.
    private static readonly string[] TrustedHosts =
    [
        "muse.ai",
        "meta.ai",
        "meta.com",
        "facebook.com",
        "fb.com",
        "instagram.com",
        "fbcdn.net",
        "fbsbx.com",
        "metaaiusercontent.com",
        "ecto1usercontent.com",
        "meta-agents-apps.workers.dev",
        "google.com",
        "login.microsoftonline.com",
        "login.live.com",
        "appleid.apple.com",
        "stripe.com",
        "link.com",
    ];

    public static bool IsTrustedHost(string? host)
    {
        if (string.IsNullOrEmpty(host))
            return false;

        var normalized = host.TrimEnd('.');
        foreach (var trusted in TrustedHosts)
        {
            if (normalized.Equals(trusted, StringComparison.OrdinalIgnoreCase) ||
                normalized.EndsWith("." + trusted, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>True for an absolute https URL whose host is trusted.</summary>
    public static bool IsTrusted(Uri? uri) =>
        uri is { IsAbsoluteUri: true } && uri.Scheme == Uri.UriSchemeHttps && IsTrustedHost(uri.IdnHost);

    /// <summary>
    /// Top-level navigation: any https is allowed because connector OAuth may redirect
    /// to arbitrary providers; leaving the trusted set is surfaced, not blocked.
    /// Everything else (http, file, data, javascript, blob, custom schemes) is blocked.
    /// </summary>
    public static TopLevelDecision ClassifyTopLevel(Uri? uri)
    {
        if (uri is null || !uri.IsAbsoluteUri)
            return TopLevelDecision.Block;
        if (IsAboutBlank(uri))
            return TopLevelDecision.Allow;
        if (uri.Scheme != Uri.UriSchemeHttps || string.IsNullOrEmpty(uri.Host))
            return TopLevelDecision.Block;
        return IsTrustedHost(uri.IdnHost) ? TopLevelDecision.Allow : TopLevelDecision.AllowOffSite;
    }

    /// <summary>Parses a raw WebView2 URI string, then classifies it.</summary>
    public static TopLevelDecision ClassifyTopLevel(string? rawUri) =>
        ClassifyTopLevel(TryParse(rawUri));

    /// <summary>
    /// Popups: blank or trusted host → in-app (keeps window.opener for OAuth/login);
    /// other https → default browser, but only when the user actually clicked, so
    /// embedded third-party content cannot spam the browser with tabs; anything else → dropped.
    /// A null URI means "window.open() with no URL", which the opener fills in later.
    /// </summary>
    public static PopupDecision ClassifyPopup(Uri? uri, bool isUserInitiated)
    {
        if (uri is null)
            return PopupDecision.InApp;
        if (!uri.IsAbsoluteUri)
            return PopupDecision.Drop;
        if (IsAboutBlank(uri) || IsTrusted(uri))
            return PopupDecision.InApp;
        if (isUserInitiated && uri.Scheme == Uri.UriSchemeHttps && IsSafeToShellExecute(uri))
            return PopupDecision.ExternalBrowser;
        return PopupDecision.Drop;
    }

    /// <summary>Parses a raw WebView2 popup URI (empty = blank popup), then classifies it.</summary>
    public static PopupDecision ClassifyPopup(string? rawUri, bool isUserInitiated)
    {
        if (string.IsNullOrEmpty(rawUri))
            return ClassifyPopup((Uri?)null, isUserInitiated);
        var uri = TryParse(rawUri);
        return uri is null ? PopupDecision.Drop : ClassifyPopup(uri, isUserInitiated);
    }

    /// <summary>
    /// Only plain web URLs may be handed to the shell. Anything else (file:, ms-*:,
    /// search-ms:, custom protocol handlers) could launch local programs.
    /// </summary>
    public static bool IsSafeToShellExecute(Uri? uri) =>
        uri is { IsAbsoluteUri: true } &&
        (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) &&
        !string.IsNullOrEmpty(uri.Host) &&
        string.IsNullOrEmpty(uri.UserInfo);

    /// <summary>
    /// External protocol launches that may reach WebView2's own confirmation dialog
    /// (LaunchingExternalUriScheme). Everything else is cancelled silently.
    /// </summary>
    public static bool IsAllowedExternalScheme(Uri? uri) =>
        uri is { IsAbsoluteUri: true } &&
        (uri.Scheme == Uri.UriSchemeMailto || uri.Scheme.Equals("tel", StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc cref="IsAllowedExternalScheme(Uri?)"/>
    public static bool IsAllowedExternalScheme(string? rawUri) => IsAllowedExternalScheme(TryParse(rawUri));

    /// <summary>
    /// Log-safe form of a URL: scheme and host only. Paths and query strings are
    /// never logged because OAuth redirects carry codes and tokens in them.
    /// </summary>
    public static string Describe(Uri? uri)
    {
        if (uri is null)
            return "(none)";
        if (!uri.IsAbsoluteUri)
            return "(relative)";
        // Only web URLs have a meaningful host; for mailto:/tel:/custom schemes even the
        // "host" can be personal data (an email domain), so just the scheme is logged.
        var isWeb = uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp;
        return isWeb ? $"{uri.Scheme}://{uri.Host}" : uri.Scheme + ":";
    }

    /// <inheritdoc cref="Describe(Uri?)"/>
    public static string Describe(string? rawUri)
    {
        if (string.IsNullOrEmpty(rawUri))
            return "(none)";
        return TryParse(rawUri) is { } uri ? Describe(uri) : "(unparseable)";
    }

    public static Uri? TryParse(string? rawUri) =>
        Uri.TryCreate(rawUri, UriKind.Absolute, out var uri) ? uri : null;

    private static bool IsAboutBlank(Uri uri) =>
        uri.Scheme == "about" && uri.AbsolutePath.Equals("blank", StringComparison.OrdinalIgnoreCase);
}
