using System.Text.Json;

namespace RikkaHub.Core.Provider;

/// <summary>Port of me.rerere.ai.provider.ProviderManager.</summary>
public class ProviderManager
{
    private readonly Dictionary<string, IProvider> _providers = new()
    {
        ["openai"] = new OpenAIProvider(),
        ["gemini"] = new GeminiProvider(),
        ["claude"] = new ClaudeProvider(),
        ["cerebras"] = new OpenAIProvider(),
        ["openrouter"] = new OpenAIProvider(),
        ["gitee_ai"] = new OpenAIProvider(),
        ["siliconflow"] = new OpenAIProvider(),
    };

    public IProvider Get(ProviderSetting setting) => Get(setting.ProviderType);

    public IProvider Get(string type)
    {
        if (_providers.TryGetValue(type, out var p)) return p;
        return _providers["openai"];
    }

    /// <summary>Find a model + its owning provider across all providers.</summary>
    public (ProviderSetting Provider, Model Model)? FindModel(IEnumerable<ProviderSetting> providers, Guid modelId)
    {
        foreach (var provider in providers)
        {
            if (!provider.Enabled) continue;
            var model = provider.Models.FirstOrDefault(m => m.Id == modelId);
            if (model != null) return (provider, model);
        }
        return null;
    }

    /// <summary>Find by model string id (first hit).</summary>
    public (ProviderSetting Provider, Model Model)? FindModelByModelId(IEnumerable<ProviderSetting> providers, string modelId)
    {
        foreach (var provider in providers)
        {
            var model = provider.Models.FirstOrDefault(m => m.ModelId == modelId);
            if (model != null) return (provider, model);
        }
        return null;
    }

    public List<Model> AllChatModels(IEnumerable<ProviderSetting> providers)
        => providers.Where(p => p.Enabled).SelectMany(p => p.Models).Where(m => m.Type == ModelType.Chat).ToList();

    public List<Model> AllImageModels(IEnumerable<ProviderSetting> providers)
        => providers.Where(p => p.Enabled).SelectMany(p => p.Models).Where(m => m.Type == ModelType.Image).ToList();
}

public static class JsonDefaults
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
}
