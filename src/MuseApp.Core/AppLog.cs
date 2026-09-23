using System.Globalization;

namespace MuseApp.Core;

/// <summary>
/// Tiny append-only diagnostic log with one-step rolling: when muse.log would pass
/// <c>maxBytes</c> it becomes muse.1.log (replacing the previous one), so at most two
/// files exist. Never throws: logging must not be the thing that crashes the app.
/// Callers must only pass log-safe text (see <see cref="UrlPolicy.Describe(Uri?)"/>).
/// </summary>
public sealed class AppLog(string directory, long maxBytes = AppLog.DefaultMaxBytes, TimeProvider? time = null)
{
    public const long DefaultMaxBytes = 1024 * 1024;

    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly Lock _gate = new();

    public string FilePath { get; } = Path.Combine(directory, "muse.log");
    public string PreviousFilePath { get; } = Path.Combine(directory, "muse.1.log");

    public void Info(string message) => Write("INFO", message);

    public void Warn(string message) => Write("WARN", message);

    public void Error(string message, Exception? exception = null) =>
        Write("ERROR", exception is null ? message : $"{message}{Environment.NewLine}{exception}");

    private void Write(string level, string message)
    {
        var timestamp = _time.GetUtcNow().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        var line = $"{timestamp} [{level}] {message}{Environment.NewLine}";
        lock (_gate)
        {
            try
            {
                Directory.CreateDirectory(directory);
                var file = new FileInfo(FilePath);
                if (file.Exists && file.Length + line.Length > maxBytes)
                    File.Move(FilePath, PreviousFilePath, overwrite: true);
                File.AppendAllText(FilePath, line);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Diagnostics are best-effort.
            }
        }
    }
}
