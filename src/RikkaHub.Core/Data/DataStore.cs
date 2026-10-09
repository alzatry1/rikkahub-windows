using System.Text.Json;

namespace RikkaHub.Core.Data;

/// <summary>
/// DataStore — port of me.rerere.rikkahub.data.datastore.PreferencesStore (DataStore) and
/// conversation persistence. On Windows, settings live in %LOCALAPPDATA%\RikkaHub\settings.json
/// and conversations in %LOCALAPPDATA%\RikkaHub\conversations\*.json (mirroring DataStore + Room).
/// </summary>
public class DataStore
{
    public static string BaseDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RikkaHub");

    public static string SettingsPath { get; } = Path.Combine(BaseDir, "settings.json");
    public static string ConversationsDir { get; } = Path.Combine(BaseDir, "conversations");
    public static string FilesDir { get; } = Path.Combine(BaseDir, "files");

    private readonly object _saveLock = new();

    public DataStore()
    {
        Directory.CreateDirectory(BaseDir);
        Directory.CreateDirectory(ConversationsDir);
        Directory.CreateDirectory(FilesDir);
    }

    // ===== Settings =====

    public Model.Settings LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<Model.Settings>(json, JsonDefaults.Options);
                if (settings != null) return settings;
            }
        }
        catch
        {
            // corrupted settings file → backup and start fresh
            try { if (File.Exists(SettingsPath)) File.Copy(SettingsPath, SettingsPath + ".bak", true); } catch { }
        }
        return DefaultSettings.Create();
    }

    public void SaveSettings(Model.Settings settings)
    {
        lock (_saveLock)
        {
            var json = JsonSerializer.Serialize(settings, JsonDefaults.Options);
            File.WriteAllText(SettingsPath, json);
        }
    }

    // ===== Conversations =====

    public List<Meta> ListConversations()
    {
        var result = new List<Meta>();
        foreach (var file in Directory.EnumerateFiles(ConversationsDir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var conv = JsonSerializer.Deserialize<Model.Conversation>(json, JsonDefaults.Options);
                if (conv != null) result.Add(new Meta(conv.Id, conv.Title, conv.UpdateAt, file));
            }
            catch { }
        }
        return result.OrderByDescending(m => m.UpdateAt).ToList();
    }

    public record Meta(Guid Id, string Title, long UpdateAt, string Path);

    public Model.Conversation? LoadConversation(Guid id)
    {
        var file = Path.Combine(ConversationsDir, id + ".json");
        if (!File.Exists(file)) return null;
        try
        {
            var conv = JsonSerializer.Deserialize<Model.Conversation>(File.ReadAllText(file), JsonDefaults.Options);
            return conv;
        }
        catch { return null; }
    }

    public void SaveConversation(Model.Conversation conv)
    {
        conv.UpdateAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var file = Path.Combine(ConversationsDir, conv.Id + ".json");
        var json = JsonSerializer.Serialize(conv, JsonDefaults.Options);
        File.WriteAllText(file, json);
    }

    public void DeleteConversation(Guid id)
    {
        var file = Path.Combine(ConversationsDir, id + ".json");
        if (File.Exists(file))
        {
            var trash = Path.Combine(BaseDir, "trash");
            Directory.CreateDirectory(trash);
            File.Move(file, Path.Combine(trash, id + ".json"));
        }
    }

    public Model.Conversation CreateConversation()
    {
        var conv = new Model.Conversation();
        SaveConversation(conv);
        return conv;
    }
}

/// <summary>Port of DefaultSettings.kt — factory defaults for a fresh install.</summary>
public static class DefaultSettings
{
    public static Model.Settings Create()
    {
        var settings = new Model.Settings
        {
            Init = true,
            ChatModelId = Guid.NewGuid(),
            FastModelId = Guid.NewGuid(),
            TranslateModelId = Guid.NewGuid(),
            ImageGenerationModelId = Guid.NewGuid(),
            OcrModelId = Guid.NewGuid(),
            CompressModelId = Guid.NewGuid(),
        };

        // Recommended providers (port of RecommendedProviders.kt) — preset, disabled until user adds key
        var openai = new Provider.OpenAISetting
        {
            Id = Guid.NewGuid(),
            Name = "OpenAI",
            Enabled = false,
        };
        var deepseek = new Provider.OpenAISetting
        {
            Id = Guid.NewGuid(),
            Name = "DeepSeek",
            Enabled = false,
            BaseUrl = "https://api.deepseek.com/v1",
        };
        var gemini = new Provider.GeminiSetting
        {
            Id = Guid.NewGuid(),
            Name = "Gemini",
            Enabled = false,
        };
        var claude = new Provider.ClaudeSetting
        {
            Id = Guid.NewGuid(),
            Name = "Claude",
            Enabled = false,
        };
        settings.Providers = new List<Provider.ProviderSetting> { openai, deepseek, gemini, claude };

        // Default assistant (port of DEFAULT_ASSISTANTS)
        settings.Assistants = new List<Model.Assistant>
        {
            new Model.Assistant
            {
                Id = Guid.Parse(Model.Assistant.DefaultAssistantId),
                Name = "Rikka",
                Icon = "🌸",
                Description = "Default assistant",
                SystemPrompt = "",
            },
        };
        settings.AssistantId = Guid.Parse(Model.Assistant.DefaultAssistantId);

        settings.FavoriteModels = new List<Guid>();
        return settings;
    }
}
