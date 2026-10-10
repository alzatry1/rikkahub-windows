using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using RikkaHub.App.Pages;
using RikkaHub.App.Services;
using RikkaHub.App.Theme;
using RikkaHub.Core.Data;
using RikkaHub.Core.Model;
using Windows.Graphics;

namespace RikkaHub.App.Shell;

/// <summary>
/// Main window — NavigationView shell with Mica backdrop (matches the Android app's
/// chat drawer + pages structure).
/// </summary>
public class MainWindow : Window
{
    private readonly AppCtx _ctx;
    private readonly ContentControl _host = new();
    private readonly NavigationView _nav;
    private readonly Dictionary<string, Page> _pageCache = new();
    private readonly List<string> _navStack = new();
    private TitleBar? _titleBar;

    public MainWindow(DataStore store, Settings settings)
    {
        _ctx = new AppCtx();
        _ctx.Init();

        Title = Loc.Tr("app_name");

        // Window setup
        var appWindow = AppWindow;
        appWindow.Resize(new SizeInt32 { Width = 1280, Height = 832 });
        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = 480;
            presenter.PreferredMinimumHeight = 600;
        }

        // Backdrop: Mica on Windows 11, Desktop Acrylic fallback on Windows 10
        if (Environment.OSVersion.Version.Build >= 22000)
        {
            SystemBackdrop = new MicaBackdrop();
        }
        else
        {
            try { SystemBackdrop = new DesktopAcrylicBackdrop(); }
            catch { /* acrylic unavailable — plain background */ }
        }

        // Custom title bar
        ExtendsContentIntoTitleBar = true;
        _titleBar = new TitleBar(this);
        SetTitleBar(_titleBar);

        // Navigation shell
        _nav = new NavigationView
        {
            IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed,
            IsPaneToggleButtonVisible = false,
            IsPaneVisible = true,
            OpenPaneLength = 280,
            PaneDisplayMode = NavigationViewPaneDisplayMode.Left,
            IsTitleBarAutoPaddingEnabled = false,
            Content = _host,
        };

        _nav.MenuItems.Add(new NavigationViewItem
        {
            Content = Loc.Tr("chat"),
            Icon = MakeIcon('\uE8F2'), // Message
            Tag = "chat",
            IsSelected = true,
        });
        _nav.MenuItems.Add(new NavigationViewItem
        {
            Content = Loc.Tr("assistants"),
            Icon = MakeIcon('\uE779'), // Contact
            Tag = "assistants",
        });
        _nav.MenuItems.Add(new NavigationViewItem
        {
            Content = Loc.Tr("history"),
            Icon = MakeIcon('\uE81C'), // Clock
            Tag = "history",
        });
        _nav.FooterMenuItems.Add(new NavigationViewItem
        {
            Content = Loc.Tr("settings"),
            Icon = MakeIcon('\uE713'), // Setting
            Tag = "settings",
        });

        _nav.SelectionChanged += (s, e) =>
        {
            var tag = (e.SelectedItemContainer?.Tag as string) ?? "chat";
            Navigate(tag);
        };

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(48) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(_titleBar, 0);
        Grid.SetRow(_nav, 1);
        root.Children.Add(_titleBar);
        root.Children.Add(_nav);

        Content = root;

        // react to theme changes
        M3Theme.Changed += p => DispatcherQueue.TryEnqueue(() =>
        {
            // refresh shell brushes
            root.Background = new SolidColorBrush(p.Background);
        });
        root.Background = new SolidColorBrush(M3Theme.Current.Background);

        // NOTE: initial navigation is deferred to the Activated event — calling
        // Frame.Navigate() from the window constructor crashes the XAML runtime
        // (AccessViolationException in IFrameMethods.Navigate) because content
        // is not loaded yet. This was the v1.0.0 startup crash.
        Activated += (s, e) =>
        {
            if (!_initialNavigated)
            {
                _initialNavigated = true;
                try
                {
                    Navigate("chat");
                }
                catch (Exception ex)
                {
                    global::RikkaHub.App.App.StartupLog($"FATAL (initial navigate): {ex}");
                }
            }
        };
    }

    private bool _initialNavigated;

    private static FontIcon MakeIcon(char glyph) => new() { Glyph = char.ToString(glyph) };

    /// <summary>
    /// Navigation without Frame: code-built pages + Frame.Navigate(Type, object)
    /// crashed deterministically (AccessViolationException in CsWinRT marshaling),
    /// so pages are instantiated directly and hosted in a ContentControl.
    /// Page instances are cached per tag; sub-pages (e.g. provider detail) are not.
    /// </summary>
    public void Navigate(string tag)
    {
        try
        {
            if (!_pageCache.TryGetValue(tag, out var page))
            {
                page = tag switch
                {
                    "settings" => new SettingPage(_ctx),
                    "assistants" => new AssistantPage(_ctx),
                    "history" => new HistoryPage(_ctx),
                    "providers" => new SettingProviderPage(_ctx),
                    "theme" => new SettingThemePage(_ctx),
                    "other" => new SettingOtherPage(_ctx),
                    "about" => new SettingAboutPage(_ctx),
                    _ => new ChatPage(_ctx),
                };
                _pageCache[tag] = page;
            }
            _host.Content = page;
            if (_navStack.LastOrDefault() != tag)
            {
                _navStack.Add(tag);
                if (_navStack.Count > 32) _navStack.RemoveAt(0);
            }
            // keep the nav rail selection in sync
            var item = _nav.MenuItems.Concat(_nav.FooterMenuItems)
                .OfType<NavigationViewItem>()
                .FirstOrDefault(i => (i.Tag as string) == NavRootOf(tag));
            if (item != null && !ReferenceEquals(_nav.SelectedItem, item)) _nav.SelectedItem = item;

            _titleBar?.SetTitle(tag switch
            {
                "settings" => Loc.Tr("settings"),
                "assistants" => Loc.Tr("assistants"),
                "history" => Loc.Tr("history"),
                "providers" => Loc.Tr("providers"),
                "providerDetail" => Loc.Tr("edit_provider"),
                _ => Loc.Tr("app_name"),
            });
        }
        catch (Exception ex)
        {
            global::RikkaHub.App.App.StartupLog($"FATAL (navigate {tag}): {ex}");
        }
    }

    /// <summary>Open a provider detail sub-page (not cached).</summary>
    public void OpenProviderDetail(Guid providerId)
    {
        _host.Content = new SettingProviderDetailPage(_ctx, providerId);
        if (_navStack.LastOrDefault() != "providerDetail") _navStack.Add("providerDetail");
        _titleBar?.SetTitle(Loc.Tr("edit_provider"));
    }

    private static string NavRootOf(string tag) => tag switch
    {
        "providers" or "providerDetail" or "theme" or "other" or "about" => "settings",
        _ => tag,
    };

    /// <summary>Go back to the previous entry in the navigation stack.</summary>
    public void GoBack()
    {
        // pop current
        if (_navStack.Count > 0) _navStack.RemoveAt(_navStack.Count - 1);
        var target = _navStack.LastOrDefault() ?? "chat";
        // re-enter without pushing a new entry
        if (_navStack.LastOrDefault() != target) _navStack.Add(target);
        Navigate(target);
    }
}

/// <summary>Custom draggable title bar matching M3 top bar height.</summary>
public class TitleBar : Grid
{
    private readonly TextBlock _title = new();

    public TitleBar(Window window)
    {
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _title.VerticalAlignment = VerticalAlignment.Center;
        _title.Margin = new Thickness(16, 0, 0, 0);
        _title.FontSize = 13;
        _title.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        _title.Foreground = new SolidColorBrush(M3Theme.Current.OnSurface);
        Children.Add(_title);

        // Drag region: whole bar (caption buttons drawn by the system)
        Loaded += (s, e) => window.SetTitleBar(this);
        M3Theme.Changed += p => window.DispatcherQueue.TryEnqueue(() =>
        {
            _title.Foreground = new SolidColorBrush(p.OnSurface);
        });
    }

    public void SetTitle(string t) => _title.Text = t;
}
