using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using RikkaHub.App.Services;
using RikkaHub.App.Theme;

namespace RikkaHub.App.Pages;

/// <summary>Theme page — port of SettingThemePage.kt (dark mode, dynamic color, seed color).</summary>
public class SettingThemePage : Page
{
    private AppCtx _ctx = null!;
    private readonly ComboBox _darkCombo = new();
    private readonly ToggleSwitch _dynamicToggle = new();
    private readonly TextBox _seedBox = new();
    private readonly Grid _preview = new();
    private readonly StackPanel _swatches = new();

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
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });

        var topBar = new Grid();
        topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var back = new Button { Content = new FontIcon { Glyph = "\uE72B", FontSize = 16 }, Background = null };
        back.Click += (s, e) => Frame.GoBack();
        Grid.SetColumn(back, 0);
        topBar.Children.Add(back);
        var title = new TextBlock
        {
            Text = Loc.Tr("theme"),
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

        // dark mode
        var darkCard = Card();
        var darkPanel = new StackPanel { Spacing = 12 };
        darkPanel.Children.Add(new TextBlock { Text = Loc.Tr("dark_mode"), FontSize = 15, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        _darkCombo.Items.Add(Loc.Tr("dark_mode_auto"));
        _darkCombo.Items.Add(Loc.Tr("dark_mode_dark"));
        _darkCombo.Items.Add(Loc.Tr("dark_mode_light"));
        _darkCombo.SelectedIndex = _ctx.Settings.DarkMode switch { "dark" => 1, "light" => 2, _ => 0 };
        _darkCombo.SelectionChanged += (s, e) =>
        {
            _ctx.Settings.DarkMode = _darkCombo.SelectedIndex switch { 1 => "dark", 2 => "light", _ => "auto" };
            _ctx.SaveSettings();
            ApplyTheme();
        };
        darkPanel.Children.Add(_darkCombo);
        darkCard.Child = darkPanel;
        panel.Children.Add(darkCard);

        // dynamic color
        var dynCard = Card();
        var dynPanel = new StackPanel { Spacing = 12 };
        var dynRow = new Grid();
        dynRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        dynRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var dynText = new StackPanel();
        dynText.Children.Add(new TextBlock { Text = Loc.Tr("dynamic_color"), FontSize = 15, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        dynText.Children.Add(new TextBlock { Text = Loc.Tr("seed_color"), FontSize = 12, Opacity = 0.6 });
        Grid.SetColumn(dynText, 0);
        dynRow.Children.Add(dynText);
        _dynamicToggle.IsOn = _ctx.Settings.DynamicColor;
        Grid.SetColumn(_dynamicToggle, 1);
        _dynamicToggle.Toggled += (s, e) => { _ctx.Settings.DynamicColor = _dynamicToggle.IsOn; _ctx.SaveSettings(); };
        dynRow.Children.Add(_dynamicToggle);
        dynPanel.Children.Add(dynRow);

        _seedBox.Header = Loc.Tr("seed_color");
        _seedBox.PlaceholderText = "#6750A4";
        _seedBox.Text = $"#{_ctx.Settings.SeedColor & 0xFFFFFF:X6}";
        var seedApply = new Button { Content = Loc.Tr("save") };
        seedApply.Click += (s, e) =>
        {
            if (TryParseColor(_seedBox.Text, out var argb))
            {
                _ctx.Settings.SeedColor = argb;
                _ctx.SaveSettings();
                ApplyTheme();
                RebuildSwatches();
            }
        };
        var seedRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        _seedBox.HorizontalAlignment = HorizontalAlignment.Left;
        _seedBox.Width = 160;
        seedRow.Children.Add(_seedBox);
        seedRow.Children.Add(seedApply);
        dynPanel.Children.Add(seedRow);
        _swatches.Orientation = Orientation.Horizontal;
        _swatches.Spacing = 6;
        dynPanel.Children.Add(_swatches);
        dynCard.Child = dynPanel;
        panel.Children.Add(dynCard);

        // preset seeds (Material palette)
        var presetCard = Card();
        var presetPanel = new StackPanel { Spacing = 8 };
        presetPanel.Children.Add(new TextBlock { Text = "Preset Seeds", FontSize = 15, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        var presetRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        int[] seeds = { unchecked((int)0xFF6750A4), unchecked((int)0xFF0061A4), unchecked((int)0xFF006E2C), unchecked((int)0xFFB3261E), unchecked((int)0xFF7D5260), unchecked((int)0xFF795548), unchecked((int)0xFF00696E), unchecked((int)0xFF6D4E2F) };
        foreach (var seed in seeds)
        {
            var s = seed;
            var swatch = new Border
            {
                Width = 36,
                Height = 36,
                CornerRadius = new Microsoft.UI.Xaml.CornerRadius(18),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255,
                    (byte)(s >> 16), (byte)(s >> 8), (byte)s)),
            };
            swatch.Tapped += (sender, e) =>
            {
                _ctx.Settings.SeedColor = s;
                _ctx.SaveSettings();
                ApplyTheme();
                RebuildSwatches();
                _seedBox.Text = $"#{s & 0xFFFFFF:X6}";
            };
            ToolTipService.SetToolTip(swatch, $"#{s & 0xFFFFFF:X6}");
            presetRow.Children.Add(swatch);
        }
        presetPanel.Children.Add(presetRow);
        presetCard.Child = presetPanel;
        panel.Children.Add(presetCard);

        scroll.Content = panel;
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);
        Content = root;

        RebuildSwatches();
    }

    private void RebuildSwatches()
    {
        _swatches.Children.Clear();
        var p = M3Theme.Current;
        foreach (var c in new[] { p.Primary, p.Secondary, p.Tertiary, p.Error, p.SurfaceContainer })
        {
            _swatches.Children.Add(new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new Microsoft.UI.Xaml.CornerRadius(14),
                Background = new SolidColorBrush(c),
            });
        }
    }

    private void ApplyTheme()
    {
        var dark = _ctx.Settings.DarkMode switch
        {
            "dark" => true,
            "light" => false,
            _ => IsSystemDark(),
        };
        M3Theme.Apply((App)Application.Current, _ctx.Settings.SeedColor, dark);
    }

    private static bool IsSystemDark()
    {
        try
        {
            var uiSettings = new Windows.UI.ViewManagement.UISettings();
            var fg = uiSettings.GetColorValue(Windows.UI.ViewManagement.UIElementType.ForegroundColor);
            return (fg.R + fg.G + fg.B) > 300;
        }
        catch { return false; }
    }

    private static bool TryParseColor(string text, out int argb)
    {
        argb = 0;
        text = text.Trim().TrimStart('#');
        if (text.Length != 6) return false;
        try
        {
            argb = unchecked((int)(0xFF000000 | Convert.ToUInt32(text, 16)));
            return true;
        }
        catch { return false; }
    }

    private static Border Card() => new()
    {
        Padding = new Thickness(16),
        CornerRadius = new Microsoft.UI.Xaml.CornerRadius(16),
        Background = new SolidColorBrush(M3Theme.Current.SurfaceContainer),
    };
}
