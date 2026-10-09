using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using RikkaHub.App.Services;
using RikkaHub.App.Theme;

namespace RikkaHub.App.Pages;

/// <summary>History page — conversation list (port of the chat drawer conversation list).</summary>
public class HistoryPage : Page
{
    private AppCtx _ctx = null!;
    private readonly ListView _list = new();
    private readonly TextBox _search = new();

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _ctx = (AppCtx)e.Parameter;
        _ctx.ConversationListChanged += OnListChanged;
        Build();
        Rebind();
    }

    protected override void OnNavigatedFrom(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        _ctx.ConversationListChanged -= OnListChanged;
    }

    private void OnListChanged() => DispatcherQueue.TryEnqueue(Rebind);

    private void Build()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(56) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(0, GridUnitType.Auto) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var topBar = new Grid();
        topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0, GridUnitType.Auto) });
        topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var back = new Button { Content = new FontIcon { Glyph = "\uE72B", FontSize = 16 }, Background = null };
        back.Click += (s, e) => Frame.GoBack();
        Grid.SetColumn(back, 0);
        topBar.Children.Add(back);
        var title = new TextBlock
        {
            Text = Loc.Tr("history"),
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
        };
        title.SetResourceReference(TextBlock.ForegroundProperty, "M3OnSurface");
        Grid.SetColumn(title, 1);
        topBar.Children.Add(title);
        var newBtn = new Button
        {
            Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new FontIcon { Glyph = "\uE710", FontSize = 14 }, new TextBlock { Text = Loc.Tr("new_chat") } } },
        };
        newBtn.Click += (s, e) =>
        {
            _ctx.NewConversation();
            Frame.GoBack();
        };
        Grid.SetColumn(newBtn, 2);
        topBar.Children.Add(newBtn);
        Grid.SetRow(topBar, 0);
        root.Children.Add(topBar);

        _search.PlaceholderText = Loc.Tr("search_conversations");
        _search.Margin = new Thickness(16, 4, 16, 4);
        _search.TextChanged += (s, e) => Rebind();
        Grid.SetRow(_search, 1);
        root.Children.Add(_search);

        _list.SelectionMode = ListViewSelectionMode.Single;
        _list.IsItemClickEnabled = false;
        _list.SelectionChanged += (s, e) =>
        {
            if (_list.SelectedItem is ListViewItem { Tag: Guid id })
            {
                _ctx.OpenConversation(id);
                Frame.GoBack();
                _list.SelectedItem = null;
            }
        };
        Grid.SetRow(_list, 2);
        root.Children.Add(_list);

        Content = root;
    }

    private void Rebind()
    {
        _list.Items.Clear();
        var q = _search.Text?.Trim() ?? "";
        foreach (var meta in _ctx.Conversations)
        {
            if (!string.IsNullOrEmpty(q) && !meta.Title.Contains(q, StringComparison.OrdinalIgnoreCase)) continue;
            var item = new ListViewItem { HorizontalContentAlignment = HorizontalAlignment.Stretch };
            var grid = new Grid { Padding = new Thickness(16, 10, 8, 10) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0, GridUnitType.Auto) });
            var info = new StackPanel();
            var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            if (meta.Pinned)
            {
                titleRow.Children.Add(new FontIcon { Glyph = "\uE840", FontSize = 12, Foreground = new SolidColorBrush(M3Theme.Current.Primary) });
            }
            var title = new TextBlock
            {
                Text = string.IsNullOrEmpty(meta.Title) ? Loc.Tr("new_chat") : meta.Title,
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            title.SetResourceReference(TextBlock.ForegroundProperty, "M3OnSurface");
            titleRow.Children.Add(title);
            info.Children.Add(titleRow);
            var time = new TextBlock
            {
                Text = FormatTime(meta.UpdateAt),
                FontSize = 11,
                Opacity = 0.55,
            };
            info.Children.Add(time);
            Grid.SetColumn(info, 0);
            grid.Children.Add(info);

            var menu = new Button { Content = new FontIcon { Glyph = "\uE712", FontSize = 12 }, Background = null, Padding = new Thickness(4) };
            var captured = meta;
            menu.Click += (s, e) => ShowItemMenu(menu, captured);
            Grid.SetColumn(menu, 1);
            grid.Children.Add(menu);

            item.Content = grid;
            item.Tag = meta.Id;
            _list.Items.Add(item);
        }
        if (_list.Items.Count == 0)
        {
            _list.Items.Add(new ListViewItem
            {
                Content = new TextBlock { Text = Loc.Tr("no_conversations"), FontSize = 13, Opacity = 0.5, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 24, 0, 0) },
                IsEnabled = false,
            });
        }
    }

    private void ShowItemMenu(FrameworkElement anchor, AppCtx.ConversationMetaItem meta)
    {
        var menu = new MenuFlyout();
        var pin = new MenuFlyoutItem { Text = meta.Pinned ? Loc.Tr("unpin") : Loc.Tr("pin") };
        pin.Click += (s, e) =>
        {
            var conv = _ctx.Store.LoadConversation(meta.Id);
            if (conv != null)
            {
                conv.Pinned = !conv.Pinned;
                _ctx.Store.SaveConversation(conv);
                _ctx.ReloadConversations();
            }
        };
        menu.Items.Add(pin);

        var del = new MenuFlyoutItem { Text = Loc.Tr("delete") };
        del.Click += async (s, e) =>
        {
            var dlg = new ContentDialog
            {
                Title = Loc.Tr("delete_conversation_confirm"),
                PrimaryButtonText = Loc.Tr("delete"),
                CloseButtonText = Loc.Tr("cancel"),
                XamlRoot = Content.XamlRoot,
            };
            if (await dlg.ShowAsync() == ContentDialogResult.Primary)
            {
                _ctx.DeleteConversation(meta.Id);
            }
        };
        menu.Items.Add(del);
        menu.ShowAt(anchor);
    }

    private static string FormatTime(long unixMs)
    {
        try
        {
            var dt = DateTimeOffset.FromUnixTimeMilliseconds(unixMs).ToLocalTime();
            var now = DateTimeOffset.Now;
            if (dt.Date == now.Date) return dt.ToString("HH:mm");
            if ((now - dt).TotalDays < 7) return dt.ToString("ddd HH:mm");
            return dt.ToString("yyyy-MM-dd");
        }
        catch { return ""; }
    }
}
