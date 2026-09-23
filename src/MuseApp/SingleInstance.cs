using MuseApp.Native;

namespace MuseApp;

/// <summary>
/// One Muse per user session: a second launch signals the first (which comes to the
/// front) and exits. Two instances would fight over the same WebView2 profile folder.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\MuseForWindows";
    private const string ActivateEventName = @"Local\MuseForWindows.Activate";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activate;
    private RegisteredWaitHandle? _registration;

    private SingleInstance(Mutex mutex, EventWaitHandle activate)
    {
        _mutex = mutex;
        _activate = activate;
    }

    /// <summary>Returns the guard if this is the first instance, otherwise null.</summary>
    public static SingleInstance? TryAcquire()
    {
        var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (!createdNew)
        {
            mutex.Dispose();
            return null;
        }
        return new SingleInstance(mutex, new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName));
    }

    /// <summary>Asks the running instance to show its window.</summary>
    public static void SignalExisting()
    {
        Windowing.AllowAnyProcessToSetForeground();
        if (EventWaitHandle.TryOpenExisting(ActivateEventName, out var activate))
        {
            using (activate)
                activate.Set();
        }
    }

    /// <summary>Invokes <paramref name="onActivate"/> (on a pool thread) whenever another launch signals.</summary>
    public void ListenForActivation(Action onActivate) =>
        _registration = ThreadPool.RegisterWaitForSingleObject(
            _activate, (_, _) => onActivate(), null, Timeout.Infinite, executeOnlyOnce: false);

    public void Dispose()
    {
        _registration?.Unregister(null);
        _activate.Dispose();
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
