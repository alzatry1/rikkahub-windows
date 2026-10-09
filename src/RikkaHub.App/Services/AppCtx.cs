using RikkaHub.Core.Data;
using RikkaHub.Core.Model;
using RikkaHub.Core.Provider;

namespace RikkaHub.App.Services;

/// <summary>
/// AppContext — the app-wide state hub (DI-lite).
/// Owns the DataStore, Settings, ProviderManager, the currently open conversation
/// and the chat generation loop. UI pages subscribe to Changed / ConversationChanged.
/// </summary>
public class AppCtx
{
    public DataStore Store { get; } = new();
    public Settings Settings { get; private set; } = null!;
    public ProviderManager Providers { get; } = new();
    public ChatVM Chat { get; private set; } = null!;

    public Conversation? Conversation { get; private set; }

    /// <summary>All conversations metadata (newest first, pinned first).</summary>
    public List<ConversationMetaItem> Conversations { get; private set; } = new();

    public record ConversationMetaItem(Guid Id, string Title, long UpdateAt, bool Pinned);

    public event Action? SettingsChanged;
    public event Action? ConversationListChanged;
    public event Action? ConversationChanged;

    public void Init()
    {
        Settings = Store.LoadSettings();
        Loc.Init(Settings.Language);
        Chat = new ChatVM(this);
        ReloadConversations();
        // open most recent conversation, or create a fresh one
        var last = Conversations.FirstOrDefault();
        Conversation = last != null ? Store.LoadConversation(last.Id) : null;
        if (Conversation == null)
        {
            Conversation = Store.CreateConversation();
            ReloadConversations();
        }
    }

    public void ReloadConversations()
    {
        var metas = Store.ListConversations();
        Conversations = metas.Select(m =>
        {
            var c = m;
            // cheap metadata read: pinned flag lives in the file; read lazily via header scan
            bool pinned = false;
            try
            {
                var json = File.ReadAllText(m.Path);
                pinned = json.Contains("\"pinned\": true", StringComparison.OrdinalIgnoreCase)
                      || json.Contains("\"pinned\":true", StringComparison.OrdinalIgnoreCase);
            }
            catch { }
            return new ConversationMetaItem(c.Id, c.Title, c.UpdateAt, pinned);
        })
        .OrderByDescending(x => x.Pinned)
        .ThenByDescending(x => x.UpdateAt)
        .ToList();
        ConversationListChanged?.Invoke();
    }

    public void SaveSettings()
    {
        Store.SaveSettings(Settings);
        SettingsChanged?.Invoke();
    }

    public void NewConversation()
    {
        Conversation = Store.CreateConversation();
        ReloadConversations();
        ConversationChanged?.Invoke();
    }

    public void OpenConversation(Guid id)
    {
        var conv = Store.LoadConversation(id);
        if (conv != null)
        {
            Conversation = conv;
            ConversationChanged?.Invoke();
        }
    }

    public void DeleteConversation(Guid id)
    {
        Store.DeleteConversation(id);
        if (Conversation?.Id == id)
        {
            var last = Store.ListConversations().OrderByDescending(m => m.UpdateAt).FirstOrDefault();
            Conversation = last != null ? Store.LoadConversation(last.Id) : Store.CreateConversation();
            ConversationChanged?.Invoke();
        }
        ReloadConversations();
    }

    public void SaveCurrentConversation()
    {
        if (Conversation != null)
        {
            Store.SaveConversation(Conversation);
            ReloadConversations();
        }
    }

    public void RaiseConversationChanged() => ConversationChanged?.Invoke();

    // ===== Model helpers =====

    public (ProviderSetting Provider, Model Model)? FindModel(Guid modelId) => Providers.FindModel(Settings.Providers, modelId);

    public List<Model> ChatModels => Providers.AllChatModels(Settings.Providers);

    public Model? ChatModel
    {
        get
        {
            var configModel = Conversation?.Config?.ModelId;
            if (configModel is { } cm)
            {
                var hit = Providers.FindModel(Settings.Providers, cm);
                if (hit != null) return hit.Value.Model;
            }
            var hit2 = Providers.FindModel(Settings.Providers, Settings.ChatModelId);
            return hit2?.Model;
        }
    }

    public Assistant? CurrentAssistant
    {
        get
        {
            var id = Conversation?.Config?.AssistantId ?? Settings.AssistantId;
            return Settings.Assistants.FirstOrDefault(a => a.Id == id) ?? Settings.Assistants.FirstOrDefault();
        }
    }
}

/// <summary>
/// Chat generation loop — port of me.rerere.rikkahub.data.ai.GenerationLoop.
/// Streams provider responses into the current conversation's assistant message.
/// </summary>
public class ChatVM
{
    private readonly AppCtx _ctx;
    private CancellationTokenSource? _cts;

    public bool IsGenerating { get; private set; }
    public event Action? StateChanged;

    public ChatVM(AppCtx ctx)
    {
        _ctx = ctx;
    }

    public void Stop() => _cts?.Cancel();

    public async Task SendMessage(string text, List<ImagePart>? images = null)
    {
        if (IsGenerating || _ctx.Conversation == null) return;
        var conv = _ctx.Conversation;

        // resolve model + provider
        var chatModel = _ctx.ChatModel;
        if (chatModel == null) return;
        var providerHit = _ctx.Providers.FindModel(_ctx.Settings.Providers, chatModel.Id)
            ?? throw new InvalidOperationException("Provider not found for model");
        var (providerSetting, model) = providerHit;

        // build user message
        var userMsg = new UIMessage { Role = MessageRole.User, Pending = false };
        if (images != null) foreach (var img in images) userMsg.Parts.Add(img);
        if (!string.IsNullOrWhiteSpace(text)) userMsg.Parts.Add(new TextPart { Text = text });
        if (userMsg.Parts.Count == 0) return;
        conv.Messages.Add(userMsg);

        // build assistant message
        var assistant = new UIMessage
        {
            Role = MessageRole.Assistant,
            Pending = true,
            Metadata = new MessageMetadata
            {
                ProviderId = providerSetting.Id,
                ModelId = model.Id,
            },
        };
        conv.Messages.Add(assistant);

        // auto title: first 20 chars of first user message
        if (string.IsNullOrEmpty(conv.Title))
        {
            conv.Title = Truncate(text, 20);
        }

        await GenerateInto(conv, assistant, providerSetting, model);
    }

    public async Task Regenerate()
    {
        if (IsGenerating || _ctx.Conversation == null) return;
        var conv = _ctx.Conversation;
        // find last assistant message; remove it (and any following tool messages)
        var idx = FindLastAssistantIndex(conv);
        if (idx < 0) return;
        // remove assistant + trailing tool messages
        while (conv.Messages.Count > idx) conv.Messages.RemoveAt(conv.Messages.Count - 1);

        var chatModel = _ctx.ChatModel;
        if (chatModel == null) return;
        var hitNullable = _ctx.Providers.FindModel(_ctx.Settings.Providers, chatModel.Id);
        if (hitNullable == null) return;
        var hit = hitNullable.Value;
        var assistant = new UIMessage
        {
            Role = MessageRole.Assistant,
            Pending = true,
            Metadata = new MessageMetadata
            {
                ProviderId = hit.Provider.Id,
                ModelId = hit.Model.Id,
                Regenerate = true,
            },
        };
        conv.Messages.Add(assistant);
        await GenerateInto(conv, assistant, hit.Provider, hit.Model);
    }

    private static int FindLastAssistantIndex(Conversation conv)
    {
        for (int i = conv.Messages.Count - 1; i >= 0; i--)
            if (conv.Messages[i].Role == MessageRole.Assistant) return i;
        return -1;
    }

    private static string Truncate(string s, int n) => string.IsNullOrEmpty(s) ? "" : (s.Length <= n ? s : s[..n] + "…");

    private async Task GenerateInto(Conversation conv, UIMessage assistant, ProviderSetting providerSetting, Model model)
    {
        _cts = new CancellationTokenSource();
        IsGenerating = true;
        StateChanged?.Invoke();

        var provider = _ctx.Providers.Get(providerSetting);
        var assistant_ = assistant;
        var contextMessages = BuildContextMessages(conv, assistant_);
        var parameters = new TextGenerationParams
        {
            Model = model,
            Temperature = conv.Config?.Temperature,
            TopP = conv.Config?.TopP,
            MaxTokens = conv.Config?.MaxTokens,
            ReasoningLevel = conv.Config?.ReasoningLevel ?? ReasoningLevel.Auto,
        };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        long firstTokenMs = -1;
        long reasoningMs = 0;
        try
        {
            await foreach (var chunk in provider.StreamTextAsync(providerSetting, contextMessages, parameters, _cts.Token))
            {
                if (firstTokenMs < 0) firstTokenMs = sw.ElapsedMilliseconds;
                switch (chunk)
                {
                    case TextDeltaChunk td:
                        var lastText = assistant_.Parts.LastOrDefault(p => p is TextPart) as TextPart;
                        if (lastText != null) lastText.Text += td.Text;
                        else assistant_.Parts.Add(new TextPart { Text = td.Text });
                        break;
                    case ThinkingDeltaChunk th:
                        var lastTh = assistant_.Parts.LastOrDefault(p => p is ThinkingPart) as ThinkingPart;
                        if (lastTh != null) lastTh.Thinking += th.Thinking;
                        else assistant_.Parts.Add(new ThinkingPart { Thinking = th.Thinking });
                        break;
                    case ToolCallDeltaChunk tc:
                        var existing = assistant_.Parts.OfType<ToolCallPart>().FirstOrDefault(x => x.Index == tc.Index) as ToolCallPart;
                        if (existing == null) { existing = new ToolCallPart { Id = tc.Id, Name = tc.Name, Index = tc.Index }; assistant_.Parts.Add(existing); }
                        if (!string.IsNullOrEmpty(tc.Id)) existing.Id = tc.Id;
                        if (!string.IsNullOrEmpty(tc.Name)) existing.Name = tc.Name;
                        existing.ArgsJson += tc.ArgsDelta;
                        break;
                    case UsageChunk u:
                        assistant_.Metadata ??= new MessageMetadata();
                        assistant_.Metadata.Usage = u.Usage;
                        break;
                    case ErrorChunk e:
                        assistant_.Error = new MessageError { Type = "provider", Message = e.Error };
                        break;
                }
                StateChanged?.Invoke();
            }
        }
        catch (OperationCanceledException)
        {
            // stopped by user — keep partial content
        }
        catch (Exception e)
        {
            assistant_.Error = new MessageError { Type = "client", Message = e.Message };
        }
        finally
        {
            sw.Stop();
            assistant_.Pending = false;
            assistant_.Metadata ??= new MessageMetadata();
            assistant_.Metadata.Time = sw.ElapsedMilliseconds;
            assistant_.Metadata.FirstTokenLatency = firstTokenMs < 0 ? null : firstTokenMs;
            assistant_.Metadata.ReasoningTime = reasoningMs > 0 ? reasoningMs : null;
            conv.Latency = sw.Elapsed.TotalMilliseconds;
            IsGenerating = false;
            StateChanged?.Invoke();
            _ctx.SaveCurrentConversation();
        }
    }

    /// <summary>Build the message list sent to the provider (context window = all messages before the pending assistant message, with system prompt).</summary>
    private List<UIMessage> BuildContextMessages(Conversation conv, UIMessage pending)
    {
        var result = new List<UIMessage>();

        // system prompt: assistant system prompt overrides conversation one
        var assistant = _ctx.CurrentAssistant;
        var sysPrompt = conv.Config?.SystemPrompt;
        if (assistant != null && assistant.OverrideSystemPrompt && !string.IsNullOrEmpty(assistant.SystemPrompt))
            sysPrompt = assistant.SystemPrompt;
        else if (assistant != null && !string.IsNullOrEmpty(assistant.SystemPrompt) && string.IsNullOrEmpty(sysPrompt))
            sysPrompt = assistant.SystemPrompt;
        if (!string.IsNullOrEmpty(sysPrompt))
            result.Add(new UIMessage { Role = MessageRole.System, Parts = { new TextPart { Text = sysPrompt! } } });

        foreach (var m in conv.Messages)
        {
            if (m == pending) break;
            if (m.Pending) continue;
            if (m.Role == MessageRole.Assistant && m.Error != null && string.IsNullOrEmpty(m.Text)) continue;
            result.Add(m);
        }
        return result;
    }
}
