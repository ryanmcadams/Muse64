using MuseApp.Core;

namespace MuseApp.Core.Tests;

public class HotkeyGestureTests
{
    [Theory]
    [InlineData("Ctrl+Alt+M", HotkeyModifiers.Control | HotkeyModifiers.Alt, "M", "Ctrl+Alt+M")]
    [InlineData("ctrl+alt+m", HotkeyModifiers.Control | HotkeyModifiers.Alt, "M", "Ctrl+Alt+M")]
    [InlineData("Control+Shift+M", HotkeyModifiers.Control | HotkeyModifiers.Shift, "M", "Ctrl+Shift+M")]
    [InlineData("Alt Shift 7", HotkeyModifiers.Alt | HotkeyModifiers.Shift, "7", "Alt+Shift+7")]
    [InlineData("  win + f9 ", HotkeyModifiers.Win, "F9", "Win+F9")]
    [InlineData("M+Ctrl", HotkeyModifiers.Control, "M", "Ctrl+M")]
    [InlineData("Ctrl+F1", HotkeyModifiers.Control, "F1", "Ctrl+F1")]
    [InlineData("Ctrl+F24", HotkeyModifiers.Control, "F24", "Ctrl+F24")]
    [InlineData("Ctrl+Alt+Shift+Win+Z", HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift | HotkeyModifiers.Win, "Z", "Ctrl+Alt+Shift+Win+Z")]
    [InlineData("Ctrl+0", HotkeyModifiers.Control, "0", "Ctrl+0")]
    public void ParsesValidCombos(string text, HotkeyModifiers modifiers, string key, string canonical)
    {
        Assert.Equal(HotkeyParseStatus.Valid, HotkeyGesture.TryParse(text, out var gesture));
        Assert.Equal(new HotkeyGesture(modifiers, key), gesture);
        Assert.Equal(canonical, gesture!.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyDisables(string? text)
    {
        Assert.Equal(HotkeyParseStatus.Disabled, HotkeyGesture.TryParse(text, out var gesture));
        Assert.Null(gesture);
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("Ctrl+Alt+")]        // missing key
    [InlineData("Ctrl+Alt")]         // missing key
    [InlineData("M")]                // bare key would be hijacked from every app
    [InlineData("Shift+M")]          // so would Shift+letter (capital M)
    [InlineData("Shift+F5")]
    [InlineData("Ctrl+M+N")]         // two keys
    [InlineData("Ctrl+Ctrl+M")]      // duplicate modifier
    [InlineData("Ctrl+Alt+Esc")]     // key outside the allowed set
    [InlineData("Ctrl+Alt+Space")]
    [InlineData("Ctrl+F0")]
    [InlineData("Ctrl+F25")]
    [InlineData("Ctrl+F01")]
    [InlineData("Ctrl+MM")]
    [InlineData("Ctrl+é")]
    [InlineData("Ctrl-Alt-M")]       // '-' is not a separator
    [InlineData("Cmd+M")]
    public void RejectsInvalid(string text)
    {
        Assert.Equal(HotkeyParseStatus.Invalid, HotkeyGesture.TryParse(text, out var gesture));
        Assert.Null(gesture);
    }

    [Fact]
    public void DefaultTextParsesToDefault()
    {
        Assert.Equal(HotkeyParseStatus.Valid, HotkeyGesture.TryParse(HotkeyGesture.DefaultText, out var gesture));
        Assert.Equal(HotkeyGesture.Default, gesture);
        Assert.Equal(HotkeyGesture.DefaultText, new AppSettings().Hotkey);
    }
}
