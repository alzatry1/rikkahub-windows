using System.Text.Json.Serialization;
using RikkaHub.Core.Provider;

namespace RikkaHub.Core.Model;

/// <summary>Port of me.rerere.rikkahub.data.datastore.Settings (main settings tree).</summary>
public class Settings
{
    public bool Init { get; set; } = false;
    public bool DynamicColor { get; set; } = true;
    /// <summary>Theme seed color (argb int) for dynamic color generation.</summary>
    public int SeedColor { get; set; } = 0xFF6750A4;
    /// <summary>dark / light / auto.</summary>
    public string DarkMode { get; set; } = "auto";
    public string ThemeId { get; set; } = "green_tangerine";
    public List<CustomTheme> CustomThemes { get; set; } = new();
    public bool DeveloperMode { get; set; } = false;
    /// <summary>App language: "system" / "zh" / "en" ...</summary>
    public string Language { get; set; } = "system";
    public DisplaySetting DisplaySetting { get; set; } = new();
    public NetworkSetting NetworkSetting { get; set; } = new();
    public List<Guid> FavoriteModels { get; set; } = new();
    public Guid ChatModelId { get; set; }
    public Guid FastModelId { get; set; }
    public Guid ImageGenerationModelId { get; set; }
    public string TitlePrompt { get; set; } = "You are a title generator. Summarize the conversation into a short title (less than 12 words). Reply with the title only.";
    public Guid TranslateModelId { get; set; }
    public string TranslatePrompt { get; set; } = "Translate the following text to {target_language}. Only output the translation.";
    public bool EnableSuggestion { get; set; } = false;
    public string SuggestionPrompt { get; set; } = "";
    public Guid OcrModelId { get; set; }
    public Guid CompressModelId { get; set; }
    public Guid? AssistantId { get; set; }
    public List<ProviderSetting> Providers { get; set; } = new();
    public List<Assistant> Assistants { get; set; } = new();
    public List<string> AssistantTags { get; set; } = new();
    /// <summary>Chat input send on Enter: true=Enter, false=Ctrl+Enter.</summary>
    public bool SendOnEnter { get; set; } = true;
    /// <summary>Message bubble style: "bubble" (iOS/WeChat style) / "plain".</summary>
    public string BubbleStyle { get; set; } = "bubble";
    public bool ShowTokenUsage { get; set; } = true;
    public bool ShowAssistantIcon { get; set; } = true;
    public float ChatFontScale { get; set; } = 1.0f;
    public List<QuickMessage> QuickMessages { get; set; } = new();
    /// <summary>Custom display names for models (model id guid -> name).</summary>
    public Dictionary<string, string> ModelNames { get; set; } = new();
    public bool WebServerEnabled { get; set; } = false;
    public int WebServerPort { get; set; } = 8080;

    public Model? FindChatModel(ProviderManager pm)
    {
        var hit = pm.FindModel(Providers, ChatModelId);
        return hit?.Model;
    }
}

public class DisplaySetting
{
    public string ChatBubbleStyle { get; set; } = "bubble";
    public float FontScale { get; set; } = 1.0f;
    public bool ShowTokenUsage { get; set; } = true;
    public bool ShowModelName { get; set; } = true;
    public bool ShowTime { get; set; } = false;
    public bool ShowAssistantIcon { get; set; } = true;
    public bool DoubleClickToEdit { get; set; } = true;
}

public class NetworkSetting
{
    public bool BalanceCheckEnabled { get; set; } = false;
    public int TimeoutSeconds { get; set; } = 60;
}

public class CustomTheme
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "Custom";
    public int SeedColor { get; set; } = 0xFF6750A4;
}

public class QuickMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Content { get; set; } = "";
}

/// <summary>Port of me.rerere.rikkahub.data.model.Assistant (agent/character).</summary>
public class Assistant
{
    public const string DefaultAssistantId = "0195b1d0-9d02-7333-a5f9-86b5e5b9dc7d";

    public Guid Id { get; set; } = Guid.Parse(DefaultAssistantId);
    public string Name { get; set; } = "Rikka";
    public string Icon { get; set; } = "🤖";
    public string Description { get; set; } = "";
    /// <summary>System prompt (main instructions).</summary>
    public string SystemPrompt { get; set; } = "";
    /// <summary>Tags shown in assistant list.</summary>
    public List<string> Tags { get; set; } = new();
    public long CreateAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public bool Pinned { get; set; } = false;
    /// <summary>Default temperature for this assistant.</summary>
    public float? Temperature { get; set; }
    /// <summary>Whether assistant's system prompt replaces the conversation one.</summary>
    public bool OverrideSystemPrompt { get; set; } = false;

    public Assistant Copy() => new()
    {
        Id = Id,
        Name = Name,
        Icon = Icon,
        Description = Description,
        SystemPrompt = SystemPrompt,
        Tags = new List<string>(Tags),
        CreateAt = CreateAt,
        Pinned = Pinned,
        Temperature = Temperature,
        OverrideSystemPrompt = OverrideSystemPrompt,
    };
}
