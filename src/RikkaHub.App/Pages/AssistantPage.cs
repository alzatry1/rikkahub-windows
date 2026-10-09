using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using RikkaHub.App.Services;
using RikkaHub.App.Theme;
using RikkaHub.Core.Model;

namespace RikkaHub.App.Pages;

/// <summary>Assistant page — port of the Android assistant list/edit UI (agent customization).</summary>
public class AssistantPage : Page
{
    private AppCtx _ctx = null!;
    private readonly StackPanel _list = new();

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
            Text = Loc.Tr("assistants"),
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
        };
        title.Foreground = new SolidColorBrush(M3Theme.Current.OnSurface);
        Grid.SetColumn(title, 1);
        topBar.Children.Add(title);

        var addBtn = new Button
        {
            Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new FontIcon { Glyph = "\uE710", FontSize = 14 }, new TextBlock { Text = Loc.Tr("new_assistant") } } },
        };
        addBtn.Click += async (s, e) => await EditAssistantAsync(null);
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
        foreach (var assistant in _ctx.Settings.Assistants.OrderByDescending(a => a.Pinned))
        {
            var a = assistant;
            var card = new Grid
            {
                Padding = new Thickness(16, 12, 16, 12),
                CornerRadius = new Microsoft.UI.Xaml.CornerRadius(16),
                Background = new SolidColorBrush(M3Theme.Current.SurfaceContainer),
            };
            card.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0, GridUnitType.Auto) });
            card.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            card.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0, GridUnitType.Auto) });

            var emoji = new TextBlock { Text = string.IsNullOrEmpty(a.Icon) ? "🤖" : a.Icon, FontSize = 24 };
            Grid.SetColumn(emoji, 0);
            card.Children.Add(emoji);

            var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
            var name = new TextBlock { Text = a.Name, FontSize = 15, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
            name.Foreground = new SolidColorBrush(M3Theme.Current.OnSurface);
            info.Children.Add(name);
            if (!string.IsNullOrEmpty(a.Description))
            {
                var desc = new TextBlock { Text = a.Description, FontSize = 12, Opacity = 0.6, TextTrimming = TextTrimming.CharacterEllipsis };
                info.Children.Add(desc);
            }
            Grid.SetColumn(info, 1);
            card.Children.Add(info);

            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            var useBtn = new Button { Content = new FontIcon { Glyph = "\uE8FB", FontSize = 14 }, Background = null, Padding = new Thickness(6) };
            ToolTipService.SetToolTip(useBtn, Loc.Tr("confirm"));
            useBtn.Click += (s, e) =>
            {
                _ctx.Settings.AssistantId = a.Id;
                if (_ctx.Conversation?.Config != null)
                    _ctx.Conversation.Config.AssistantId = a.Id;
                _ctx.SaveSettings();
                _ctx.SaveCurrentConversation();
                Frame.GoBack();
            };
            actions.Children.Add(useBtn);
            Grid.SetColumn(actions, 2);
            card.Children.Add(actions);

            var btn = new Button
            {
                Content = card,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Background = null,
                Padding = new Thickness(0),
            };
            btn.Click += async (s, e) => await EditAssistantAsync(a);
            _list.Children.Add(btn);
        }
    }

    private async Task EditAssistantAsync(Assistant? assistant)
    {
        var isEdit = assistant != null;
        var nameBox = new TextBox { Header = Loc.Tr("assistant_name"), Text = assistant?.Name ?? "" };
        var iconBox = new TextBox { Header = Loc.Tr("assistant_icon"), Text = assistant?.Icon ?? "🤖" };
        var descBox = new TextBox { Header = Loc.Tr("assistant_desc"), Text = assistant?.Description ?? "" };
        var promptBox = new TextBox
        {
            Header = Loc.Tr("system_prompt"),
            Text = assistant?.SystemPrompt ?? "",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 120,
            MaxHeight = 240,
        };

        var panel = new StackPanel { Spacing = 12, MinWidth = 420 };
        panel.Children.Add(nameBox);
        panel.Children.Add(iconBox);
        panel.Children.Add(descBox);
        panel.Children.Add(promptBox);

        var dlg = new ContentDialog
        {
            Title = isEdit ? Loc.Tr("edit") : Loc.Tr("new_assistant"),
            Content = panel,
            PrimaryButtonText = Loc.Tr("save"),
            CloseButtonText = Loc.Tr("cancel"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;

        var a = assistant?.Copy() ?? new Assistant();
        a.Name = nameBox.Text.Trim();
        a.Icon = string.IsNullOrWhiteSpace(iconBox.Text) ? "🤖" : iconBox.Text.Trim();
        a.Description = descBox.Text.Trim();
        a.SystemPrompt = promptBox.Text;

        if (isEdit)
        {
            var idx = _ctx.Settings.Assistants.FindIndex(x => x.Id == a.Id);
            _ctx.Settings.Assistants[idx] = a;
        }
        else
        {
            _ctx.Settings.Assistants.Add(a);
        }
        _ctx.SaveSettings();
        Rebind();
    }
}
