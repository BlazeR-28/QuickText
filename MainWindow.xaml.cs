using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace QuickText;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        InitializeWebView();
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
            this.DragMove();
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