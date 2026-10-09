using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using RikkaHub.App.Services;
using RikkaHub.App.Theme;
using RikkaHub.Core.Provider;

namespace RikkaHub.App.Pages;

/// <summary>Provider list page — port of SettingProviderPage.kt.</summary>
public class SettingProviderPage : Page
{
    private AppCtx _ctx = null!;
    private readonly StackPanel _list = new();
    private readonly TextBlock _balance = new();

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _ctx = (AppCtx)e.Parameter;
        Build();
        Rebind();
    }

    private void Build()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(56) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // top bar with back
        var topBar = new Grid();
        topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0, GridUnitType.Auto) });
        topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0, GridUnitType.Auto) });
        var back = new Button { Content = new FontIcon { Glyph = "\uE72B", FontSize = 16 }, Background = null };
        back.Click += (s, e) => Frame.GoBack();
        Grid.SetColumn(back, 0);
        topBar.Children.Add(back);
        var title = new TextBlock
        {
            Text = Loc.Tr("providers"),
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
        };
        title.SetResourceReference(TextBlock.ForegroundProperty, "M3OnSurface");
        Grid.SetColumn(title, 1);
        topBar.Children.Add(title);

        var addBtn = new Button
        {
            Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new FontIcon { Glyph = "\uE710", FontSize = 14 }, new TextBlock { Text = Loc.Tr("add_provider") } } },
        };
        addBtn.Click += async (s, e) => await AddProviderAsync();
        Grid.SetColumn(addBtn, 2);
        topBar.Children.Add(addBtn);
        Grid.SetRow(topBar, 0);
        root.Children.Add(topBar);

        var scroll = new ScrollViewer { Content = _list, Padding = new Thickness(16) };
        _list.Spacing = 8;
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);

        Content = root;
    }

    private void Rebind()
    {
        _list.Children.Clear();
        foreach (var provider in _ctx.Settings.Providers)
        {
            var p = provider;
            var card = new Grid
            {
                Padding = new Thickness(16, 12, 16, 12),
                CornerRadius = new Microsoft.UI.Xaml.CornerRadius(16),
                Background = new SolidColorBrush(M3Theme.Current.SurfaceContainer),
            };
            card.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0, GridUnitType.Auto) });
            card.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            card.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0, GridUnitType.Auto) });

            var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var name = new TextBlock { Text = p.Name, FontSize = 15, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
            name.SetResourceReference(TextBlock.ForegroundProperty, "M3OnSurface");
            var desc = new TextBlock
            {
                Text = p.Models.Count == 0
                    ? Loc.Tr("models") + ": 0"
                    : Loc.Tr("models") + $": {p.Models.Count}" + (p.Enabled ? "" : " · " + Loc.Tr("enabled") + ": ✕"),
                FontSize = 12,
                Opacity = 0.65,
            };
            info.Children.Add(name);
            info.Children.Add(desc);
            Grid.SetColumn(info, 1);
            card.Children.Add(info);

            var toggle = new ToggleSwitch
            {
                IsOn = p.Enabled,
                MinWidth = 0,
                OnContent = "",
                OffContent = "",
                VerticalAlignment = VerticalAlignment.Center,
            };
            var captured = p;
            toggle.Toggled += (s, e) =>
            {
                var idx = _ctx.Settings.Providers.IndexOf(captured);
                if (idx < 0) return;
                var copy = captured.CopyProvider();
                copy.Enabled = toggle.IsOn;
                _ctx.Settings.Providers[idx] = copy;
                _ctx.SaveSettings();
                Rebind();
            };
            Grid.SetColumn(toggle, 2);
            card.Children.Add(toggle);

            var btn = new Button
            {
                Content = card,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Background = null,
                Padding = new Thickness(0),
            };
            btn.Click += (s, e) => Frame.Navigate(typeof(SettingProviderDetailPage), (_ctx, p.Id));
            _list.Children.Add(btn);
        }
    }

    private async Task AddProviderAsync()
    {
        var name = new TextBox { Header = Loc.Tr("provider_name"), PlaceholderText = "OpenAI" };
        var typeCombo = new ComboBox { Header = Loc.Tr("provider_type"), HorizontalAlignment = HorizontalAlignment.Stretch };
        typeCombo.Items.Add("OpenAI Compatible");
        typeCombo.Items.Add("Gemini");
        typeCombo.Items.Add("Claude");
        typeCombo.SelectedIndex = 0;
        var baseUrl = new TextBox { Header = Loc.Tr("base_url"), PlaceholderText = "https://api.openai.com/v1" };
        var apiKey = new PasswordBox { Header = Loc.Tr("api_key") };

        var panel = new StackPanel { Spacing = 12, MinWidth = 360 };
        panel.Children.Add(typeCombo);
        panel.Children.Add(name);
        panel.Children.Add(baseUrl);
        panel.Children.Add(apiKey);

        var dlg = new ContentDialog
        {
            Title = Loc.Tr("add_provider"),
            Content = panel,
            PrimaryButtonText = Loc.Tr("confirm"),
            CloseButtonText = Loc.Tr("cancel"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;

        ProviderSetting setting = ((typeCombo.SelectedIndex < 0 ? 0 : typeCombo.SelectedIndex)) switch
        {
            1 => new GeminiSetting(),
            2 => new ClaudeSetting(),
            _ => new OpenAISetting(),
        };
        if (string.IsNullOrWhiteSpace(name.Text))
            setting.Name = typeCombo.SelectedIndex switch { 1 => "Gemini", 2 => "Claude", _ => "Provider" };
        else
            setting.Name = name.Text.Trim();
        if (setting is ICredentialSetting cred)
        {
            if (!string.IsNullOrWhiteSpace(baseUrl.Text)) cred.BaseUrl = baseUrl.Text.Trim();
            cred.ApiKey = apiKey.Password;
        }
        _ctx.Settings.Providers.Add(setting);
        _ctx.SaveSettings();
        Rebind();
    }
}
