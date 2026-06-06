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
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to set DWM corner preference: {ex.Message}");
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

    private async void InitializeWebView()
    {
        try
        {
            string userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "QuickText",
                "WebView2_Data"
            );
            
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