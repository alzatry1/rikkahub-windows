using System.Text.Json;
using System.Text.Json.Serialization;

namespace RikkaHub.Core.Provider;

/// <summary>Message role — port of me.rerere.ai.core.MessageRole.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MessageRole
{
    [JsonPropertyName("system")] System,
    [JsonPropertyName("user")] User,
    [JsonPropertyName("assistant")] Assistant,
    [JsonPropertyName("tool")] Tool,
}

/// <summary>
/// UI message — port of me.rerere.ai.ui.UIMessage.
/// Parts are polymorphic with "type" discriminators mirroring kotlinx serialization.
/// </summary>
public class UIMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public MessageRole Role { get; set; } = MessageRole.Assistant;
    public List<MessagePart> Parts { get; set; } = new();
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public bool Pending { get; set; }
    public MessageMetadata? Metadata { get; set; }
    public MessageError? Error { get; set; }

    public string? Text
    {
        get
        {
            var sb = new System.Text.StringBuilder();
            foreach (var p in Parts)
                if (p is TextPart tp) sb.Append(tp.Text);
            return sb.Length == 0 ? null : sb.ToString();
        }
    }

    public string? Thinking
    {
        get
        {
            var sb = new System.Text.StringBuilder();
            foreach (var p in Parts)
                if (p is ThinkingPart thp) sb.Append(thp.Thinking);
            return sb.Length == 0 ? null : sb.ToString();
        }
    }

    public List<ImagePart> Images => Parts.OfType<ImagePart>().ToList();
    public List<ToolCallPart> ToolCalls => Parts.OfType<ToolCallPart>().ToList();

    public UIMessage Copy() => new()
    {
        Id = Id,
        Role = Role,
        CreatedAt = CreatedAt,
        Pending = Pending,
        Metadata = Metadata?.Copy(),
        Error = Error,
        Parts = Parts.Select(p => p.Copy()).ToList(),
    };
}

// ===== Parts =====

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextPart), "text")]
[JsonDerivedType(typeof(ImagePart), "image")]
[JsonDerivedType(typeof(ThinkingPart), "thinking")]
[JsonDerivedType(typeof(ToolCallPart), "toolCall")]
[JsonDerivedType(typeof(ToolResultPart), "toolResult")]
public abstract class MessagePart
{
    public abstract MessagePart Copy();
}

public class TextPart : MessagePart
{
    public string Text { get; set; } = "";
    public override MessagePart Copy() => new TextPart { Text = Text };
}

public class ImagePart : MessagePart
{
    public string Url { get; set; } = "";
    /// <summary>Base64 (no data: prefix) for multimodal upload.</summary>
    public string? Data { get; set; }
    public string MimeType { get; set; } = "image/png";
    public override MessagePart Copy() => new ImagePart { Url = Url, Data = Data, MimeType = MimeType };
    /// <summary>For display: data URI or remote URL.</summary>
    public string ToDisplayUrl() => !string.IsNullOrEmpty(Url) ? Url : $"data:{MimeType};base64,{Data}";
}

public class ThinkingPart : MessagePart
{
    public string Thinking { get; set; } = "";
    public override MessagePart Copy() => new ThinkingPart { Thinking = Thinking };
}

public class ToolCallPart : MessagePart
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Raw JSON args string (as emitted by the provider).</summary>
    public string ArgsJson { get; set; } = "{}";
    public string Status { get; set; } = "pending"; // pending | success | error
    /// <summary>Index in the provider's tool call array (for streaming assembly).</summary>
    public int Index { get; set; } = -1;
    public override MessagePart Copy() => new ToolCallPart { Id = Id, Name = Name, ArgsJson = ArgsJson, Status = Status, Index = Index };
}

public class ToolResultPart : MessagePart
{
    public string ToolCallId { get; set; } = "";
    public string Content { get; set; } = "";
    public bool IsError { get; set; }
    public override MessagePart Copy() => new ToolResultPart { ToolCallId = ToolCallId, Content = Content, IsError = IsError };
}

// ===== Metadata =====

public class MessageMetadata
{
    public Guid? ProviderId { get; set; }
    public Guid? ModelId { get; set; }
    public TokenUsage? Usage { get; set; }
    /// <summary>Total wall time in ms.</summary>
    public long? Time { get; set; }
    /// <summary>Reasoning time in ms.</summary>
    public long? ReasoningTime { get; set; }
    /// <summary>First token latency ms.</summary>
    public long? FirstTokenLatency { get; set; }
    public bool Regenerate { get; set; }
    public bool IsSearch { get; set; }

    public MessageMetadata Copy() => new()
    {
        ProviderId = ProviderId,
        ModelId = ModelId,
        Usage = Usage,
        Time = Time,
        ReasoningTime = ReasoningTime,
        FirstTokenLatency = FirstTokenLatency,
        Regenerate = Regenerate,
        IsSearch = IsSearch,
    };
}

public class TokenUsage
{
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int ReasoningTokens { get; set; }
    public int TotalTokens => InputTokens + OutputTokens + ReasoningTokens;
}

public class MessageError
{
    public string Type { get; set; } = "";
    public string Message { get; set; } = "";
}

// ===== Generation params =====

public class TextGenerationParams
{
    public Model Model { get; set; } = new();
    public float? Temperature { get; set; }
    public float? TopP { get; set; }
    public int? MaxTokens { get; set; }
    public List<Tool> Tools { get; set; } = new();
    public ReasoningLevel ReasoningLevel { get; set; } = ReasoningLevel.Auto;
    public List<CustomHeader> CustomHeaders { get; set; } = new();
    public List<CustomBody> CustomBody { get; set; } = new();
    public string? SessionId { get; set; } = Guid.NewGuid().ToString();
}

/// <summary>Tool definition (OpenAI function tool).</summary>
public class Tool
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    /// <summary>JSON schema object.</summary>
    public JsonElement? Parameters { get; set; }
}
