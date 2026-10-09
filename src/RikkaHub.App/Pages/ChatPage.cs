using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using RikkaHub.App.Services;
using RikkaHub.App.Theme;
using RikkaHub.Core.Provider;

namespace RikkaHub.App.Pages;

/// <summary>
/// Chat page — 1:1 layout port of the Android ChatPage: top bar with model picker,
/// message list (markdown via WebView2, identical to the Android renderer), input bar.
/// </summary>
public class ChatPage : Page
{
    private readonly Grid _root = new();
    private readonly StackPanel _messageStack = new();
    private readonly ScrollViewer _scroller = new();
    private readonly TextBox _input = new();
    private readonly Button _sendBtn = new();
    private readonly TextBlock _modelChip = new();
    private readonly TextBlock _convTitle = new();
    private readonly Grid _emptyState = new();
    private readonly StackPanel _attachRow = new();
    private readonly List<ImagePart> _attachments = new();
    private readonly Dictionary<Guid, MessageItem> _items = new();
    private bool _autoScroll = true;

    private AppCtx? _ctxRef;

    private AppCtx Ctx => _ctxRef ?? throw new InvalidOperationException("ChatPage navigated without context");

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _ctxRef = e.Parameter as AppCtx ?? throw new InvalidOperationException("ChatPage requires AppCtx parameter");
        BuildUi();
        HookEvents();
        RebindAll();
    }

    private void BuildUi()
    {
        _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(56) });   // top bar
        _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });      // messages
        _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(0, GridUnitType.Auto) });      // attachments
        _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(0, GridUnitType.Auto) });      // input bar
        _root.Background = new SolidColorBrush(M3Theme.Current.Background);

        // ===== top bar =====
        var topBar = new Grid();
        topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0, GridUnitType.Auto) });
        topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0, GridUnitType.Auto) });
        topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0, GridUnitType.Auto) });

        _convTitle.VerticalAlignment = VerticalAlignment.Center;
        _convTitle.FontSize = 16;
        _convTitle.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        _convTitle.Margin = new Thickness(8, 0, 8, 0);
        _convTitle.Foreground = new SolidColorBrush(M3Theme.Current.OnSurface);
        Grid.SetColumn(_convTitle, 1);
        topBar.Children.Add(_convTitle);

        // model chip (opens model picker)
        var modelChipHost = new Button
        {
            Content = MakeChipContent(_modelChip),
            Background = null,
            Padding = new Thickness(8, 4, 8, 4),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(modelChipHost, 2);
        modelChipHost.Click += (s, e) => ShowModelPicker(modelChipHost);
        topBar.Children.Add(modelChipHost);

        var moreBtn = new Button
        {
            Content = new FontIcon { Glyph = "\uE712", FontSize = 16 },
            Background = null,
            Padding = new Thickness(8),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(moreBtn, 3);
        moreBtn.Click += (s, e) => ShowMoreMenu(moreBtn);
        topBar.Children.Add(moreBtn);
        Grid.SetRow(topBar, 0);
        _root.Children.Add(topBar);

        // ===== messages =====
        _scroller.Content = _messageStack;
        _scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        Grid.SetRow(_scroller, 1);
        _root.Children.Add(_scroller);

        // ===== empty state =====
        BuildEmptyState();
        Grid.SetRow(_emptyState, 1);
        _root.Children.Add(_emptyState);

        // ===== attachment row =====
        _attachRow.Orientation = Orientation.Horizontal;
        _attachRow.Spacing = 8;
        _attachRow.Padding = new Thickness(16, 8, 16, 0);
        Grid.SetRow(_attachRow, 2);
        _root.Children.Add(_attachRow);

        // ===== input bar =====
        var inputGrid = new Grid();
        inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0, GridUnitType.Auto) });
        inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0, GridUnitType.Auto) });
        inputGrid.Padding = new Thickness(12, 8, 12, 12);

        var attachBtn = new Button
        {
            Content = new FontIcon { Glyph = "\uEB9F", FontSize = 18 }, // Photo2
            Background = null,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 8, 4),
        };
        attachBtn.Click += async (s, e) => await PickImagesAsync();
        Grid.SetColumn(attachBtn, 0);
        inputGrid.Children.Add(attachBtn);

        _input.PlaceholderText = Loc.Tr("input_placeholder");
        _input.AcceptsReturn = true;
        _input.TextWrapping = TextWrapping.Wrap;
        _input.MaxHeight = 160;
        _input.FontSize = 14;
        _input.VerticalAlignment = VerticalAlignment.Bottom;
        _input.KeyDown += OnInputKeyDown;
        Grid.SetColumn(_input, 1);
        inputGrid.Children.Add(_input);

        _sendBtn.Content = new FontIcon { Glyph = "\uE724", FontSize = 18 }; // Send
        _sendBtn.Width = 44;
        _sendBtn.Height = 40;
        _sendBtn.VerticalAlignment = VerticalAlignment.Bottom;
        _sendBtn.Margin = new Thickness(8, 0, 0, 0);
        _sendBtn.CornerRadius = new Microsoft.UI.Xaml.CornerRadius(20);
        _sendBtn.Click += OnSendClick;
        Grid.SetColumn(_sendBtn, 2);
        inputGrid.Children.Add(_sendBtn);

        Grid.SetRow(inputGrid, 3);
        _root.Children.Add(inputGrid);

        Content = _root;
    }

    private static object MakeChipContent(TextBlock chip)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var icon = new FontIcon { Glyph = "\uE945", FontSize = 14 }; // Lightbulb? use robot
        chip.FontSize = 13;
        chip.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        panel.Children.Add(icon);
        panel.Children.Add(chip);
        return panel;
    }

    private void BuildEmptyState()
    {
        var panel = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 16,
        };
        var title = new TextBlock
        {
            Text = Loc.Tr("empty_chat_title"),
            FontSize = 22,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        title.Foreground = new SolidColorBrush(M3Theme.Current.OnSurface);
        var hint = new TextBlock
        {
            Text = Loc.Tr("no_models_hint"),
            FontSize = 13,
            HorizontalAlignment = HorizontalAlignment.Center,
            Opacity = 0.6,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 360,
            TextAlignment = TextAlignment.Center,
        };
        panel.Children.Add(title);
        panel.Children.Add(hint);
        _emptyState.Children.Add(panel);
    }

    private void HookEvents()
    {
        Ctx.ConversationChanged += OnConversationChanged;
        Ctx.Chat.StateChanged += OnStateChanged;
        Ctx.ConversationListChanged += OnConversationListChanged;
        M3Theme.Changed += OnThemeChanged;
    }

    private void OnThemeChanged(M3Palette p) => DispatcherQueue.TryEnqueue(() =>
    {
        _root.Background = new SolidColorBrush(p.Background);
        RebindAll();
    });

    private void OnConversationListChanged() => DispatcherQueue.TryEnqueue(() => UpdateTitle());

    private void UpdateTitle()
    {
        _convTitle.Text = Ctx.Conversation?.Title ?? "";
        var model = Ctx.ChatModel;
        _modelChip.Text = model?.DisplayName ?? Loc.Tr("select_model");
    }

    private void OnConversationChanged() => DispatcherQueue.TryEnqueue(() =>
    {
        RebindAll();
        UpdateTitle();
    });

    private long _lastUiRefresh;

    private void OnStateChanged() => DispatcherQueue.TryEnqueue(() =>
    {
        var conv = Ctx.Conversation;
        if (conv == null) return;
        var generating = Ctx.Chat.IsGenerating;
        var now = System.Diagnostics.Stopwatch.GetTimestamp();
        var elapsedMs = System.Diagnostics.Stopwatch.GetElapsedTime(_lastUiRefresh).TotalMilliseconds;
        // throttle UI refresh during streaming (~80ms) but always refresh on completion
        if (generating && _lastUiRefresh != 0 && elapsedMs < 80) return;
        _lastUiRefresh = now;
        // update existing items' content (MarkdownWebView throttles internal updates)
        foreach (var msg in conv.Messages)
        {
            if (_items.TryGetValue(msg.Id, out var item))
                item.Bind(msg);
        }
        // a new assistant message may have appeared (generation started)
        if (conv.Messages.Count != _items.Count) RebindAll();
        UpdateSendState();
        if (_autoScroll) ScrollToEnd();
    });

    private void UpdateSendState()
    {
        var generating = Ctx.Chat.IsGenerating;
        ((FontIcon)_sendBtn.Content).Glyph = generating ? "\uE715" : "\uE724"; // pause vs send
        _sendBtn.IsEnabled = generating || !string.IsNullOrWhiteSpace(_input.Text) || _attachments.Count > 0;
        if (!generating) _input.Focus(FocusState.Programmatic);
    }

    private void RebindAll()
    {
        var conv = Ctx.Conversation;
        _messageStack.Children.Clear();
        _items.Clear();
        _emptyState.Visibility = (conv == null || conv.Messages.Count == 0) ? Visibility.Visible : Visibility.Collapsed;
        if (conv == null) return;
        foreach (var msg in conv.Messages)
        {
            var item = new MessageItem(Ctx);
            item.Bind(msg);
            _items[msg.Id] = item;
            _messageStack.Children.Add(item);
        }
        UpdateTitle();
        UpdateSendState();
        _ = DispatcherQueue.TryEnqueue(() => ScrollToEnd());
    }

    private void ScrollToEnd()
    {
        _scroller.ChangeView(null, _scroller.ScrollableHeight + 100, null, disableAnimation: true);
    }

    // ===== input handling =====

    private void OnInputKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            var shift = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift)
                .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
            if (!shift && Ctx.Settings.SendOnEnter)
            {
                e.Handled = true;
                TrySend();
            }
        }
    }

    private async void OnSendClick(object sender, RoutedEventArgs e)
    {
        if (Ctx.Chat.IsGenerating)
        {
            Ctx.Chat.Stop();
            return;
        }
        await TrySendAsync();
    }

    private void TrySend() => _ = TrySendAsync();

    private async Task TrySendAsync()
    {
        var text = _input.Text.Trim();
        var images = _attachments.Count > 0 ? _attachments.ToList() : null;
        if (text.Length == 0 && (images?.Count ?? 0) == 0) return;
        if (Ctx.ChatModels.Count == 0)
        {
            var dlg = new ContentDialog
            {
                Title = Loc.Tr("no_provider_hint"),
                PrimaryButtonText = Loc.Tr("go_to_settings"),
                CloseButtonText = Loc.Tr("cancel"),
                XamlRoot = _root.XamlRoot,
            };
            var r = await dlg.ShowAsync();
            if (r == ContentDialogResult.Primary)
            {
                global::RikkaHub.App.App.Current.MainWindow?.Navigate("settings");
            }
            return;
        }
        _input.Text = "";
        _attachments.Clear();
        RebuildAttachRow();
        _autoScroll = true;
        await Ctx.Chat.SendMessage(text, images);
    }

    private async Task PickImagesAsync()
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".webp");
        picker.FileTypeFilter.Add(".gif");
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
        WinRT.Interop.InitializeWithWindow.Initialize(picker, global::RikkaHub.App.App.Current.WindowHandle);
        var files = await picker.PickMultipleFilesAsync();
        foreach (var file in files)
        {
            using var ras = await file.OpenReadAsync();
            var size = (int)ras.Size;
            using var reader = new Windows.Storage.Streams.DataReader(ras);
            await reader.LoadAsync((uint)size);
            var data = new byte[size];
            reader.ReadBytes(data);
            var mime = file.ContentType switch
            {
                "image/png" => "image/png",
                "image/webp" => "image/webp",
                "image/gif" => "image/gif",
                _ => "image/jpeg",
            };
            _attachments.Add(new ImagePart
            {
                Data = Convert.ToBase64String(data),
                MimeType = mime,
            });
        }
        RebuildAttachRow();
    }

    private void RebuildAttachRow()
    {
        _attachRow.Children.Clear();
        foreach (var img in _attachments)
        {
            var thumb = new Image
            {
                Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(img.ToDisplayUrl())),
                Width = 56,
                Height = 56,
                Stretch = Stretch.UniformToFill,
            };
            var border = new Grid
            {
                Width = 60,
                Height = 60,
                CornerRadius = new Microsoft.UI.Xaml.CornerRadius(8),
            };
            border.Children.Add(thumb);
            var remove = new Button
            {
                Content = new FontIcon { Glyph = "\uE711", FontSize = 10 },
                Width = 18,
                Height = 18,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Thickness(0),
            };
            var toRemove = img;
            remove.Click += (s, e) => { _attachments.Remove(toRemove); RebuildAttachRow(); };
            border.Children.Add(remove);
            _attachRow.Children.Add(border);
        }
        _attachRow.Visibility = _attachments.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // ===== model picker =====

    private void ShowModelPicker(FrameworkElement anchor)
    {
        var flyout = new Flyout();
        var panel = new StackPanel { Width = 320, MaxHeight = 480 };
        var search = new TextBox { PlaceholderText = Loc.Tr("search_models") };
        search.Margin = new Thickness(0, 0, 0, 8);
        panel.Children.Add(search);

        var list = new ListView
        {
            MaxHeight = 400,
            SelectionMode = ListViewSelectionMode.Single,
        };
        var models = Ctx.ChatModels;

        void RebuildList(string q)
        {
            list.Items.Clear();
            var filtered = models.Where(m =>
                string.IsNullOrEmpty(q) ||
                m.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                m.ModelId.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
            // group by provider
            var byProvider = Ctx.Settings.Providers.Where(p => p.Enabled);
            foreach (var provider in byProvider)
            {
                var providerModels = filtered.Where(m => provider.Models.Any(pm => pm.Id == m.Id)).ToList();
                if (providerModels.Count == 0) continue;
                list.Items.Add(new ListViewHeaderItem
                {
                    Content = provider.Name,
                    FontSize = 12,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    IsHitTestVisible = false,
                    IsEnabled = false,
                });
                foreach (var m in providerModels)
                {
                    var itemPanel = new Grid { Padding = new Thickness(12, 8, 12, 8) };
                    itemPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    itemPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0, GridUnitType.Auto) });
                    var name = new StackPanel();
                    var nameText = new TextBlock { Text = m.DisplayName, FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
                    var idText = new TextBlock { Text = m.ModelId, FontSize = 11, Opacity = 0.6 };
                    name.Children.Add(nameText);
                    name.Children.Add(idText);
                    Grid.SetColumn(name, 0);
                    itemPanel.Children.Add(name);
                    if (Ctx.Settings.FavoriteModels.Contains(m.Id))
                    {
                        var fav = new FontIcon { Glyph = "\uE735", FontSize = 14, Foreground = new SolidColorBrush(M3Theme.Current.Tertiary) };
                        Grid.SetColumn(fav, 1);
                        itemPanel.Children.Add(fav);
                    }
                    list.Items.Add(new ListViewItem { Content = itemPanel, Tag = m, HorizontalContentAlignment = HorizontalAlignment.Stretch });
                }
            }
        }

        RebuildList("");
        search.TextChanged += (s, e) => RebuildList(search.Text);

        list.SelectionChanged += (s, e) =>
        {
            if (list.SelectedItem is ListViewItem { Tag: Model m })
            {
                Ctx.Settings.ChatModelId = m.Id;
                if (Ctx.Conversation != null)
                {
                    Ctx.Conversation.Config ??= new Core.Model.ConversationConfig();
                    Ctx.Conversation.Config.ModelId = m.Id;
                }
                Ctx.SaveSettings();
                Ctx.SaveCurrentConversation();
                UpdateTitle();
                flyout.Hide();
                list.SelectedItem = null;
            }
        };

        panel.Children.Add(list);
        flyout.Content = panel;
        flyout.ShowAt(anchor);
    }

    private void ShowMoreMenu(FrameworkElement anchor)
    {
        var menu = new MenuFlyout();
        var conv = Ctx.Conversation;

        var rename = new MenuFlyoutItem { Text = Loc.Tr("rename"), Icon = new FontIcon { Glyph = "\uE8AC" } };
        rename.Click += async (s, e) => await RenameConversationAsync();
        menu.Items.Add(rename);

        var clear = new MenuFlyoutItem { Text = Loc.Tr("clear_conversation"), Icon = new FontIcon { Glyph = "\uE74D" } };
        clear.Click += (s, e) =>
        {
            if (conv != null)
            {
                conv.Messages.Clear();
                conv.Title = "";
                Ctx.SaveCurrentConversation();
                RebindAll();
            }
        };
        menu.Items.Add(clear);

        var del = new MenuFlyoutItem { Text = Loc.Tr("delete"), Icon = new FontIcon { Glyph = "\uE74D" } };
        del.Click += async (s, e) => await DeleteConversationAsync();
        menu.Items.Add(del);

        menu.ShowAt(anchor);
    }

    private async Task RenameConversationAsync()
    {
        var conv = Ctx.Conversation;
        if (conv == null) return;
        var input = new TextBox { Text = conv.Title, Header = Loc.Tr("title") };
        var dlg = new ContentDialog
        {
            Title = Loc.Tr("rename_conversation"),
            Content = input,
            PrimaryButtonText = Loc.Tr("confirm"),
            CloseButtonText = Loc.Tr("cancel"),
            XamlRoot = _root.XamlRoot,
        };
        if (await dlg.ShowAsync() == ContentDialogResult.Primary)
        {
            conv.Title = input.Text.Trim();
            Ctx.SaveCurrentConversation();
            UpdateTitle();
        }
    }

    private async Task DeleteConversationAsync()
    {
        var conv = Ctx.Conversation;
        if (conv == null) return;
        var dlg = new ContentDialog
        {
            Title = Loc.Tr("delete_conversation_confirm"),
            PrimaryButtonText = Loc.Tr("delete"),
            CloseButtonText = Loc.Tr("cancel"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = _root.XamlRoot,
        };
        if (await dlg.ShowAsync() == ContentDialogResult.Primary)
        {
            Ctx.DeleteConversation(conv.Id);
        }
    }
}
