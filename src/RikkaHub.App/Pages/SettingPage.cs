using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using RikkaHub.App.Services;
using RikkaHub.App.Theme;

namespace RikkaHub.App.Pages;

/// <summary>Settings index page — port of SettingPage.kt (navigation entry).</summary>
public class SettingPage : Page
{
    private AppCtx _ctx = null!;

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _ctx = (AppCtx)e.Parameter;
        Build();
    }

    private void Build()
    {
        var scroll = new ScrollViewer();
        var panel = new StackPanel { Spacing = 2, Margin = new Thickness(16) };

        panel.Children.Add(MakeHeader(Loc.Tr("settings")));

        panel.Children.Add(MakeSectionItem("\uF121", Loc.Tr("providers"), () => Frame.Navigate(typeof(SettingProviderPage), _ctx)));
        panel.Children.Add(MakeSectionItem("\uE790", Loc.Tr("theme"), () => Frame.Navigate(typeof(SettingThemePage), _ctx)));
        panel.Children.Add(MakeSectionItem("\uE779", Loc.Tr("assistants"), () => Frame.Navigate(typeof(AssistantPage), _ctx)));
        panel.Children.Add(MakeSectionItem("\uE713", Loc.Tr("other_settings"), () => Frame.Navigate(typeof(SettingOtherPage), _ctx)));
        panel.Children.Add(MakeSectionItem("\uE946", Loc.Tr("about"), () => Frame.Navigate(typeof(SettingAboutPage), _ctx)));

        scroll.Content = panel;
        Content = scroll;
    }

    private TextBlock MakeHeader(string text)
    {
        var h = new TextBlock
        {
            Text = text,
            FontSize = 28,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(4, 12, 0, 16),
        };
        h.Foreground = new SolidColorBrush(M3Theme.Current.OnSurface);
        return h;
    }

    private Control MakeSectionItem(string glyph, string label, Action onClick)
    {
        var grid = new Grid
        {
            Padding = new Thickness(16, 12, 16, 12),
            CornerRadius = new Microsoft.UI.Xaml.CornerRadius(24),
            Margin = new Thickness(0, 2),
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0, GridUnitType.Auto) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0, GridUnitType.Auto) });
        var icon = new FontIcon { Glyph = glyph, FontSize = 18 };
        Grid.SetColumn(icon, 0);
        grid.Children.Add(icon);
        var text = new TextBlock { Text = label, FontSize = 15, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };
        text.Foreground = new SolidColorBrush(M3Theme.Current.OnSurface);
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        var chevron = new FontIcon { Glyph = "\uE76C", FontSize = 14 };
        chevron.Foreground = new SolidColorBrush(M3Theme.Current.OnSurfaceVariant);
        Grid.SetColumn(chevron, 2);
        grid.Children.Add(chevron);

        var btn = new Button
        {
            Content = grid,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = null,
            Padding = new Thickness(0),
            Margin = new Thickness(0),
        };
        btn.Click += (s, e) => onClick();
        return btn;
    }
}
