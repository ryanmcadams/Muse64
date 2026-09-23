using MuseApp.Core;

namespace MuseApp.Core.Tests;

public class PermissionPolicyTests
{
    [Theory]
    [InlineData(PermissionKind.Microphone, PermissionDecision.Allow)]
    [InlineData(PermissionKind.ClipboardRead, PermissionDecision.Allow)]
    [InlineData(PermissionKind.Notifications, PermissionDecision.Allow)]
    [InlineData(PermissionKind.Autoplay, PermissionDecision.Allow)]
    [InlineData(PermissionKind.Camera, PermissionDecision.Default)]
    [InlineData(PermissionKind.Geolocation, PermissionDecision.Default)]
    [InlineData(PermissionKind.ScreenCapture, PermissionDecision.Default)]
    [InlineData(PermissionKind.Other, PermissionDecision.Deny)]
    public void MuseOrigin(PermissionKind kind, PermissionDecision expected)
    {
        Assert.Equal(expected, PermissionPolicy.Decide("https://muse.ai/", kind));
        Assert.Equal(expected, PermissionPolicy.Decide("https://auth.muse.ai/x", kind));
    }

    [Theory]
    [InlineData(PermissionKind.Microphone, PermissionDecision.Deny)]
    [InlineData(PermissionKind.Camera, PermissionDecision.Deny)]
    [InlineData(PermissionKind.Geolocation, PermissionDecision.Deny)]
    [InlineData(PermissionKind.ScreenCapture, PermissionDecision.Deny)]
    [InlineData(PermissionKind.Notifications, PermissionDecision.Deny)]
    [InlineData(PermissionKind.ClipboardRead, PermissionDecision.Default)]
    [InlineData(PermissionKind.Autoplay, PermissionDecision.Default)]
    [InlineData(PermissionKind.Other, PermissionDecision.Deny)]
    public void OtherOrigins(PermissionKind kind, PermissionDecision expected)
    {
        Assert.Equal(expected, PermissionPolicy.Decide("https://vm.metaaiusercontent.com/", kind));
        Assert.Equal(expected, PermissionPolicy.Decide("https://example.com/", kind));
    }

    [Theory]
    [InlineData("http://muse.ai/")]
    [InlineData("https://evilmuse.ai/")]
    [InlineData("https://muse.ai.evil.com/")]
    [InlineData("https://notmuse.ai/")]
    [InlineData("https://muse.ai@evil.com/")]
    [InlineData("https://muse.ai:443@evil.com/")]
    [InlineData("https://evil.com/muse.ai")]
    [InlineData("https://evil.com/?origin=https://muse.ai")]
    [InlineData("https://meta.ai/")]
    [InlineData("not a url")]
    [InlineData(null)]
    public void NonMuseOriginsNeverGetMicrophone(string? origin) =>
        Assert.Equal(PermissionDecision.Deny, PermissionPolicy.Decide(origin, PermissionKind.Microphone));

    [Fact]
    public void RelativeUriIsNotMuseOrigin() =>
        Assert.False(PermissionPolicy.IsMuseOrigin(new Uri("/x", UriKind.Relative)));
}
