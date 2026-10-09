using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using RikkaHub.App.Services;
using RikkaHub.App.Theme;
using RikkaHub.Core.Provider;

namespace RikkaHub.App.Pages;

/// <summary>Provider detail page — port of SettingProviderDetailPage.kt (API key, base url, model management).</summary>
public class SettingProviderDetailPage : Page
{
    private AppCtx _ctx = null!;
    private Guid _providerId;
    private readonly TextBox _nameBox = new();
    private readonly TextBox _baseUrlBox = new();
    private readonly PasswordBox _apiKeyBox = new();
    private readonly TextBox _chatCompletionsBox = new();
    private readonly StackPanel _modelList = new();
    private readonly ProgressRing _fetching = new() { IsActive = false, Width = 20, Height = 20 };
    private readonly TextBlock _fetchStatus = new();

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        (_ctx, _providerId) = ((AppCtx, Guid))e.Parameter;
        Build();
        Rebind();
    }

    private ProviderSetting? Provider => _ctx.Settings.Providers.FirstOrDefault(p => p.Id == _providerId);

    private void SaveProvider(Action<ProviderSetting> mutate)
    {
        var provider = Provider;
        if (provider == null) return;
        var copy = provider.CopyProvider();
        mutate(copy);
        var idx = _ctx.Settings.Providers.IndexOf(provider);
        _ctx.Settings.Providers[idx] = copy;
        _ctx.SaveSettings();
        _providerId = copy.Id; // unchanged
    }

    private void Build()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(56) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });

        // top bar
        var topBar = new Grid();
        topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var back = new Button { Content = new FontIcon { Glyph = "\uE72B", FontSize = 16 }, Background = null };
        back.Click += (s, e) => Frame.GoBack();
        Grid.SetColumn(back, 0);
        topBar.Children.Add(back);
        var title = new TextBlock
        {
            Text = Loc.Tr("edit_provider"),
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
        scroll.Content = panel;

        // ===== basic info card =====
        var basicCard = MakeCard();
        var basicPanel = new StackPanel { Spacing = 12 };
        _nameBox.Header = Loc.Tr("provider_name");
        _nameBox.LostFocus += (s, e) => SaveProvider(p => p.Name = _nameBox.Text.Trim());
        basicPanel.Children.Add(_nameBox);

        if (Provider is ICredentialSetting)
        {
            _apiKeyBox.Header = Loc.Tr("api_key");
            _apiKeyBox.LostFocus += (s, e) => SaveProvider(p =>
            {
                if (p is ICredentialSetting c) c.ApiKey = _apiKeyBox.Password;
            });
            basicPanel.Children.Add(_apiKeyBox);

            _baseUrlBox.Header = Loc.Tr("base_url");
            _baseUrlBox.LostFocus += (s, e) => SaveProvider(p =>
            {
                if (p is ICredentialSetting c) c.BaseUrl = _baseUrlBox.Text.Trim();
            });
            basicPanel.Children.Add(_baseUrlBox);

            if (Provider is OpenAISetting)
            {
                _chatCompletionsBox.Header = "Chat Completions Path";
                _chatCompletionsBox.PlaceholderText = "/chat/completions";
                _chatCompletionsBox.LostFocus += (s, e) => SaveProvider(p =>
                {
                    if (p is OpenAISetting o) o.ChatCompletionsPath = _chatCompletionsBox.Text.Trim();
                });
                basicPanel.Children.Add(_chatCompletionsBox);
            }
        }
        basicCard.Child = basicPanel;
        panel.Children.Add(basicCard);

        // ===== models card =====
        var modelsCard = MakeCard();
        var modelsPanel = new StackPanel { Spacing = 8 };
        var modelsHeader = new Grid();
        modelsHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        modelsHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        modelsHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var modelsTitle = new TextBlock { Text = Loc.Tr("models"), FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        modelsTitle.SetResourceReference(TextBlock.ForegroundProperty, "M3OnSurface");
        Grid.SetColumn(modelsTitle, 0);
        modelsHeader.Children.Add(modelsTitle);

        var fetchRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        fetchRow.Children.Add(_fetching);
        fetchRow.Children.Add(_fetchStatus);
        Grid.SetColumn(fetchRow, 1);
        modelsHeader.Children.Add(fetchRow);

        var fetchBtn = new Button { Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new FontIcon { Glyph = "\uE895", FontSize = 14 }, new TextBlock { Text = Loc.Tr("fetch_models") } } } };
        fetchBtn.Click += async (s, e) => await FetchModelsAsync();
        Grid.SetColumn(fetchBtn, 2);
        modelsHeader.Children.Add(fetchBtn);
        modelsPanel.Children.Add(modelsHeader);

        _modelList.Spacing = 4;
        modelsPanel.Children.Add(_modelList);

        var addModelBtn = new Button
        {
            Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new FontIcon { Glyph = "\uE710", FontSize = 14 }, new TextBlock { Text = Loc.Tr("add_model") } } },
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        addModelBtn.Click += async (s, e) => await AddModelAsync();
        modelsPanel.Children.Add(addModelBtn);
        modelsCard.Child = modelsPanel;
        panel.Children.Add(modelsCard);

        // ===== danger zone =====
        var deleteBtn = new Button { Content = Loc.Tr("delete"), Style = null };
        deleteBtn.Click += async (s, e) =>
        {
            var provider = Provider;
            if (provider == null) return;
            var dlg = new ContentDialog
            {
                Title = Loc.Tr("delete_provider_confirm"),
                PrimaryButtonText = Loc.Tr("delete"),
                CloseButtonText = Loc.Tr("cancel"),
                XamlRoot = Content.XamlRoot,
            };
            if (await dlg.ShowAsync() == ContentDialogResult.Primary)
            {
                _ctx.Settings.Providers.Remove(provider);
                _ctx.SaveSettings();
                Frame.GoBack();
            }
        };
        deleteBtn.Background = new SolidColorBrush(M3Theme.Current.ErrorContainer);
        deleteBtn.Foreground = new SolidColorBrush(M3Theme.Current.OnErrorContainer);
        panel.Children.Add(deleteBtn);

        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);
        Content = root;
    }

    private static Border MakeCard() => new()
    {
        Padding = new Thickness(16),
        CornerRadius = new Microsoft.UI.Xaml.CornerRadius(16),
        Background = new SolidColorBrush(M3Theme.Current.SurfaceContainer),
    };

    private void Rebind()
    {
        var provider = Provider;
        if (provider == null) { Frame.GoBack(); return; }
        _nameBox.Text = provider.Name;
        if (provider is ICredentialSetting c)
        {
            _apiKeyBox.Password = c.ApiKey;
            _baseUrlBox.Text = c.BaseUrl;
        }
        if (provider is OpenAISetting o)
        {
            _chatCompletionsBox.Text = o.ChatCompletionsPath;
        }
        RebindModels();
    }

    private void RebindModels()
    {
        var provider = Provider;
        if (provider == null) return;
        _modelList.Children.Clear();
        foreach (var model in provider.Models)
        {
            var m = model;
            var row = new Grid
            {
                Padding = new Thickness(12, 8, 12, 8),
                CornerRadius = new Microsoft.UI.Xaml.CornerRadius(12),
                Background = new SolidColorBrush(M3Theme.Current.SurfaceContainerHigh),
            };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var info = new StackPanel();
            var name = new TextBlock { Text = string.IsNullOrEmpty(m.DisplayName) ? m.ModelId : m.DisplayName, FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
            name.SetResourceReference(TextBlock.ForegroundProperty, "M3OnSurface");
            var id = new TextBlock { Text = m.ModelId, FontSize = 11, Opacity = 0.6 };
            info.Children.Add(name);
            info.Children.Add(id);
            Grid.SetColumn(info, 0);
            row.Children.Add(info);

            var abilities = new TextBlock
            {
                Text = string.Join(" ", new[]
                {
                    m.SupportVision ? "👁" : "",
                    m.SupportTool ? "🔧" : "",
                    m.SupportReasoning ? "🧠" : "",
                }.Where(x => x.Length > 0)),
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(abilities, 1);
            row.Children.Add(abilities);

            var del = new Button { Content = new FontIcon { Glyph = "\uE74D", FontSize = 12 }, Background = null, Padding = new Thickness(4) };
            del.Click += async (s, e) =>
            {
                var dlg = new ContentDialog
                {
                    Title = Loc.Tr("delete_model_confirm"),
                    PrimaryButtonText = Loc.Tr("delete"),
                    CloseButtonText = Loc.Tr("cancel"),
                    XamlRoot = Content.XamlRoot,
                };
                if (await dlg.ShowAsync() == ContentDialogResult.Primary)
                {
                    SaveProvider(p =>
                    {
                        var np = p.DelModel(m);
                        p.Models = np.Models;
                    });
                    RebindModels();
                }
            };
            Grid.SetColumn(del, 2);
            row.Children.Add(del);

            var editBtn = new Button { Content = row, Background = null, Padding = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch };
            editBtn.Click += async (s, e) => await EditModelAsync(m);
            _modelList.Children.Add(editBtn);
        }
    }

    private async Task FetchModelsAsync()
    {
        var provider = Provider;
        if (provider == null) return;
        _fetching.IsActive = true;
        _fetchStatus.Text = Loc.Tr("fetching_models");
        try
        {
            var impl = _ctx.Providers.Get(provider);
            var models = await impl.ListModelsAsync(provider);
            // merge: keep existing customizations, add new ones
            SaveProvider(p =>
            {
                var existing = p.Models.ToDictionary(m => m.ModelId);
                var merged = new List<Model>();
                foreach (var fetched in models)
                {
                    if (existing.TryGetValue(fetched.ModelId, out var old))
                        merged.Add(old);
                    else
                        merged.Add(fetched);
                }
                // preserve models not in the fetched list (custom additions)
                foreach (var old in p.Models)
                    if (!merged.Any(m => m.ModelId == old.ModelId))
                        merged.Add(old);
                p.Models = merged;
            });
            _fetchStatus.Text = Loc.Tr("fetch_models_ok", models.Count);
            RebindModels();
        }
        catch (Exception ex)
        {
            _fetchStatus.Text = $"{Loc.Tr("fetch_models_fail")}: {ex.Message}";
        }
        finally
        {
            _fetching.IsActive = false;
        }
    }

    private async Task AddModelAsync() => await EditModelAsync(null);

    private async Task EditModelAsync(Model? model)
    {
        var idBox = new TextBox { Header = Loc.Tr("model_id"), Text = model?.ModelId ?? "" };
        var nameBox = new TextBox { Header = Loc.Tr("model_name"), Text = model?.DisplayName ?? "" };
        var toolsCheck = new CheckBox { Content = Loc.Tr("tools") + " (Tool)", IsChecked = model?.Abilities.Contains(ModelAbility.Tool) ?? false };
        var reasoningCheck = new CheckBox { Content = Loc.Tr("thinking") + " (Reasoning)", IsChecked = model?.Abilities.Contains(ModelAbility.Reasoning) ?? false };
        var visionCheck = new CheckBox { Content = Loc.Tr("image") + " (Vision)", IsChecked = model?.InputModalities.Contains(Modality.Image) ?? false };

        var panel = new StackPanel { Spacing = 12, MinWidth = 380 };
        panel.Children.Add(idBox);
        panel.Children.Add(nameBox);
        panel.Children.Add(toolsCheck);
        panel.Children.Add(reasoningCheck);
        panel.Children.Add(visionCheck);

        var dlg = new ContentDialog
        {
            Title = model == null ? Loc.Tr("add_model") : Loc.Tr("edit"),
            Content = panel,
            PrimaryButtonText = Loc.Tr("save"),
            CloseButtonText = Loc.Tr("cancel"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        if (string.IsNullOrWhiteSpace(idBox.Text)) return;

        var newModel = (model ?? new Model()).Copy();
        newModel.ModelId = idBox.Text.Trim();
        newModel.DisplayName = string.IsNullOrWhiteSpace(nameBox.Text) ? idBox.Text.Trim() : nameBox.Text.Trim();
        newModel.Abilities.Clear();
        if (toolsCheck.IsChecked == true) newModel.Abilities.Add(ModelAbility.Tool);
        if (reasoningCheck.IsChecked == true) newModel.Abilities.Add(ModelAbility.Reasoning);
        newModel.InputModalities.Clear();
        newModel.InputModalities.Add(Modality.Text);
        if (visionCheck.IsChecked == true) newModel.InputModalities.Add(Modality.Image);

        SaveProvider(p =>
        {
            var np = p.Models.Any(x => x.Id == newModel.Id) ? p.EditModel(newModel) : p.AddModel(newModel);
            p.Models = np.Models;
        });
        RebindModels();
    }
}
