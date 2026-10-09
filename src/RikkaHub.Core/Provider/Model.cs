using System.Text.Json.Serialization;

namespace RikkaHub.Core.Provider;

/// <summary>Port of me.rerere.ai.provider.Model (sealed data class in Kotlin).</summary>
public class Model
{
    public string ModelId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public Guid Id { get; set; } = Guid.NewGuid();
    public ModelType Type { get; set; } = ModelType.Chat;
    public List<CustomHeader> CustomHeaders { get; set; } = new();
    public List<CustomBody> CustomBodies { get; set; } = new();
    public List<Modality> InputModalities { get; set; } = new() { Modality.Text };
    public List<Modality> OutputModalities { get; set; } = new() { Modality.Text };
    public List<ModelAbility> Abilities { get; set; } = new();
    public List<BuiltInToolType> Tools { get; set; } = new();
    /// <summary>Provider overwrite: per-model routing override (json type = "openai"/"gemini"/...).</summary>
    public ProviderSetting? ProviderOverwrite { get; set; }

    public Model Copy() => new()
    {
        ModelId = ModelId,
        DisplayName = DisplayName,
        Id = Id,
        Type = Type,
        CustomHeaders = new List<CustomHeader>(CustomHeaders),
        CustomBodies = new List<CustomBody>(CustomBodies),
        InputModalities = new List<Modality>(InputModalities),
        OutputModalities = new List<Modality>(OutputModalities),
        Abilities = new List<ModelAbility>(Abilities),
        Tools = new List<BuiltInToolType>(Tools),
        ProviderOverwrite = ProviderOverwrite?.CopyProvider(),
    };

    public bool SupportTool => Abilities.Contains(ModelAbility.Tool) || ModelId.Contains("gpt") || ModelId.Contains("claude");
    public bool SupportReasoning => Abilities.Contains(ModelAbility.Reasoning);
    public bool SupportVision => InputModalities.Contains(Modality.Image);
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ModelType
{
    [JsonPropertyName("CHAT")] Chat,
    [JsonPropertyName("IMAGE")] Image,
    [JsonPropertyName("EMBEDDING")] Embedding,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Modality
{
    [JsonPropertyName("TEXT")] Text,
    [JsonPropertyName("IMAGE")] Image,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ModelAbility
{
    [JsonPropertyName("TOOL")] Tool,
    [JsonPropertyName("REASONING")] Reasoning,
}

/// <summary>Built-in provider tools (gemini search / url_context / image_generation).</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BuiltInToolType
{
    [JsonPropertyName("search")] Search,
    [JsonPropertyName("url_context")] UrlContext,
    [JsonPropertyName("image_generation")] ImageGeneration,
}

public class CustomHeader
{
    public string Name { get; set; } = "";
    public string Value { get; set; } = "";
}

/// <summary>Custom body injection: key + arbitrary JSON value.</summary>
public class CustomBody
{
    public string Key { get; set; } = "";
    public System.Text.Json.JsonElement? Value { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReasoningLevel
{
    [JsonPropertyName("off")] Off,
    [JsonPropertyName("auto")] Auto,
    [JsonPropertyName("on")] On,
    [JsonPropertyName("high")] High,
    [JsonPropertyName("medium")] Medium,
    [JsonPropertyName("low")] Low,
    [JsonPropertyName("minimal")] Minimal,
    [JsonPropertyName("none")] None,
}
