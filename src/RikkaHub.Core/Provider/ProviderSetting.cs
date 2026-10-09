using System.Text.Json.Serialization;
using RikkaHub.Core.Provider;

namespace RikkaHub.Core.Provider;

/// <summary>
/// Port of me.rerere.ai.provider.ProviderSetting (sealed class hierarchy).
/// JSON shape mirrors kotlinx.serialization (discriminator = "type").
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(OpenAISetting), "openai")]
[JsonDerivedType(typeof(GeminiSetting), "gemini")]
[JsonDerivedType(typeof(ClaudeSetting), "claude")]
[JsonDerivedType(typeof(CerebrasSetting), "cerebras")]
[JsonDerivedType(typeof(OpenRouterSetting), "openrouter")]
[JsonDerivedType(typeof(GiteeAISetting), "gitee_ai")]
[JsonDerivedType(typeof(SiliconFlowSetting), "siliconflow")]
public abstract class ProviderSetting
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public bool Enabled { get; set; } = true;
    public string Name { get; set; } = "";
    public List<Model> Models { get; set; } = new();
    public BalanceOption BalanceOption { get; set; } = new();
    public List<CustomHeader> CustomHeaders { get; set; } = new();
    public bool BuiltIn { get; set; } = false;

    public abstract string ProviderType { get; }

    public ProviderSetting AddModel(Model model) { var c = CopyProvider(); c.Models.Add(model); return c; }
    public ProviderSetting EditModel(Model model)
    {
        var c = CopyProvider();
        var i = c.Models.FindIndex(m => m.Id == model.Id);
        if (i >= 0) c.Models[i] = model.Copy();
        return c;
    }
    public ProviderSetting DelModel(Model model) { var c = CopyProvider(); c.Models.RemoveAll(m => m.Id == model.Id); return c; }
    public ProviderSetting MoveModel(int from, int to)
    {
        var c = CopyProvider();
        if (from < 0 || from >= c.Models.Count) return c;
        var m = c.Models[from];
        c.Models.RemoveAt(from);
        var idx = Math.Clamp(to, 0, c.Models.Count);
        c.Models.Insert(idx, m);
        return c;
    }

    public abstract ProviderSetting CopyProvider();

    /// <summary>Resolve effective routing for a model (model-level provider overwrite wins).</summary>
    public (ProviderSetting Provider, Model Model) Route(Model model)
    {
        if (model.ProviderOverwrite != null)
        {
            var m = model.Copy();
            m.ProviderOverwrite = null;
            return (model.ProviderOverwrite, m);
        }
        return (this, model);
    }
}

public class BalanceOption
{
    public bool Enabled { get; set; } = false;
    public string ApiPath { get; set; } = "/credits";
    public string ResultPath { get; set; } = "data.total_usage";
}

/// <summary>Credential access for provider settings (API key / base URL).</summary>
public interface ICredentialSetting
{
    string ApiKey { get; set; }
    string BaseUrl { get; set; }
}

public abstract class ProviderSettingBase<T> : ProviderSetting, ICredentialSetting where T : ProviderSettingBase<T>
{
    public string ApiKey { get; set; } = "";
    public abstract string DefaultBaseUrl { get; }
    public string BaseUrl { get; set; } = "";

    public string GetBaseUrl() => string.IsNullOrWhiteSpace(BaseUrl) ? DefaultBaseUrl : BaseUrl.TrimEnd('/');

    public override ProviderSetting CopyProvider()
    {
        var copy = (T)MemberwiseClone();
        copy.Models = Models.Select(m => m.Copy()).ToList();
        copy.CustomHeaders = CustomHeaders.Select(h => new CustomHeader { Name = h.Name, Value = h.Value }).ToList();
        copy.BalanceOption = new BalanceOption { Enabled = BalanceOption.Enabled, ApiPath = BalanceOption.ApiPath, ResultPath = BalanceOption.ResultPath };
        return copy;
    }
}

/// <summary>OpenAI-compatible provider (OpenAI / DeepSeek / aihubmix / any custom base url).</summary>
public class OpenAISetting : ProviderSettingBase<OpenAISetting>
{
    public override string ProviderType => "openai";
    public override string DefaultBaseUrl => "https://api.openai.com/v1";
    public string ChatCompletionsPath { get; set; } = "/chat/completions";
    public string ResponsesPath { get; set; } = "/responses";
    public bool UseResponseApi { get; set; } = false;
    public bool IncludeHistoryReasoning { get; set; } = true;

    public OpenAISetting()
    {
        Name = "OpenAI";
    }
}

/// <summary>Google Gemini (generativelanguage API).</summary>
public class GeminiSetting : ProviderSettingBase<GeminiSetting>
{
    public override string ProviderType => "gemini";
    public override string DefaultBaseUrl => "https://generativelanguage.googleapis.com/v1beta";
    /// <summary>Vertex AI service account JSON (optional, advanced).</summary>
    public string? ServiceAccountJson { get; set; }

    public GeminiSetting()
    {
        Name = "Gemini";
    }
}

/// <summary>Anthropic Claude messages API.</summary>
public class ClaudeSetting : ProviderSettingBase<ClaudeSetting>
{
    public override string ProviderType => "claude";
    public override string DefaultBaseUrl => "https://api.anthropic.com";
    public string Version { get; set; } = "2023-06-01";
    public bool EnablePromptCache { get; set; } = false;
    public string PromptCacheTtl { get; set; } = "5m";
    public bool IncludeHistoryReasoning { get; set; } = false;

    public ClaudeSetting()
    {
        Name = "Claude";
    }
}

// ===== OpenAI-compatible 预设供应商 =====

public class CerebrasSetting : OpenAISetting
{
    public override string ProviderType => "cerebras";
    public override string DefaultBaseUrl => "https://api.cerebras.ai/v1";
    public CerebrasSetting() { Name = "Cerebras"; ApiKey = "csk-"; }
}

public class OpenRouterSetting : OpenAISetting
{
    public override string ProviderType => "openrouter";
    public override string DefaultBaseUrl => "https://openrouter.ai/api";
    public OpenRouterSetting() { Name = "OpenRouter"; }
}

public class GiteeAISetting : OpenAISetting
{
    public override string ProviderType => "gitee_ai";
    public override string DefaultBaseUrl => "https://ai.gitee.com/v1";
    public GiteeAISetting() { Name = "Gitee AI"; }
}

public class SiliconFlowSetting : OpenAISetting
{
    public override string ProviderType => "siliconflow";
    public override string DefaultBaseUrl => "https://api.siliconflow.cn/v1";
    public SiliconFlowSetting() { Name = "SiliconFlow"; }
}
