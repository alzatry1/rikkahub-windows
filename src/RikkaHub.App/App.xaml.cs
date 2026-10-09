using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using RikkaHub.App.Shell;
using RikkaHub.App.Theme;

namespace RikkaHub.App;

public partial class App : Application
{
    public static new App Current => (App)Application.Current;

    private MainWindow? _window;

    public MainWindow? MainWindow => _window;

    public System.IntPtr WindowHandle => _window != null
        ? WinRT.Interop.WindowNative.GetWindowHandle(_window)
        : System.IntPtr.Zero;

    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            StartupLog($"FATAL (domain): {e.ExceptionObject}");
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            StartupLog($"WARN (task): {e.Exception}");
            e.SetObserved();
        };
        StartupLog("App constructed");
    }

    /// <summary>Append a line to %LOCALAPPDATA%\RikkaHub\logs\startup.log — never throws.</summary>
    public static void StartupLog(string message)
    {
        try
        {
            var dir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RikkaHub", "logs");
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "startup.log"),
                $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}\n");
        }
        catch { }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    /// <summary>Show a native message box even when XAML is unavailable (startup crashes).</summary>
    public static void FatalMessageBox(string title, string details)
    {
        try
        {
            MessageBox(IntPtr.Zero, details, title, 0x00000010 /* MB_ICONERROR */);
        }
        catch { }
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        StartupLog($"UNHANDLED: {e.Message}\n{e.Exception}");
        try
        {
            var logDir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RikkaHub", "logs");
            System.IO.Directory.CreateDirectory(logDir);
            System.IO.File.AppendAllText(System.IO.Path.Combine(logDir, "errors.log"),
                $"[{DateTimeOffset.Now:O}] {e.Message}\n{e.Exception}\n\n");
        }
        catch { }
        e.Handled = true;
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        try
        {
            StartupLog("OnLaunched begin");
            var store = new Core.Data.DataStore();
            var settings = store.LoadSettings();
            StartupLog("settings loaded");

            _window = new MainWindow(store, settings);
            StartupLog("main window constructed");

            var dark = settings.DarkMode switch
            {
                "dark" => true,
                "light" => false,
                _ => _window.Content is FrameworkElement fe && fe.ActualTheme == ElementTheme.Dark,
            };
            M3Theme.Apply(this, settings.SeedColor, dark);
            StartupLog("theme applied");

            _window.Activate();
            StartupLog("window activated");
        }
        catch (Exception e)
        {
            StartupLog($"FATAL (launch): {e}");
            FatalMessageBox(
                "RikkaHub failed to start",
                "RikkaHub 启动失败 / failed to start:\n\n" + e +
                "\n\n日志/Log: %LOCALAPPDATA%\\RikkaHub\\logs\\startup.log\n" +
                "请反馈给 / please report: github.com/alzatry1/rikkahub-windows/issues");
        }
    }
}
