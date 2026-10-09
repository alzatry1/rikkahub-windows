using System.Text.Json.Serialization;

namespace RikkaHub.Core.Provider;

/// <summary>Stream chunk — port of me.rerere.ai.ui.StreamChunk (sealed class).</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ChunkId), "id")]
[JsonDerivedType(typeof(ChunkModel), "model")]
[JsonDerivedType(typeof(TextDeltaChunk), "text")]
[JsonDerivedType(typeof(ThinkingDeltaChunk), "thinking")]
[JsonDerivedType(typeof(ToolCallDeltaChunk), "toolCallDelta")]
[JsonDerivedType(typeof(UsageChunk), "usage")]
[JsonDerivedType(typeof(FinishReasonChunk), "finishReason")]
[JsonDerivedType(typeof(ErrorChunk), "error")]
public abstract class StreamChunk;

public sealed class ChunkId : StreamChunk
{
    public string Id { get; set; } = "";
}

public sealed class ChunkModel : StreamChunk
{
    public string Model { get; set; } = "";
}

public sealed class TextDeltaChunk : StreamChunk
{
    public string Text { get; set; } = "";
}

public sealed class ThinkingDeltaChunk : StreamChunk
{
    public string Thinking { get; set; } = "";
}

public sealed class ToolCallDeltaChunk : StreamChunk
{
    public int Index { get; set; }
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string ArgsDelta { get; set; } = "";
}

public sealed class UsageChunk : StreamChunk
{
    public TokenUsage Usage { get; set; } = new();
}

public sealed class FinishReasonChunk : StreamChunk
{
    public string? Reason { get; set; }
}

public sealed class ErrorChunk : StreamChunk
{
    public string Error { get; set; } = "";
    /// <summary>Whether the error occurred mid-stream (after content was received).</summary>
    public bool IsStream { get; set; }
}
