namespace MuseApp.Core;

/// <summary>Permission kinds the host decides on (a WebView2-independent mirror of its enum).</summary>
public enum PermissionKind
{
    Microphone,
    Camera,
    Geolocation,
    ScreenCapture,
    Notifications,
    ClipboardRead,
    Autoplay,

    /// <summary>Anything else (sensors, MIDI, file system, fonts, window management, ...).</summary>
    Other,
}

public enum PermissionDecision
{
    /// <summary>Let WebView2 prompt the user (and remember the answer in the profile).</summary>
    Default,
    Allow,
    Deny,
}

/// <summary>
/// Site permission decisions. Muse itself gets what voice dictation and copy/paste need
/// without prompts; camera, location and screen capture still prompt. Everyone else
/// (third-party popups, the Secure VM iframe, embedded apps) never gets devices or notifications.
/// </summary>
public static class PermissionPolicy
{
    public static PermissionDecision Decide(Uri? origin, PermissionKind kind)
    {
        if (IsMuseOrigin(origin))
        {
            return kind switch
            {
                PermissionKind.Microphone or PermissionKind.ClipboardRead or
                PermissionKind.Notifications or PermissionKind.Autoplay => PermissionDecision.Allow,
                PermissionKind.Camera or PermissionKind.Geolocation or
                PermissionKind.ScreenCapture => PermissionDecision.Default,
                _ => PermissionDecision.Deny,
            };
        }

        return kind switch
        {
            // Low-risk and gesture-gated by Chromium: leave WebView2's normal behavior.
            PermissionKind.ClipboardRead or PermissionKind.Autoplay => PermissionDecision.Default,
            _ => PermissionDecision.Deny,
        };
    }

    /// <inheritdoc cref="Decide(Uri?, PermissionKind)"/>
    public static PermissionDecision Decide(string? origin, PermissionKind kind) =>
        Decide(UrlPolicy.TryParse(origin), kind);

    /// <summary>https://muse.ai or any https subdomain of it.</summary>
    public static bool IsMuseOrigin(Uri? origin) =>
        origin is { IsAbsoluteUri: true } &&
        origin.Scheme == Uri.UriSchemeHttps &&
        (origin.IdnHost.Equals("muse.ai", StringComparison.OrdinalIgnoreCase) ||
         origin.IdnHost.EndsWith(".muse.ai", StringComparison.OrdinalIgnoreCase));
}
