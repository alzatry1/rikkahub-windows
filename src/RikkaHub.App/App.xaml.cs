using RikkaHub.App.Theme;
using RikkaHub.App.Shell;
using Microsoft.UI.Xaml;

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
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[RikkaHub] Unhandled: {e.Message}");
        try
        {
            var logDir = System.IO.Path.Combine(Core.Data.DataStore.BaseDir, "logs");
            System.IO.Directory.CreateDirectory(logDir);
            System.IO.File.AppendAllText(System.IO.Path.Combine(logDir, "errors.log"),
                $"[{DateTimeOffset.Now:O}] {e.Message}\n{e.Exception}\n\n");
        }
        catch { }
        e.Handled = true;
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        var store = new Core.Data.DataStore();
        var settings = store.LoadSettings();

        _window = new MainWindow(store, settings);
        var dark = settings.DarkMode switch
        {
            "dark" => true,
            "light" => false,
            _ => _window.Content is FrameworkElement fe && fe.ActualTheme == ElementTheme.Dark,
        };
        M3Theme.Apply(this, settings.SeedColor, dark);

        _window.Activate();
    }
}
