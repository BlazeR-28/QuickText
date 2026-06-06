using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace QuickText;

public partial class MainWindow : Window
{
    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

    private const int WM_NCLBUTTONDOWN = 0xA1;
    private const int HT_CAPTION = 0x2;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int pvAttribute, int cbAttribute);

    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const int HOTKEY_ID = 9000;
    private const int WM_HOTKEY = 0x0312;

    public class AppSettings
    {
        public bool autosave { get; set; } = true;
        public string copyHotkey { get; set; } = "CTRL+SHIFT+C";
        public string closeHotkey { get; set; } = "ESCAPE";
        public string globalHotkey { get; set; } = "not set";
    }

    private readonly string AppDataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "QuickText"
    );
    private string SettingsPath => Path.Combine(AppDataFolder, "settings.json");
    private string NotePath => Path.Combine(AppDataFolder, "notes.txt");

    public MainWindow()
    {
        InitializeComponent();
        InitializeWebView();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        try
        {
            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            int cornerPreference = DWMWCP_ROUND;
            DwmSetWindowAttribute(helper.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPreference, sizeof(int));

            // Add hook for global hotkeys
            System.Windows.Interop.HwndSource source = System.Windows.Interop.HwndSource.FromHwnd(helper.Handle);
            source.AddHook(HwndHook);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to set DWM corner preference or add hooks: {ex.Message}");
        }

        try
        {
            Uri iconUri = new Uri("pack://application:,,,/Resources/quicktext.ico", UriKind.RelativeOrAbsolute);
            this.Icon = System.Windows.Media.Imaging.BitmapFrame.Create(iconUri);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load window icon: {ex.Message}");
        }
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            ToggleWindowVisibility();
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void ToggleWindowVisibility()
    {
        if (this.Visibility == Visibility.Visible && this.IsActive)
        {
            this.Visibility = Visibility.Hidden;
        }
        else
        {
            this.Visibility = Visibility.Visible;
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
            webView.Focus();
        }
    }

    private void UpdateGlobalHotkey(string hotkeyStr)
    {
        try
        {
            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            IntPtr hWnd = helper.Handle;

            UnregisterHotKey(hWnd, HOTKEY_ID);

            if (string.IsNullOrEmpty(hotkeyStr) || hotkeyStr.Equals("not set", StringComparison.OrdinalIgnoreCase) || hotkeyStr.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            uint modifiers = 0;
            uint vk = 0;

            string[] parts = hotkeyStr.Split('+');
            foreach (string part in parts)
            {
                string p = part.Trim().ToUpper();
                if (p == "CTRL" || p == "CONTROL") modifiers |= 0x0002;
                else if (p == "ALT") modifiers |= 0x0001;
                else if (p == "SHIFT") modifiers |= 0x0004;
                else if (p == "WIN") modifiers |= 0x0008;
                else
                {
                    vk = ParseVirtualKey(p);
                }
            }

            if (vk != 0)
            {
                // Register hotkey (0x4000 = MOD_NOREPEAT)
                bool success = RegisterHotKey(hWnd, HOTKEY_ID, modifiers | 0x4000, vk);
                Debug.WriteLine($"RegisterHotKey for '{hotkeyStr}': {success}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error updating global hotkey: {ex.Message}");
        }
    }

    private uint ParseVirtualKey(string keyName)
    {
        if (keyName.Length == 1)
        {
            char c = keyName[0];
            if (c >= 'A' && c <= 'Z') return (uint)c;
            if (c >= '0' && c <= '9') return (uint)c;
        }

        if (keyName.StartsWith("F") && keyName.Length > 1 && int.TryParse(keyName.Substring(1), out int fNum) && fNum >= 1 && fNum <= 12)
        {
            return (uint)(0x70 + (fNum - 1));
        }

        switch (keyName)
        {
            case "ESCAPE": return 0x1B;
            case "SPACE": return 0x20;
            case "TAB": return 0x09;
            case "ENTER": return 0x0D;
            case "BACKSPACE": return 0x08;
            case "DELETE": return 0x2E;
        }

        return 0;
    }

    private async void InitializeWebView()
    {
        try
        {
            string userDataFolder = Path.Combine(AppDataFolder, "WebView2_Data");
            
            var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
            await webView.EnsureCoreWebView2Async(env);

            webView.WebMessageReceived += WebView_WebMessageReceived;
            LoadHtmlContent();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Initialization Error: {ex.Message}", "QuickText Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadHtmlContent()
    {
        try
        {
            string html = ReadResourceText("QuickText.Resources.index.html");
            string css = ReadResourceText("QuickText.Resources.app.css");
            string js = ReadResourceText("QuickText.Resources.app.js");

            html = html.Replace("<!-- STYLESHEET_PLACEHOLDER -->", $"<style>{css}</style>");
            html = html.Replace("<!-- SCRIPT_PLACEHOLDER -->", $"<script>{js}</script>");

            webView.NavigateToString(html);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load resources: {ex.Message}", "QuickText Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void WebView_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        string messageText = e.WebMessageAsJson;

        if (messageText == "\"drag\"")
        {
            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            ReleaseCapture();
            SendMessage(helper.Handle, WM_NCLBUTTONDOWN, new IntPtr(HT_CAPTION), IntPtr.Zero);
        }
        else if (messageText == "\"close\"")
        {
            this.Close();
        }
        else if (messageText == "\"hide\"")
        {
            this.Visibility = Visibility.Hidden;
        }
        else
        {
            try
            {
                using var doc = JsonDocument.Parse(messageText);
                var root = doc.RootElement;
                if (root.TryGetProperty("type", out var typeProp))
                {
                    string type = typeProp.GetString() ?? "";
                    if (type == "resize")
                    {
                        double width = root.GetProperty("width").GetDouble();
                        double height = root.GetProperty("height").GetDouble();
                        
                        double screenHeight = SystemParameters.WorkArea.Height;
                        this.Height = Math.Min(height, screenHeight);
                        this.Width = width;
                    }
                    else if (type == "open_url")
                    {
                        string url = root.GetProperty("url").GetString() ?? "";
                        if (!string.IsNullOrEmpty(url))
                        {
                            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                        }
                    }
                    else if (type == "copy")
                    {
                        string text = root.GetProperty("text").GetString() ?? "";
                        if (!string.IsNullOrEmpty(text))
                        {
                            Clipboard.SetText(text);
                        }
                    }
                    else if (type == "ready")
                    {
                        if (!Directory.Exists(AppDataFolder)) Directory.CreateDirectory(AppDataFolder);

                        // Load Settings
                        AppSettings settings = new AppSettings();
                        if (File.Exists(SettingsPath))
                        {
                            try
                            {
                                string settingsJson = File.ReadAllText(SettingsPath);
                                var loaded = JsonSerializer.Deserialize<AppSettings>(settingsJson);
                                if (loaded != null) settings = loaded;
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Failed to load settings: {ex.Message}");
                            }
                        }

                        // Register global hotkey
                        UpdateGlobalHotkey(settings.globalHotkey);

                        // Load Saved Text
                        string noteText = "";
                        if (settings.autosave && File.Exists(NotePath))
                        {
                            try
                            {
                                noteText = File.ReadAllText(NotePath);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Failed to load note: {ex.Message}");
                            }
                        }

                        // Post back to WebView
                        var initObj = new { type = "init", settings = settings, noteText = noteText };
                        string initJson = JsonSerializer.Serialize(initObj);
                        webView.CoreWebView2.PostWebMessageAsJson(initJson);
                    }
                    else if (type == "save_settings")
                    {
                        if (root.TryGetProperty("settings", out var settingsProp))
                        {
                            try
                            {
                                if (!Directory.Exists(AppDataFolder)) Directory.CreateDirectory(AppDataFolder);
                                string settingsJson = settingsProp.GetRawText();
                                File.WriteAllText(SettingsPath, settingsJson);

                                // Re-register global hotkey
                                var loaded = JsonSerializer.Deserialize<AppSettings>(settingsJson);
                                if (loaded != null)
                                {
                                    UpdateGlobalHotkey(loaded.globalHotkey);
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Failed to save settings: {ex.Message}");
                            }
                        }
                    }
                    else if (type == "save_note")
                    {
                        if (root.TryGetProperty("text", out var textProp))
                        {
                            try
                            {
                                if (!Directory.Exists(AppDataFolder)) Directory.CreateDirectory(AppDataFolder);
                                string noteText = textProp.GetString() ?? "";
                                File.WriteAllText(NotePath, noteText);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Failed to save note: {ex.Message}");
                            }
                        }
                    }
                    else if (type == "clear_note")
                    {
                        try
                        {
                            if (File.Exists(NotePath))
                            {
                                File.Delete(NotePath);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Failed to delete note: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error parsing WebView message: {ex.Message}");
            }
        }
    }

    private string ReadResourceText(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null) 
            throw new FileNotFoundException($"Embedded resource '{resourceName}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}