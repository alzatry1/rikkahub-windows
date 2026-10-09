using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using RikkaHub.App.Pages.Components;
using RikkaHub.App.Services;
using RikkaHub.App.Theme;
using RikkaHub.Core.Provider;

namespace RikkaHub.App.Pages;

/// <summary>
/// One chat message — layout port of the Android message components:
/// user = right aligned M3 primary-container bubble; assistant = full width card
/// with thinking expander, markdown (WebView2, original renderer), usage footer.
/// </summary>
public class MessageItem : UserControl
{
    private readonly AppCtx _ctx;
    private UIMessage? _msg;

    private readonly Grid _root = new();
    private readonly TextBlock _userText = new();
    private readonly StackPanel _userImages = new();
    private readonly Expander? _thinking;
    private readonly TextBlock _thinkingHeader = new();
    private readonly TextBlock _thinkingBody = new();
    private readonly StackPanel _assistantContent = new();
    private readonly StackPanel _toolPanel = new();
    private readonly MarkdownWebView _markdown = new();
    private readonly Grid _userBubble = new();
    private readonly StackPanel _userWrap = new();
    private readonly TextBlock _errorText = new();
    private readonly Grid _errorCard = new();
    private readonly TextBlock _footer = new();
    private readonly StackPanel _actions = new();

    public MessageItem(AppCtx ctx)
    {
        _ctx = ctx;
        MinWidth = 200;

        // user layout (right aligned)
        _userWrap.Orientation = Orientation.Vertical;
        _userWrap.HorizontalAlignment = HorizontalAlignment.Right;
        _userWrap.Spacing = 6;
        _userImages.Orientation = Orientation.Horizontal;
        _userImages.Spacing = 6;
        _userImages.HorizontalAlignment = HorizontalAlignment.Right;
        _userBubble.CornerRadius = new Microsoft.UI.Xaml.CornerRadius(20, 20, 4, 20);
        _userBubble.Padding = new Thickness(14, 10, 14, 10);
        _userBubble.MaxWidth = 560;
        _userBubble.Background = new SolidColorBrush(M3Theme.Current.PrimaryContainer);
        _userText.TextWrapping = TextWrapping.Wrap;
        _userText.FontSize = 14;
        _userText.Foreground = new SolidColorBrush(M3Theme.Current.OnPrimaryContainer);
        _userText.IsTextSelectionEnabled = true;
        _userBubble.Children.Add(_userText);

        // assistant layout
        var assistantRow = new StackPanel { Orientation = Orientation.Vertical, Spacing = 4 };

        _thinking = new Expander
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            CornerRadius = new Microsoft.UI.Xaml.CornerRadius(12),
        };
        _thinkingHeader.FontSize = 12;
        _thinkingHeader.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        _thinking.Header = _thinkingHeader;
        var thinkingScroll = new ScrollViewer
        {
            MaxHeight = 240,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _thinkingBody,
        };
        _thinkingBody.FontSize = 12;
        _thinkingBody.Opacity = 0.75;
        _thinkingBody.TextWrapping = TextWrapping.Wrap;
        _thinking.Content = thinkingScroll;

        _toolPanel.Spacing = 4;
        _markdown.PreferCompactPadding = true;
        _markdown.HorizontalAlignment = HorizontalAlignment.Stretch;

        // error card
        _errorCard.CornerRadius = new Microsoft.UI.Xaml.CornerRadius(12);
        _errorCard.Padding = new Thickness(12, 8, 12, 8);
        _errorCard.Background = new SolidColorBrush(M3Theme.Current.ErrorContainer);
        _errorText.FontSize = 13;
        _errorText.TextWrapping = TextWrapping.Wrap;
        _errorText.Foreground = new SolidColorBrush(M3Theme.Current.OnErrorContainer);
        _errorCard.Children.Add(_errorText);

        // footer + actions
        _footer.FontSize = 11;
        _footer.Opacity = 0.6;
        _actions.Orientation = Orientation.Horizontal;
        _actions.Spacing = 4;

        assistantRow.Children.Add(_thinking);
        assistantRow.Children.Add(_toolPanel);
        assistantRow.Children.Add(_markdown);
        assistantRow.Children.Add(_errorCard);
        assistantRow.Children.Add(_footer);
        assistantRow.Children.Add(_actions);

        _root.Children.Add(_userWrap);
        _root.Children.Add(assistantRow);
        Content = _root;

        PointerWheelChanged += (s, e) => { }; // let scroll pass
    }

    public void Bind(UIMessage msg)
    {
        _msg = msg;
        var isUser = msg.Role == MessageRole.User;
        _userWrap.Visibility = isUser ? Visibility.Visible : Visibility.Collapsed;
        var assistantRow = (StackPanel)_root.Children[1];
        assistantRow.Visibility = isUser ? Visibility.Collapsed : Visibility.Visible;

        if (isUser)
        {
            _userText.Text = msg.Text ?? "";
            _userImages.Children.Clear();
            foreach (var img in msg.Images)
            {
                _userImages.Children.Add(new Grid
                {
                    CornerRadius = new Microsoft.UI.Xaml.CornerRadius(8),
                    Children =
                    {
                        new Image
                        {
                            Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(img.ToDisplayUrl())),
                            MaxWidth = 200,
                            MaxHeight = 200,
                            Stretch = Stretch.Uniform,
                        },
                    },
                });
            }
            if (_userImages.Children.Count > 0 && !_userWrap.Children.Contains(_userImages))
                _userWrap.Children.Insert(0, _userImages);
            return;
        }

        // thinking
        var thinking = msg.Thinking;
        _thinking!.Visibility = string.IsNullOrEmpty(thinking) ? Visibility.Collapsed : Visibility.Visible;
        if (!string.IsNullOrEmpty(thinking))
        {
            var secs = Math.Max(1, (int)((msg.Metadata?.ReasoningTime ?? 1000) / 1000.0));
            _thinkingHeader.Text = msg.Pending ? $"{Loc.Tr("thinking")}…" : Loc.Tr("thinking_done", secs);
            _thinkingBody.Text = thinking;
        }

        // tool calls
        _toolPanel.Children.Clear();
        foreach (var tc in msg.ToolCalls)
        {
            var chip = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            chip.Children.Add(new FontIcon { Glyph = "\uE90F", FontSize = 14 });
            chip.Children.Add(new TextBlock
            {
                Text = $"{tc.Name}({tc.ArgsJson})",
                FontSize = 12,
                Opacity = 0.8,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 400,
            });
            var card = new Grid
            {
                Padding = new Thickness(10, 6, 10, 6),
                CornerRadius = new Microsoft.UI.Xaml.CornerRadius(10),
                Background = new SolidColorBrush(M3Theme.Current.SurfaceContainerHigh),
            };
            card.Children.Add(chip);
            _toolPanel.Children.Add(card);
        }
        _toolPanel.Visibility = msg.ToolCalls.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        // markdown
        _markdown.Visibility = string.IsNullOrEmpty(msg.Text) ? Visibility.Collapsed : Visibility.Visible;
        _markdown.Markdown = msg.Text ?? "";

        // error
        var hasError = msg.Error != null && !string.IsNullOrEmpty(msg.Error.Message);
        _errorCard.Visibility = hasError ? Visibility.Visible : Visibility.Collapsed;
        if (hasError) _errorText.Text = $"⚠ {msg.Error!.Message}";

        // footer (model + usage + time)
        var md = msg.Metadata;
        if (md?.Usage != null || md?.Time != null || md?.ModelId != null)
        {
            var bits = new List<string>();
            if (md!.ModelId is { } mid)
            {
                var name = _ctx.FindModel(mid)?.Model.DisplayName ?? mid.ToString().Substring(0, 8);
                bits.Add(name);
            }
            if (md.Usage != null && _ctx.Settings.ShowTokenUsage)
                bits.Add($"{Loc.Tr("tokens")}: {md.Usage.InputTokens}↑ {md.Usage.OutputTokens}↓");
            if (md.Time is { } t)
                bits.Add($"{t / 1000.0:F1}s");
            _footer.Text = string.Join(" · ", bits);
            _footer.Visibility = bits.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        else
        {
            _footer.Visibility = Visibility.Collapsed;
        }

        // actions
        RebuildActions(msg);
    }

    private void RebuildActions(UIMessage msg)
    {
        _actions.Children.Clear();
        if (msg.Pending) return;

        AddAction("\uE8C8", Loc.Tr("copy"), () =>
        {
            var text = msg.Text;
            if (!string.IsNullOrEmpty(text))
            {
                var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
                dp.SetText(text);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
            }
        });

        var isLast = _ctx.Conversation?.Messages.LastOrDefault() == msg;
        if (isLast && !_ctx.Chat.IsGenerating)
        {
            AddAction("\uE72C", Loc.Tr("regenerate"), () => _ = _ctx.Chat.Regenerate());
        }
    }

    private void AddAction(string glyph, string tooltip, Action action)
    {
        var btn = new Button
        {
            Content = new FontIcon { Glyph = glyph, FontSize = 14 },
            Background = null,
            Padding = new Thickness(6),
            MinWidth = 32,
            MinHeight = 32,
        };
        ToolTipService.SetToolTip(btn, tooltip);
        btn.Click += (s, e) => action();
        _actions.Children.Add(btn);
    }
}
