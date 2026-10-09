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
    private readonly Frame _frame = new();
    private readonly NavigationView _nav;
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

        SystemBackdrop = new MicaBackdrop();

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
            Content = _frame,
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

        Navigate("chat");
    }

    private static FontIcon MakeIcon(char glyph) => new() { Glyph = char.ToString(glyph) };

    public void Navigate(string tag)
    {
        switch (tag)
        {
            case "settings":
                _frame.Navigate(typeof(SettingPage), _ctx);
                break;
            case "assistants":
                _frame.Navigate(typeof(AssistantPage), _ctx);
                break;
            case "history":
                _frame.Navigate(typeof(HistoryPage), _ctx);
                break;
            default:
                _frame.Navigate(typeof(ChatPage), _ctx);
                break;
        }
        _titleBar?.SetTitle(tag switch
        {
            "settings" => Loc.Tr("settings"),
            "assistants" => Loc.Tr("assistants"),
            "history" => Loc.Tr("history"),
            _ => Loc.Tr("app_name"),
        });
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
