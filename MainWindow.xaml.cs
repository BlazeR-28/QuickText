using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;

namespace QuickText;

public partial class MainWindow : Window
{
    private const int GlobalHotkeyId = 9000;
    private const double SettingsMinHeight = 500;
    private const int MaxPills = 6;

    private static readonly Regex UrlRegex = new(@"https?://[^\s]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly AppSettings _s = Storage.LoadSettings();
    private readonly GlobalHotkey _global = new();
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private readonly Dictionary<Button, int> _flashTokens = new();
    private readonly List<Button> _hotkeyButtons;

    private Gesture? _copyKey, _closeKey, _exitKey, _globalKey;
    private string? _globalError;
    private List<(int Start, int Length, string Url)> _links = new();

    private IntPtr _hwnd;
    private TrayIcon? _tray;
    private double _baseHeight;
    private bool _noteDirty, _settingsDirty, _settingsOpen, _loadingUi;
    private Button? _recording;

    public MainWindow()
    {
        InitializeComponent();
        _hotkeyButtons = new() { CopyKeyBtn, CloseKeyBtn, ExitKeyBtn, GlobalKeyBtn };

        Width = Math.Max(MinWidth, _s.Width);
        _baseHeight = Math.Max(MinHeight, _s.Height);
        Height = _baseHeight;
        if (_s.Left is double left && _s.Top is double top)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = left;
            Top = top;
        }

        // WPF's spell checker follows the element language, which defaults to en-US.
        Pad.Language = XmlLanguage.GetLanguage(CultureInfo.CurrentUICulture.IetfLanguageTag);

        if (_s.Autosave) Pad.Text = Storage.LoadNote();
        Pad.CaretIndex = Pad.Text.Length;

        AboutLabel.Text = $"QuickText v{typeof(MainWindow).Assembly.GetName().Version?.ToString(3)}  -  made by BlazeR";

        ParseLocalHotkeys();

        Pad.TextChanged += (_, _) => OnTextChanged();
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); FlushSave(); };

        SettingsBtn.Click += (_, _) => { if (_settingsOpen) CloseSettings(); else OpenSettings(); };
        DoneBtn.Click += (_, _) => CloseSettings();
        CloseBtn.Click += (_, _) => HideWindow();
        ExitAppBtn.Click += (_, _) => Close();
        CopyBtn.Click += (_, _) => DoCopy();
        ClearBtn.Click += (_, _) => DoClear();

        AutosaveSwitch.Click += (_, _) => OnAutosaveChanged();
        OpacitySlider.ValueChanged += (_, e) => OnOpacityChanged(e.NewValue);

        foreach (var b in _hotkeyButtons)
        {
            b.Click += (_, _) => StartRecording(b);
            b.LostKeyboardFocus += (_, _) => { if (_recording == b) StopRecording(); };
        }

        RebuildLinks();
        Loaded += (_, _) => { UpdateHeight(); Pad.Focus(); };
    }

    // ---------- Window plumbing ----------

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;

        int round = NativeMethods.DWMWCP_ROUND;
        NativeMethods.DwmSetWindowAttribute(_hwnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));
        int borderColor = 0x002F3538; // COLORREF (BGR) of LineBrush #38352F: a faint line instead of the default accent border
        NativeMethods.DwmSetWindowAttribute(_hwnd, NativeMethods.DWMWA_BORDER_COLOR, ref borderColor, sizeof(int));

        HwndSource.FromHwnd(_hwnd)?.AddHook(WndProc);

        NativeMethods.SetWindowOpacity(_hwnd, _s.Opacity);
        RegisterGlobalFromSettings();

        _tray = new TrayIcon(_hwnd, ShowAndActivate, () => Close());
        _tray.Add();
        RefreshHints();

        KeepOnScreen();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (_tray != null && _tray.TryHandle(msg, lParam))
        {
            handled = true;
            return IntPtr.Zero;
        }

        switch (msg)
        {
            case NativeMethods.WM_HOTKEY when wParam.ToInt64() == GlobalHotkeyId:
                Toggle();
                handled = true;
                break;

            // WPF strips WS_EX_LAYERED again on style changes; put it back so opacity keeps working.
            case NativeMethods.WM_STYLECHANGING when wParam.ToInt64() is NativeMethods.GWL_EXSTYLE or 4294967276L:
                var ss = Marshal.PtrToStructure<NativeMethods.STYLESTRUCT>(lParam);
                ss.styleNew |= NativeMethods.WS_EX_LAYERED;
                Marshal.StructureToPtr(ss, lParam, false);
                handled = true;
                break;

            case NativeMethods.WM_EXITSIZEMOVE:
                OnUserMovedOrResized();
                break;
        }
        return IntPtr.Zero;
    }

    public void ShowAndActivate()
    {
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
        Pad.Focus();
    }

    private void Toggle()
    {
        if (IsVisible && IsActive) Hide();
        else ShowAndActivate();
    }

    /// <summary>Hiding is always safe: the tray icon (and usually the global shortcut) brings the window back.</summary>
    private void HideWindow() => Hide();

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);
        CaptureGeometry();
        _noteDirty = _s.Autosave;
        _settingsDirty = true;
        FlushSave();
        _global.Unregister();
        _tray?.Dispose();
    }

    /// <summary>Keeps every place that mentions the shortcuts (hint, tooltip, tray) in sync.</summary>
    private void RefreshHints()
    {
        string? hide = _closeKey?.Pretty;
        string? show = _globalKey?.Pretty;

        CloseBtn.ToolTip = hide != null ? $"Hide to tray ({hide})" : "Hide to tray";

        string hidePart = hide != null ? $"{hide} hides this window" : "The button at the top right hides this window";
        string showPart = show != null ? $"{show} or the tray icon brings it back" : "the tray icon brings it back";
        HintLabel.Text = $"{hidePart} - {showPart}.";

        if (_tray != null)
        {
            _tray.ShortcutText = show ?? "";
            _tray.SetTip(show != null ? $"QuickText - {show} opens it" : "QuickText - click to open");
        }
    }

    // ---------- Geometry ----------

    private Rect WorkAreaDip()
    {
        IntPtr monitor = NativeMethods.MonitorFromWindow(_hwnd, 2 /* nearest */);
        var info = new NativeMethods.MONITORINFO { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>() };
        if (monitor == IntPtr.Zero || !NativeMethods.GetMonitorInfo(monitor, ref info))
            return SystemParameters.WorkArea;

        DpiScale dpi = VisualTreeHelper.GetDpi(this);
        var r = info.rcWork;
        return new Rect(r.left / dpi.DpiScaleX, r.top / dpi.DpiScaleY,
                        (r.right - r.left) / dpi.DpiScaleX, (r.bottom - r.top) / dpi.DpiScaleY);
    }

    private void KeepOnScreen()
    {
        Rect wa = WorkAreaDip();
        Width = Math.Min(Width, wa.Width);
        Height = Math.Min(Height, wa.Height);
        Left = Math.Max(wa.Left, Math.Min(Left, wa.Right - Width));
        Top = Math.Max(wa.Top, Math.Min(Top, wa.Bottom - Height));
    }

    private void CaptureGeometry()
    {
        _s.Left = Left;
        _s.Top = Top;
        _s.Width = ActualWidth > 0 ? ActualWidth : Width;
        _s.Height = _baseHeight;
    }

    private void OnUserMovedOrResized()
    {
        if (!_settingsOpen) _baseHeight = ActualHeight;
        CaptureGeometry();
        MarkSettingsDirty();
    }

    /// <summary>Grows the window with the text up to 85% of the screen, and shrinks back to the user's own height.</summary>
    private void UpdateHeight()
    {
        if (!IsLoaded) return;

        Pad.UpdateLayout();
        Rect wa = WorkAreaDip();

        double chrome = ActualHeight - Pad.ViewportHeight;
        double needed = Pad.ExtentHeight + chrome;
        double max = Math.Max(_baseHeight, wa.Height * 0.85);
        double target = Math.Clamp(needed, _baseHeight, max);
        if (_settingsOpen) target = Math.Max(target, Math.Min(SettingsMinHeight, wa.Height));

        if (Math.Abs(target - ActualHeight) < 1) return;

        Height = target;
        Top = Math.Max(wa.Top, Math.Min(Top, wa.Bottom - target));
    }

    // ---------- Editor ----------

    private void OnTextChanged()
    {
        RebuildLinks();
        UpdateHeight();
        if (_s.Autosave)
        {
            _noteDirty = true;
            if (!_saveTimer.IsEnabled) _saveTimer.Start();
        }
    }

    private void DoCopy()
    {
        if (Pad.Text.Length == 0) return;
        try
        {
            Clipboard.SetDataObject(Pad.Text, true);
            Flash(CopyBtn, "Copy", "Copied", (Brush)FindResource("SuccessBrush"));
        }
        catch (COMException)
        {
            Flash(CopyBtn, "Copy", "Failed", (Brush)FindResource("DangerBrush"));
        }
    }

    private void DoClear()
    {
        // Replacing the selection keeps the undo history, so Ctrl+Z restores the note.
        Pad.SelectAll();
        Pad.SelectedText = "";
        Pad.Focus();
        Flash(ClearBtn, "Clear", "Cleared", (Brush)FindResource("SuccessBrush"));
    }

    private async void Flash(Button button, string original, string text, Brush color)
    {
        int token = _flashTokens[button] = _flashTokens.GetValueOrDefault(button) + 1;
        button.Content = text;
        button.Foreground = color;
        button.BorderBrush = color;

        await Task.Delay(1200);

        if (_flashTokens[button] != token) return;
        button.Content = original;
        button.ClearValue(Control.ForegroundProperty);
        button.ClearValue(Control.BorderBrushProperty);
    }

    // ---------- Link pills ----------

    private void RebuildLinks()
    {
        var found = new List<(int, int, string)>();
        foreach (Match m in UrlRegex.Matches(Pad.Text))
        {
            string url = TrimUrl(m.Value);
            if (url.Length > "https://".Length) found.Add((m.Index, url.Length, url));
        }

        bool sameUrls = found.Count == _links.Count && found.Select(f => f.Item3).SequenceEqual(_links.Select(l => l.Url));
        _links = found;
        if (sameUrls) return; // positions are looked up on hover, so nothing to redraw

        PillPanel.Children.Clear();
        for (int i = 0; i < Math.Min(found.Count, MaxPills); i++)
        {
            int index = i;
            string label = Regex.Replace(found[i].Item3, @"^https?://(www\.)?", "");
            if (label.Length > 22) label = label[..20] + "...";

            var pill = new Button
            {
                Style = (Style)FindResource("PillButton"),
                Content = $"{i + 1}  {label}",
                ToolTip = found[i].Item3
            };

            int savedStart = 0, savedLength = 0;
            pill.MouseEnter += (_, _) =>
            {
                if (index >= _links.Count) return;
                savedStart = Pad.SelectionStart;
                savedLength = Pad.SelectionLength;
                Pad.Select(_links[index].Start, _links[index].Length);
            };
            pill.MouseLeave += (_, _) =>
            {
                if (savedStart + savedLength <= Pad.Text.Length) Pad.Select(savedStart, savedLength);
            };
            pill.Click += (_, _) => { if (index < _links.Count) OpenUrl(_links[index].Url); };
            PillPanel.Children.Add(pill);
        }

        if (found.Count > MaxPills)
        {
            PillPanel.Children.Add(new TextBlock
            {
                Text = $"+{found.Count - MaxPills} more",
                FontSize = 11,
                Foreground = (Brush)FindResource("MutedBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 0, 4)
            });
        }
    }

    private static string TrimUrl(string url)
    {
        while (url.Length > 0)
        {
            char last = url[^1];
            bool unbalancedCloser = last is ')' or ']' or '}' &&
                url.Count(c => c == last) > url.Count(c => c == Opener(last));
            if (".,;:!?'\"".Contains(last) || unbalancedCloser) url = url[..^1];
            else break;
        }
        return url;
    }

    private static char Opener(char closer) => closer switch { ')' => '(', ']' => '[', _ => '{' };

    private static void OpenUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || uri.Scheme is not ("http" or "https")) return;
        try { Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); }
        catch (Exception ex) { Debug.WriteLine($"Failed to open URL: {ex.Message}"); }
    }

    // ---------- Persistence ----------

    private void MarkSettingsDirty()
    {
        _settingsDirty = true;
        if (!_saveTimer.IsEnabled) _saveTimer.Start();
    }

    private void FlushSave()
    {
        if (_noteDirty && _s.Autosave)
        {
            Storage.SaveNote(Pad.Text);
            _noteDirty = false;
        }
        if (_settingsDirty)
        {
            Storage.SaveSettings(_s);
            _settingsDirty = false;
        }
    }

    // ---------- Settings panel ----------

    private void OpenSettings()
    {
        _loadingUi = true;
        AutosaveSwitch.IsChecked = _s.Autosave;
        OpacitySlider.Value = PercentToPosition(_s.Opacity * 100);
        OpacityLabel.Text = $"{Math.Round(_s.Opacity * 100)}%";
        _loadingUi = false;

        RefreshHotkeyButtons();
        HotkeyError.Text = _globalError ?? "";

        SettingsPanel.Visibility = Visibility.Visible;
        _settingsOpen = true;
        UpdateHeight();
    }

    private void CloseSettings()
    {
        StopRecording();
        SettingsPanel.Visibility = Visibility.Collapsed;
        _settingsOpen = false;
        UpdateHeight();
        Pad.Focus();
    }

    private void OnAutosaveChanged()
    {
        _s.Autosave = AutosaveSwitch.IsChecked == true;
        if (_s.Autosave) _noteDirty = true;
        else Storage.DeleteNote();
        MarkSettingsDirty();
    }

    // The bar runs from 20% (left) to 100% (right) with 50% exactly in the middle:
    // the left half covers 20-50, the right half 50-100. Slider position is 0-100.
    private static double PositionToPercent(double pos) => pos < 50 ? 20 + pos * 0.6 : pos;

    private static double PercentToPosition(double percent) => percent < 50 ? (percent - 20) / 0.6 : percent;

    private void OnOpacityChanged(double position)
    {
        if (_loadingUi) return;

        double percent = PositionToPercent(position);
        _s.Opacity = percent / 100;
        OpacityLabel.Text = $"{Math.Round(percent)}%";
        NativeMethods.SetWindowOpacity(_hwnd, _s.Opacity);
        MarkSettingsDirty();
    }

    // ---------- Shortcuts ----------

    private string GetKey(string id) => id switch
    {
        "Copy" => _s.CopyHotkey,
        "Close" => _s.CloseHotkey,
        "Exit" => _s.ExitHotkey,
        _ => _s.GlobalHotkey
    };

    private void SetKey(string id, string value)
    {
        switch (id)
        {
            case "Copy": _s.CopyHotkey = value; break;
            case "Close": _s.CloseHotkey = value; break;
            case "Exit": _s.ExitHotkey = value; break;
            default: _s.GlobalHotkey = value; break;
        }
    }

    private void ParseLocalHotkeys()
    {
        _copyKey = Gesture.TryParse(_s.CopyHotkey, out var c) ? c : null;
        _closeKey = Gesture.TryParse(_s.CloseHotkey, out var cl) ? cl : null;
        _exitKey = Gesture.TryParse(_s.ExitHotkey, out var x) ? x : null;
    }

    private void RegisterGlobalFromSettings()
    {
        if (!Gesture.TryParse(_s.GlobalHotkey, out var g)) return;

        if (_global.TryRegister(_hwnd, GlobalHotkeyId, g, out string? error)) _globalKey = g;
        else _globalError = $"The global shortcut {g} is not active. {error}";
    }

    private void RefreshHotkeyButtons()
    {
        foreach (var b in _hotkeyButtons)
            b.Content = b == _recording ? "Press keys..." : Gesture.Display(GetKey((string)b.Tag));
    }

    private void StartRecording(Button button)
    {
        _recording = button;
        HotkeyError.Text = "";
        RefreshHotkeyButtons();
        button.Focus();
    }

    private void StopRecording()
    {
        if (_recording == null) return;
        _recording = null;
        RefreshHotkeyButtons();
        if (_settingsOpen) SettingsPanel.Focus();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        if (_recording != null)
        {
            e.Handled = true;
            if (e.Key is Key.Back or Key.Delete && Keyboard.Modifiers == ModifierKeys.None)
            {
                AssignHotkey(null);
                return;
            }
            if (Gesture.FromEvent(e) is Gesture recorded) AssignHotkey(recorded);
            return;
        }

        if (Gesture.FromEvent(e) is not Gesture pressed) return;

        if (pressed == _exitKey)
        {
            e.Handled = true;
            Close();
        }
        else if (pressed == _copyKey)
        {
            e.Handled = true;
            DoCopy();
        }
        else if (pressed == _closeKey)
        {
            e.Handled = true;
            if (_settingsOpen) CloseSettings();
            else HideWindow();
        }
    }

    private void AssignHotkey(Gesture? gesture)
    {
        string id = (string)_recording!.Tag;
        HotkeyError.Text = "";

        if (id == "Global")
        {
            if (gesture is Gesture g)
            {
                if (!_global.TryRegister(_hwnd, GlobalHotkeyId, g, out string? error))
                {
                    HotkeyError.Text = error ?? "";
                    if (_globalKey is Gesture previous) _global.TryRegister(_hwnd, GlobalHotkeyId, previous, out _);
                    StopRecording();
                    return;
                }
                _globalKey = g;
            }
            else
            {
                _global.Unregister();
                _globalKey = null;
            }
            _globalError = null;
        }
        else if (gesture is Gesture g && IsUsedLocally(g, id))
        {
            HotkeyError.Text = "That shortcut is already used by another action.";
            StopRecording();
            return;
        }

        SetKey(id, gesture?.ToString() ?? Gesture.NotSet);
        ParseLocalHotkeys();
        RefreshHints();
        MarkSettingsDirty();
        StopRecording();
    }

    private bool IsUsedLocally(Gesture g, string exceptId)
    {
        foreach (string other in new[] { "Copy", "Close", "Exit" })
            if (other != exceptId && Gesture.TryParse(GetKey(other), out var existing) && existing == g)
                return true;
        return false;
    }
}
