using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using RikkaHub.App.Services;
using RikkaHub.App.Theme;

namespace RikkaHub.App.Pages;

/// <summary>Other settings — display preferences, language, input behavior.</summary>
public class SettingOtherPage : Page
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
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(56) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var topBar = new Grid();
        topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0, GridUnitType.Auto) });
        var back = new Button { Content = new FontIcon { Glyph = "\uE72B", FontSize = 16 }, Background = null };
        back.Click += (s, e) => Frame.GoBack();
        Grid.SetColumn(back, 0);
        topBar.Children.Add(back);
        var title = new TextBlock
        {
            Text = Loc.Tr("other_settings"),
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
        };
        title.SetResourceReference(TextBlock.ForegroundProperty, "M3OnSurface");
        Grid.SetColumn(title, 1);
        topBar.Children.Add(title);
        Grid.SetRow(topBar, 0);
        root.Children.Add(topBar);

        var scroll = new ScrollViewer();
        var panel = new StackPanel { Spacing = 16, Padding = new Thickness(16), MaxWidth = 720, HorizontalAlignment = HorizontalAlignment.Left };

        // display
        var displayCard = Card();
        var displayPanel = new StackPanel { Spacing = 12 };
        displayPanel.Children.Add(new TextBlock { Text = Loc.Tr("display"), FontSize = 15, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });

        var bubbleCombo = new ComboBox { Header = Loc.Tr("bubble_style"), HorizontalAlignment = HorizontalAlignment.Left, MinWidth = 200 };
        bubbleCombo.Items.Add(Loc.Tr("bubble_style_bubble"));
        bubbleCombo.Items.Add(Loc.Tr("bubble_style_plain"));
        bubbleCombo.SelectedIndex = _ctx.Settings.BubbleStyle == "plain" ? 1 : 0;
        bubbleCombo.SelectionChanged += (s, e) =>
        {
            _ctx.Settings.BubbleStyle = bubbleCombo.SelectedIndex == 1 ? "plain" : "bubble";
            _ctx.SaveSettings();
        };
        displayPanel.Children.Add(bubbleCombo);

        var tokenToggle = new ToggleSwitch { Header = Loc.Tr("show_token_usage"), IsOn = _ctx.Settings.ShowTokenUsage, OnContent = "", OffContent = "" };
        tokenToggle.Toggled += (s, e) => { _ctx.Settings.ShowTokenUsage = tokenToggle.IsOn; _ctx.SaveSettings(); };
        displayPanel.Children.Add(tokenToggle);

        var sendToggle = new ToggleSwitch { Header = "Enter " + Loc.Tr("send"), IsOn = _ctx.Settings.SendOnEnter, OnContent = "", OffContent = "" };
        sendToggle.Toggled += (s, e) => { _ctx.Settings.SendOnEnter = sendToggle.IsOn; _ctx.SaveSettings(); };
        displayPanel.Children.Add(sendToggle);
        displayCard.Child = displayPanel;
        panel.Children.Add(displayCard);

        // language
        var langCard = Card();
        var langPanel = new StackPanel { Spacing = 12 };
        var langCombo = new ComboBox { Header = Loc.Tr("language"), HorizontalAlignment = HorizontalAlignment.Left, MinWidth = 200 };
        langCombo.Items.Add(Loc.Tr("language_system"));
        langCombo.Items.Add("English");
        langCombo.Items.Add("简体中文");
        langCombo.SelectedIndex = _ctx.Settings.Language switch { "en" => 1, "zh" => 2, _ => 0 };
        langCombo.SelectionChanged += (s, e) =>
        {
            _ctx.Settings.Language = langCombo.SelectedIndex switch { 1 => "en", 2 => "zh", _ => "system" };
            _ctx.SaveSettings();
            Loc.Init(_ctx.Settings.Language);
        };
        langPanel.Children.Add(langCombo);
        langCard.Child = langPanel;
        panel.Children.Add(langCard);

        // data location
        var dataCard = Card();
        var dataPanel = new StackPanel { Spacing = 8 };
        dataPanel.Children.Add(new TextBlock { Text = "Data Directory", FontSize = 15, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        var pathText = new TextBlock
        {
            Text = Core.Data.DataStore.BaseDir,
            FontSize = 12,
            Opacity = 0.7,
            IsTextSelectionEnabled = true,
            TextWrapping = TextWrapping.Wrap,
        };
        dataPanel.Children.Add(pathText);
        dataCard.Child = dataPanel;
        panel.Children.Add(dataCard);

        scroll.Content = panel;
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);
        Content = root;
    }

    private static Border Card() => new()
    {
        Padding = new Thickness(16),
        CornerRadius = new Microsoft.UI.Xaml.CornerRadius(16),
        Background = new SolidColorBrush(M3Theme.Current.SurfaceContainer),
    };
}
