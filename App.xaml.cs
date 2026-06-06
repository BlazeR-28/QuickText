using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace QuickText;

public partial class App : Application
{
    private static Mutex? _mutex;
    private const string MutexName = "QuickText-SingleInstance-Mutex";

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private const int SW_RESTORE = 9;

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, MutexName, out bool createdNew);

        if (!createdNew)
        {
            // Focus the existing process window
            Process current = Process.GetCurrentProcess();
            foreach (Process process in Process.GetProcessesByName(current.ProcessName))
            {
                if (process.Id != current.Id)
                {
                    IntPtr handle = process.MainWindowHandle;
                    if (handle != IntPtr.Zero)
                    {
                        ShowWindow(handle, SW_RESTORE);
                        SetForegroundWindow(handle);
                    }
                    break;
                }
            }
            Shutdown();
            return;
        }

        base.OnStartup(e);
    }
}

