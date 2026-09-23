using System.IO;
using System.Security;
using Microsoft.Win32;
using MuseApp.Core;

namespace MuseApp;

/// <summary>
/// "Start with Windows" via the per-user Run key. The registry is the source of truth
/// (not settings) because the user can also change it from Task Manager's Startup page.
/// </summary>
internal static class StartupRegistration
{
    public static bool IsEnabled()
    {
        try
        {
            using var run = Registry.CurrentUser.OpenSubKey(StartupEntry.RunKeyPath);
            using var approved = Registry.CurrentUser.OpenSubKey(StartupEntry.ApprovedKeyPath);
            return StartupEntry.IsEnabled(
                run?.GetValue(StartupEntry.ValueName) as string,
                approved?.GetValue(StartupEntry.ValueName) as byte[]);
        }
        catch (Exception ex) when (ex is SecurityException or IOException or UnauthorizedAccessException)
        {
            App.Log.Warn($"Could not read startup registration: {ex.Message}");
            return false;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        try
        {
            using (var run = Registry.CurrentUser.CreateSubKey(StartupEntry.RunKeyPath))
            {
                if (enabled)
                    run.SetValue(StartupEntry.ValueName, StartupEntry.BuildCommand(Environment.ProcessPath!));
                else
                    run.DeleteValue(StartupEntry.ValueName, throwOnMissingValue: false);
            }

            // Drop any Task Manager "disabled" marker so an explicit opt-in actually takes effect.
            using (var approved = Registry.CurrentUser.OpenSubKey(StartupEntry.ApprovedKeyPath, writable: true))
                approved?.DeleteValue(StartupEntry.ValueName, throwOnMissingValue: false);

            App.Log.Info($"Start with Windows {(enabled ? "enabled" : "disabled")}");
        }
        catch (Exception ex) when (ex is SecurityException or IOException or UnauthorizedAccessException)
        {
            App.Log.Warn($"Could not update startup registration: {ex.Message}");
        }
    }
}
