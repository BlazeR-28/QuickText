using System.Windows;

namespace QuickText;

public partial class App : Application
{
#if DEBUG
    private const string MutexName = "QuickText.SingleInstance.Dev";
    private const string ShowEventName = "QuickText.Show.Dev";
#else
    private const string MutexName = "QuickText.SingleInstance";
    private const string ShowEventName = "QuickText.Show";
#endif

    private Mutex? _mutex;
    private EventWaitHandle? _showEvent;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new Mutex(true, MutexName, out bool isFirst);
        if (!isFirst)
        {
            // Another instance is running: ask it to show itself instead of opening a second window.
            try
            {
                NativeMethods.AllowSetForegroundWindow(-1);
                EventWaitHandle.OpenExisting(ShowEventName).Set();
            }
            catch { /* the other instance is shutting down */ }
            Shutdown();
            return;
        }

        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        new Thread(() =>
        {
            while (_showEvent.WaitOne())
                Dispatcher.BeginInvoke(() => (MainWindow as MainWindow)?.ShowAndActivate());
        })
        { IsBackground = true }.Start();

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mutex?.ReleaseMutex();
        base.OnExit(e);
    }
}
