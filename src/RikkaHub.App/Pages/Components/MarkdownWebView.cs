using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Dispatching;
using RikkaHub.App.Theme;
using Windows.Foundation;

namespace RikkaHub.App.Pages.Components;

/// <summary>
/// Renders one markdown message via WebView2 using the original RikkaHub
/// mark.html template (marked.js + KaTeX + mermaid + highlight.js) — the same
/// pipeline the Android app uses for WebView markdown, so rendering is
/// pixel-faithful to the source project.
/// Streams: host calls Update(markdown) repeatedly; content updates in-page.
/// </summary>
public class MarkdownWebView : UserControl
{
    private readonly WebView2 _web = new();
    private string _pending = "";
    private string _lastRendered = "\u0000INITIAL\u0000";
    private bool _ready;
    private DispatcherQueueTimer? _throttle;
    private double _reportedHeight;

    public static readonly DependencyProperty MarkdownProperty = DependencyProperty.Register(
        nameof(Markdown), typeof(string), typeof(MarkdownWebView), new PropertyMetadata("", OnMarkdownChanged));

    public string Markdown
    {
        get => (string)GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    private static void OnMarkdownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var self = (MarkdownWebView)d;
        self._pending = (string?)e.NewValue ?? "";
        self.ScheduleRender();
    }

    public bool PreferCompactPadding { get; set; } = true;

    public event TypedEventHandler<MarkdownWebView, double>? HeightReported;

    public MarkdownWebView()
    {
        Content = _web;
        _web.MinHeight = 24;
        _web.DefaultBackgroundColor = Microsoft.UI.Colors.Transparent;
        _web.Opacity = 0;
        _web.WebMessageReceived += OnWebMessage;
        _web.NavigationCompleted += (s, e) =>
        {
            _ready = true;
            _web.Opacity = 1;
            if (_pending != _lastRendered) FlushRender();
        };

        var dq = DispatcherQueue;
        _throttle = dq.CreateTimer();
        _throttle.Interval = TimeSpan.FromMilliseconds(160);
        _throttle.IsRepeating = false;
        _throttle.Tick += (s, e) => FlushRender();

        Loaded += async (s, e) =>
        {
            if (_web.CoreWebView2 == null)
            {
                try
                {
                    await _web.EnsureCoreWebView2Async();
                    var core = _web.CoreWebView2!;
                    core.Settings.AreDefaultContextMenusEnabled = false;
                    core.Settings.IsZoomControlEnabled = false;
                    core.Settings.AreDevToolsEnabled = false;
                    core.Settings.IsStatusBarEnabled = false;
                    core.Settings.UserAgent = "RikkaHub-Windows-Markdown/1.0";
                    core.NewWindowRequested += OnNewWindow;
                    var html = BuildHtml(_pending);
                    _web.NavigateToString(html);
                }
                catch (Exception ex)
                {
                    // Fallback: plain text
                    Content = new TextBlock { Text = _pending, IsTextSelectionEnabled = true, TextWrapping = TextWrapping.Wrap };
                    System.Diagnostics.Debug.WriteLine($"WebView2 init failed: {ex.Message}");
                }
            }
        };
    }

    private void OnNewWindow(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        try
        {
            if (e.Uri is string uri && (uri.StartsWith("http://") || uri.StartsWith("https://")))
                _ = Windows.System.Launcher.LaunchUriAsync(new Uri(uri));
        }
        catch { }
    }

    private void OnWebMessage(WebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var msg = e.TryGetWebMessageAsString();
            if (msg != null && msg.Contains("\"rk-height\""))
            {
                var json = System.Text.Json.JsonDocument.Parse(msg);
                if (json.RootElement.TryGetProperty("height", out var h) && h.ValueKind == System.Text.Json.JsonValueKind.Number)
                {
                    var height = h.GetDouble();
                    _reportedHeight = height;
                    var dip = Math.Max(24, Math.Ceiling(height / 0.95)); // px→dip safety margin
                    if (Math.Abs(_web.Height - dip) > 1)
                    {
                        _web.Height = dip;
                    }
                    HeightReported?.Invoke(this, height);
                }
            }
        }
        catch { }
    }

    private void ScheduleRender()
    {
        if (!_ready) return; // first content goes in via NavigateToString
        _throttle?.Start();
    }

    private async void FlushRender()
    {
        if (!_ready || _web.CoreWebView2 == null) return;
        var text = _pending;
        if (text == _lastRendered) return;
        _lastRendered = text;
        try
        {
            var b64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(text));
            await _web.CoreWebView2.ExecuteScriptAsync($"window.setMarkdownBase64('{b64}')");
            _web.Opacity = 1;
        }
        catch { }
    }

    /// <summary>Build the mark.html page with theme colors + streaming hooks injected.</summary>
    private string BuildHtml(string markdown)
    {
        var template = LoadTemplate();
        var p = M3Theme.Current;
        var b64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(markdown ?? ""));
        var padding = PreferCompactPadding ? "8px 12px" : "16px";
        var maxWidth = PreferCompactPadding ? "100%" : "800px";

        var html = template
            .Replace("{{PADDING_PLACEHOLDER}}", padding)
            .Replace("{{MAXWIDTH_PLACEHOLDER}}", maxWidth)
            .Replace("{{MARKDOWN_BASE64}}", "")
            .Replace("{{BACKGROUND_COLOR}}", "transparent")
            .Replace("{{ON_BACKGROUND_COLOR}}", Hex(p.OnSurface))
            .Replace("{{SURFACE_COLOR}}", Hex(p.Surface))
            .Replace("{{ON_SURFACE_COLOR}}", Hex(p.OnSurface))
            .Replace("{{SURFACE_VARIANT_COLOR}}", Hex(p.SurfaceContainerHigh))
            .Replace("{{ON_SURFACE_VARIANT_COLOR}}", Hex(p.OnSurfaceVariant))
            .Replace("{{PRIMARY_COLOR}}", Hex(p.Primary))
            .Replace("{{OUTLINE_COLOR}}", Hex(p.Outline))
            .Replace("{{OUTLINE_VARIANT_COLOR}}", Hex(p.OutlineVariant));

        // Initial content + height reporting: the injected script calls setMarkdownBase64 after module load
        var injection = $@"
    <script>
        window.addEventListener('load', () => {{
            window.__rkInitial = '{b64}';
            if (window.setMarkdownBase64) {{
                window.setMarkdownBase64(window.__rkInitial);
            }} else {{
                window.addEventListener('rk-md-ready', () => window.setMarkdownBase64(window.__rkInitial), {{ once: true }});
            }}
            const report = () => {{
                const h = Math.max(
                    document.body.scrollHeight,
                    document.documentElement.scrollHeight,
                    document.getElementById('content') ? document.getElementById('content').scrollHeight : 0
                );
                if (window.chrome && window.chrome.webview)
                    window.chrome.webview.postMessage(JSON.stringify({{type:'rk-height', height: h}}));
            }};
            new ResizeObserver(report).observe(document.body);
            new ResizeObserver(report).observe(document.getElementById('content'));
            setInterval(report, 800);
        }});
    </script>";
        html = html.Replace("</body>", injection + "\n</body>");
        return html;
    }

    private static string Hex(Windows.UI.Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    private static string? _templateCache;

    private static string LoadTemplate()
    {
        if (_templateCache != null) return _templateCache;
        try
        {
            var path = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "mark.html");
            _templateCache = System.IO.File.ReadAllText(path);
        }
        catch
        {
            // extremely degraded fallback
            _templateCache = "<html><body style='margin:8px'><pre>{{MARKDOWN_BASE64}}</pre></body></html>";
        }
        return _templateCache;
    }
}
