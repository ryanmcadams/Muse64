using MuseApp.Core;

namespace MuseApp.Core.Tests;

public class UrlPolicyTests
{
    [Theory]
    [InlineData("muse.ai")]
    [InlineData("auth.muse.ai")]
    [InlineData("MUSE.AI")]
    [InlineData("muse.ai.")]
    [InlineData("www.meta.ai")]
    [InlineData("meta.com")]
    [InlineData("accountscenter.facebook.com")]
    [InlineData("fb.com")]
    [InlineData("www.instagram.com")]
    [InlineData("scontent.xx.fbcdn.net")]
    [InlineData("lookaside.fbsbx.com")]
    [InlineData("x.metaaiusercontent.com")]
    [InlineData("y.ecto1usercontent.com")]
    [InlineData("app.meta-agents-apps.workers.dev")]
    [InlineData("accounts.google.com")]
    [InlineData("google.com")]
    [InlineData("login.microsoftonline.com")]
    [InlineData("login.live.com")]
    [InlineData("appleid.apple.com")]
    [InlineData("checkout.stripe.com")]
    [InlineData("js.stripe.com")]
    [InlineData("link.com")]
    public void TrustedHostsAreRecognized(string host) => Assert.True(UrlPolicy.IsTrustedHost(host));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("evilmuse.ai")]
    [InlineData("muse.ai.evil.com")]
    [InlineData("example.com")]
    [InlineData("apple.com")]
    [InlineData("microsoftonline.com")]
    [InlineData("other.workers.dev")]
    [InlineData("notgoogle.com")]
    [InlineData("notmuse.ai")]
    [InlineData("muse.ai.evil.com.")]
    public void UntrustedHostsAreRejected(string? host) => Assert.False(UrlPolicy.IsTrustedHost(host));

    [Theory]
    [InlineData("https://muse.ai", TopLevelDecision.Allow)]
    [InlineData("https://auth.muse.ai/aymh/?origin=x", TopLevelDecision.Allow)]
    [InlineData("https://accounts.google.com/o/oauth2", TopLevelDecision.Allow)]
    [InlineData("about:blank", TopLevelDecision.Allow)]
    [InlineData("https://www.opentable.com/oauth", TopLevelDecision.AllowOffSite)]
    [InlineData("https://muse.ai@evil.example/", TopLevelDecision.AllowOffSite)]
    [InlineData("http://muse.ai", TopLevelDecision.Block)]
    [InlineData("file:///C:/Windows/win.ini", TopLevelDecision.Block)]
    [InlineData("data:text/html,hi", TopLevelDecision.Block)]
    [InlineData("javascript:alert(1)", TopLevelDecision.Block)]
    [InlineData("blob:https://muse.ai/1234", TopLevelDecision.Block)]
    [InlineData("ms-settings:privacy", TopLevelDecision.Block)]
    [InlineData("about:settings", TopLevelDecision.Block)]
    [InlineData("not a url", TopLevelDecision.Block)]
    [InlineData("", TopLevelDecision.Block)]
    [InlineData(null, TopLevelDecision.Block)]
    public void ClassifyTopLevel(string? url, TopLevelDecision expected) =>
        Assert.Equal(expected, UrlPolicy.ClassifyTopLevel(url));

    [Fact]
    public void ClassifyTopLevelBlocksRelativeUri() =>
        Assert.Equal(TopLevelDecision.Block, UrlPolicy.ClassifyTopLevel(new Uri("/relative", UriKind.Relative)));

    [Theory]
    [InlineData(null, false, PopupDecision.InApp)]
    [InlineData("", false, PopupDecision.InApp)]
    [InlineData("about:blank", false, PopupDecision.InApp)]
    [InlineData("https://auth.muse.ai/login", false, PopupDecision.InApp)]
    [InlineData("https://accounts.google.com/o/oauth2/v2/auth?code=secret", true, PopupDecision.InApp)]
    [InlineData("https://checkout.stripe.com/pay", true, PopupDecision.InApp)]
    [InlineData("https://www.example.com/privacy", true, PopupDecision.ExternalBrowser)]
    [InlineData("https://www.example.com/privacy", false, PopupDecision.Drop)]
    [InlineData("https://user:pw@example.com/", true, PopupDecision.Drop)]
    [InlineData("http://example.com/", true, PopupDecision.Drop)]
    [InlineData("http://muse.ai/", true, PopupDecision.Drop)]
    [InlineData("file:///C:/Windows/System32/calc.exe", true, PopupDecision.Drop)]
    [InlineData("search-ms:query=x", true, PopupDecision.Drop)]
    [InlineData("ms-msdt:/id", true, PopupDecision.Drop)]
    [InlineData("mailto:a@b.c", true, PopupDecision.Drop)]
    [InlineData("blob:https://muse.ai/1", true, PopupDecision.Drop)]
    [InlineData("::not a url::", true, PopupDecision.Drop)]
    public void ClassifyPopup(string? url, bool userInitiated, PopupDecision expected) =>
        Assert.Equal(expected, UrlPolicy.ClassifyPopup(url, userInitiated));

    [Fact]
    public void ClassifyPopupNullUriIsBlankPopup() =>
        Assert.Equal(PopupDecision.InApp, UrlPolicy.ClassifyPopup((Uri?)null, isUserInitiated: false));

    [Fact]
    public void ClassifyPopupDropsRelativeUri() =>
        Assert.Equal(PopupDecision.Drop, UrlPolicy.ClassifyPopup(new Uri("x", UriKind.Relative), true));

    [Theory]
    [InlineData("https://example.com/a?b=c", true)]
    [InlineData("http://example.com/", true)]
    [InlineData("https://user@example.com/", false)]
    [InlineData("file:///C:/x", false)]
    [InlineData("file://server/share/x", false)]
    [InlineData("ms-settings:", false)]
    [InlineData("mailto:a@b.c", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("zoommtg://join", false)]
    public void IsSafeToShellExecute(string url, bool expected) =>
        Assert.Equal(expected, UrlPolicy.IsSafeToShellExecute(new Uri(url)));

    [Fact]
    public void IsSafeToShellExecuteRejectsNullAndRelative()
    {
        Assert.False(UrlPolicy.IsSafeToShellExecute(null));
        Assert.False(UrlPolicy.IsSafeToShellExecute(new Uri("/a", UriKind.Relative)));
    }

    [Theory]
    [InlineData("mailto:someone@example.com", true)]
    [InlineData("tel:+15551234567", true)]
    [InlineData("TEL:+15551234567", true)]
    [InlineData("ms-settings:privacy", false)]
    [InlineData("zoommtg://join", false)]
    [InlineData("file:///C:/x", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsAllowedExternalScheme(string? url, bool expected) =>
        Assert.Equal(expected, UrlPolicy.IsAllowedExternalScheme(url));

    [Theory]
    [InlineData("https://auth.muse.ai/aymh/?origin=x&token=secret", "https://auth.muse.ai")]
    [InlineData("mailto:someone@example.com", "mailto:")]
    [InlineData("", "(none)")]
    [InlineData(null, "(none)")]
    [InlineData("%%%", "(unparseable)")]
    public void DescribeNeverIncludesPathOrQuery(string? url, string expected) =>
        Assert.Equal(expected, UrlPolicy.Describe(url));

    [Fact]
    public void DescribeHandlesNullAndRelativeUris()
    {
        Assert.Equal("(none)", UrlPolicy.Describe((Uri?)null));
        Assert.Equal("(relative)", UrlPolicy.Describe(new Uri("/a?b", UriKind.Relative)));
    }

    [Fact]
    public void IsTrustedRequiresHttps()
    {
        Assert.True(UrlPolicy.IsTrusted(new Uri("https://muse.ai")));
        Assert.False(UrlPolicy.IsTrusted(new Uri("http://muse.ai")));
        Assert.False(UrlPolicy.IsTrusted(null));
    }

    [Fact]
    public void HomeIsTrustedMuseRoot() =>
        Assert.Equal(TopLevelDecision.Allow, UrlPolicy.ClassifyTopLevel(UrlPolicy.Home));
}
