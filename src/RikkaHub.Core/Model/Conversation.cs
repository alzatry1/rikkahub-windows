using System.Text.Json.Serialization;
using RikkaHub.Core.Provider;

namespace RikkaHub.Core.Model;

/// <summary>Port of me.rerere.rikkahub.data.model.Conversation.</summary>
public class Conversation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public List<UIMessage> Messages { get; set; } = new();
    public ConversationConfig? Config { get; set; }
    public long CreateAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public long UpdateAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public bool Pinned { get; set; }
    public string? Tag { get; set; }
    /// <summary>Folder id (null = root).</summary>
    public Guid? FolderId { get; set; }
    public double Latency { get; set; }
    public double FirstTokenLatency { get; set; }

    public Conversation Copy() => new()
    {
        Id = Id,
        Title = Title,
        Messages = Messages.Select(m => m.Copy()).ToList(),
        Config = Config?.Copy(),
        CreateAt = CreateAt,
        UpdateAt = UpdateAt,
        Pinned = Pinned,
        Tag = Tag,
        FolderId = FolderId,
        Latency = Latency,
        FirstTokenLatency = FirstTokenLatency,
    };
}

/// <summary>Port of me.rerere.rikkahub.data.model.ConversationConfig.</summary>
public class ConversationConfig
{
    /// <summary>Per-conversation model id.</summary>
    public Guid? ModelId { get; set; }
    public string? SystemPrompt { get; set; }
    public float? Temperature { get; set; }
    public float? TopP { get; set; }
    public int? MaxTokens { get; set; }
    public Guid? AssistantId { get; set; }
    /// <summary>Reasoning level override.</summary>
    public ReasoningLevel? ReasoningLevel { get; set; }

    public ConversationConfig Copy() => new()
    {
        ModelId = ModelId,
        SystemPrompt = SystemPrompt,
        Temperature = Temperature,
        TopP = TopP,
        MaxTokens = MaxTokens,
        AssistantId = AssistantId,
        ReasoningLevel = ReasoningLevel,
    };
}

public class Folder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public long CreateAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}

/// <summary>Port of me.rerere.rikkahub.data.model.Tag.</summary>
public class AssistantTag
{
    public string Name { get; set; } = "";
}

/// <summary>Port of me.rerere.rikkahub.data.model.Favorite (favorite messages).</summary>
public class Favorite
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Content { get; set; } = "";
    public Guid? ConversationId { get; set; }
    public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
