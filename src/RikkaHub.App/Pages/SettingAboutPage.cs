using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using RikkaHub.App.Services;
using RikkaHub.App.Theme;

namespace RikkaHub.App.Pages;

/// <summary>About page — port of SettingAboutPage.kt.</summary>
public class SettingAboutPage : Page
{
    private AppCtx _ctx = null!;

    public SettingAboutPage(AppCtx ctx)
    {
        _ctx = ctx;
        Build();
    }

    private void Build()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(56) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var topBar = new Grid();
        topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0, GridUnitType.Auto) });
        var back = new Button { Content = new FontIcon { Glyph = "\uE72B", FontSize = 16 }, Background = null };
        back.Click += (s, e) => global::RikkaHub.App.App.Current.MainWindow?.GoBack();
        Grid.SetColumn(back, 0);
        topBar.Children.Add(back);
        var title = new TextBlock
        {
            Text = Loc.Tr("about"),
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
        };
        title.Foreground = new SolidColorBrush(M3Theme.Current.OnSurface);
        Grid.SetColumn(title, 1);
        topBar.Children.Add(title);
        Grid.SetRow(topBar, 0);
        root.Children.Add(topBar);

        var scroll = new ScrollViewer();
        var panel = new StackPanel
        {
            Spacing = 8,
            Padding = new Thickness(24),
            HorizontalAlignment = HorizontalAlignment.Center,
            MaxWidth = 560,
        };

        // icon (unpackaged apps can't rely on ms-appx; use file path)
        var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "icon.png");
        Microsoft.UI.Xaml.Media.Imaging.BitmapImage? appIcon = null;
        try
        {
            if (System.IO.File.Exists(iconPath)) appIcon = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(iconPath));
        }
        catch { }
        if (appIcon != null)
        {
            panel.Children.Add(new Image
            {
                Source = appIcon,
                Width = 96,
                Height = 96,
                HorizontalAlignment = HorizontalAlignment.Center,
            });
        }

        var name = new TextBlock
        {
            Text = Loc.Tr("app_name") + " for Windows",
            FontSize = 24,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        name.Foreground = new SolidColorBrush(M3Theme.Current.OnSurface);
        panel.Children.Add(name);

        panel.Children.Add(MakeRow(Loc.Tr("about_version"), Loc.Tr("version")));
        panel.Children.Add(MakeRow("GitHub", "github.com/alzatry1/rikkahub-windows"));
        panel.Children.Add(MakeRow(Loc.Tr("about_source"), "github.com/rikkahub/rikkahub"));

        var desc = new TextBlock
        {
            Text = Loc.Tr("about_desc"),
            FontSize = 13,
            Opacity = 0.7,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 12, 0, 0),
        };
        panel.Children.Add(desc);

        var license = new TextBlock
        {
            Text = "GNU AGPL v3 · Port of RikkaHub (me.rerere.rikkahub)",
            FontSize = 11,
            Opacity = 0.5,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 24, 0, 0),
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
        };
        panel.Children.Add(license);

        scroll.Content = panel;
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);
        Content = root;
    }

    private StackPanel MakeRow(string key, string value)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, HorizontalAlignment = HorizontalAlignment.Center };
        var k = new TextBlock { Text = key, FontSize = 13, Opacity = 0.6 };
        var v = new TextBlock { Text = value, FontSize = 13, IsTextSelectionEnabled = true };
        panel.Children.Add(k);
        panel.Children.Add(v);
        return panel;
    }
}
