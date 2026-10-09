using System.Text;
using System.Text.Json;

namespace RikkaHub.Core.Provider;

/// <summary>
/// Anthropic Claude provider — port of me.rerere.ai.provider.providers.claude.ClaudeProvider.
/// /v1/messages with SSE stream.
/// </summary>
public class ClaudeProvider : ProviderBase
{
    public override string Type => "claude";

    public override Task<List<Model>> ListModelsAsync(ProviderSetting setting, CancellationToken ct = default)
    {
        // Anthropic does not expose a model list endpoint historically; use a curated list
        // matching ModelRegistry of the Android app.
        var models = new List<Model>
        {
            MakeModel("claude-opus-4-5", "Claude Opus 4.5"),
            MakeModel("claude-sonnet-4-5", "Claude Sonnet 4.5"),
            MakeModel("claude-sonnet-4-5-20250929", "Claude Sonnet 4.5 (2025-09-29)"),
            MakeModel("claude-haiku-4-5", "Claude Haiku 4.5"),
            MakeModel("claude-opus-4-1", "Claude Opus 4.1"),
            MakeModel("claude-sonnet-4-0", "Claude Sonnet 4"),
            MakeModel("claude-3-7-sonnet-latest", "Claude 3.7 Sonnet"),
            MakeModel("claude-3-5-haiku-latest", "Claude 3.5 Haiku"),
        };
        return Task.FromResult(models);
    }

    private static Model MakeModel(string id, string name)
    {
        var m = new Model { ModelId = id, DisplayName = name };
        m.Abilities.Add(ModelAbility.Tool);
        m.Abilities.Add(ModelAbility.Reasoning);
        m.InputModalities.Add(Modality.Image);
        return m;
    }

    public override async IAsyncEnumerable<StreamChunk> StreamTextAsync(
        ProviderSetting setting, List<UIMessage> messages, TextGenerationParams p,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var s = (ClaudeSetting)setting;
        var base_ = s.GetBaseUrl().TrimEnd('/');
        var url = $"{base_}/v1/messages";
        var body = BuildClaudeBody(s, messages, p);
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Content = new StringContent(body, Encoding.UTF8, "application/json");
        if (!string.IsNullOrEmpty(s.ApiKey)) req.Headers.Add("x-api-key", s.ApiKey);
        req.Headers.Add("anthropic-version", string.IsNullOrWhiteSpace(s.Version) ? "2023-06-01" : s.Version);
        ApplyCustomHeaders(req, s, p);

        HttpResponseMessage? resp = null;
        Exception? sendError = null;
        try { resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct); }
        catch (Exception e) { sendError = e; }
        if (sendError != null || resp == null)
        {
            yield return new ErrorChunk { Error = sendError?.Message ?? "request failed", IsStream = false };
            yield break;
        }
        using (resp)
        {
            if (!resp.IsSuccessStatusCode)
            {
                yield return new ErrorChunk { Error = await ReadErrorAsync(resp), IsStream = false };
                yield break;
            }
            string? currentToolId = null, currentToolName = null;
            var toolArgs = new StringBuilder();
            await foreach (var line in ReadSseLinesAsync(resp, ct))
            {
                if (string.IsNullOrEmpty(line)) continue;
                if (!line.StartsWith("event:", StringComparison.OrdinalIgnoreCase) &&
                    !line.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) continue;
                if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) continue;
                var data = line[5..].Trim();
                JsonDocument doc;
                try { doc = JsonDocument.Parse(data); }
                catch { continue; }
                using (doc)
                {
                    var root = doc.RootElement;
                    var type = root.TryGetProperty("type", out var t) ? t.GetString() : null;
                    switch (type)
                    {
                        case "message_start":
                            if (root.TryGetProperty("message", out var message))
                            {
                                if (message.TryGetProperty("id", out var mid)) yield return new ChunkId { Id = mid.GetString() ?? "" };
                                if (message.TryGetProperty("model", out var mm)) yield return new ChunkModel { Model = mm.GetString() ?? "" };
                            }
                            break;
                        case "content_block_start":
                            if (root.TryGetProperty("content_block", out var cb) && cb.TryGetProperty("type", out var cbt))
                            {
                                var blockType = cbt.GetString();
                                if (blockType == "tool_use")
                                {
                                    currentToolId = cb.TryGetProperty("id", out var cid) ? cid.GetString() : null;
                                    currentToolName = cb.TryGetProperty("name", out var cn) ? cn.GetString() : null;
                                    toolArgs.Clear();
                                }
                            }
                            break;
                        case "content_block_delta":
                            if (root.TryGetProperty("delta", out var delta))
                            {
                                if (delta.TryGetProperty("type", out var dt))
                                {
                                    var deltaType = dt.GetString();
                                    if (deltaType == "text_delta" && delta.TryGetProperty("text", out var txt))
                                    {
                                        var text = txt.GetString();
                                        if (!string.IsNullOrEmpty(text)) yield return new TextDeltaChunk { Text = text };
                                    }
                                    else if (deltaType == "thinking_delta" && delta.TryGetProperty("thinking", out var thk))
                                    {
                                        var thinking = thk.GetString();
                                        if (!string.IsNullOrEmpty(thinking)) yield return new ThinkingDeltaChunk { Thinking = thinking };
                                    }
                                    else if (deltaType == "input_json_delta" && delta.TryGetProperty("partial_json", out var pj))
                                    {
                                        toolArgs.Append(pj.GetString() ?? "");
                                    }
                                }
                            }
                            break;
                        case "content_block_stop":
                            if (currentToolId != null)
                            {
                                yield return new ToolCallDeltaChunk
                                {
                                    Index = 0,
                                    Id = currentToolId,
                                    Name = currentToolName ?? "",
                                    ArgsDelta = toolArgs.ToString(),
                                };
                                currentToolId = null; currentToolName = null; toolArgs.Clear();
                            }
                            break;
                        case "message_delta":
                            if (root.TryGetProperty("delta", out var md) && md.TryGetProperty("stop_reason", out var sr))
                                yield return new FinishReasonChunk { Reason = sr.GetString() };
                            if (root.TryGetProperty("usage", out var usage))
                            {
                                yield return new UsageChunk
                                {
                                    Usage = new TokenUsage
                                    {
                                        InputTokens = usage.TryGetProperty("input_tokens", out var it) && it.ValueKind == JsonValueKind.Number ? it.GetInt32() : 0,
                                        OutputTokens = usage.TryGetProperty("output_tokens", out var ot) && ot.ValueKind == JsonValueKind.Number ? ot.GetInt32() : 0,
                                    },
                                };
                            }
                            break;
                    }
                }
            }
        }
    }

    public static string BuildClaudeBody(ClaudeSetting s, List<UIMessage> messages, TextGenerationParams p)
    {
        using var writer = new Utf8JsonWriter(new MemoryStream());
        writer.WriteStartObject();
        writer.WriteString("model", p.Model.ModelId);
        writer.WriteNumber("max_tokens", p.MaxTokens ?? 8192);
        if (p.Temperature is { } temp) writer.WriteNumber("temperature", temp);
        if (p.TopP is { } topP) writer.WriteNumber("top_p", topP);
        if (p.ReasoningLevel is ReasoningLevel.High or ReasoningLevel.On)
        {
            writer.WriteStartObject("thinking");
            writer.WriteBoolean("enabled", true);
            writer.WriteNumber("budget_tokens", 8192);
            writer.WriteEndObject();
        }

        // system prompt
        var system = string.Join("\n\n", messages.Where(m => m.Role == MessageRole.System).Select(m => m.Text).Where(t => !string.IsNullOrEmpty(t)));
        if (!string.IsNullOrEmpty(system)) writer.WriteString("system", system);

        writer.WriteStartArray("messages");
        foreach (var m in messages)
        {
            if (m.Role == MessageRole.System) continue;
            if (m.Role == MessageRole.Tool) continue; // tool results attached to the tool message below
            var role = m.Role == MessageRole.User ? "user" : "assistant";
            // Attach any pending tool results as a user message before this message
            writer.WriteStartObject();
            writer.WriteString("role", role);
            writer.WriteStartArray("content");
            if (role == "user")
            {
                foreach (var part in m.Parts)
                {
                    switch (part)
                    {
                        case TextPart tp when !string.IsNullOrEmpty(tp.Text):
                            writer.WriteStartObject();
                            writer.WriteString("type", "text");
                            writer.WriteString("text", tp.Text);
                            writer.WriteEndObject();
                            break;
                        case ImagePart ip when !string.IsNullOrEmpty(ip.Data):
                            writer.WriteStartObject();
                            writer.WriteString("type", "image");
                            writer.WriteStartObject("source");
                            writer.WriteString("type", "base64");
                            writer.WriteString("media_type", ip.MimeType);
                            writer.WriteString("data", ip.Data);
                            writer.WriteEndObject();
                            writer.WriteEndObject();
                            break;
                    }
                }
            }
            else
            {
                var thinking = m.Thinking;
                if (s.IncludeHistoryReasoning && !string.IsNullOrEmpty(thinking))
                {
                    writer.WriteStartObject();
                    writer.WriteString("type", "thinking");
                    writer.WriteString("thinking", thinking);
                    writer.WriteEndObject();
                }
                var toolCalls = m.ToolCalls;
                if (toolCalls.Count > 0)
                {
                    foreach (var tc in toolCalls)
                    {
                        writer.WriteStartObject();
                        writer.WriteString("type", "tool_use");
                        writer.WriteString("id", tc.Id);
                        writer.WriteString("name", tc.Name);
                        writer.WritePropertyName("input");
                        JsonElement args;
                        try { args = JsonDocument.Parse(string.IsNullOrWhiteSpace(tc.ArgsJson) ? "{}" : tc.ArgsJson).RootElement.Clone(); }
                        catch { args = JsonDocument.Parse("{}").RootElement.Clone(); }
                        args.WriteTo(writer);
                        writer.WriteEndObject();
                    }
                }
                var text = m.Text;
                if (!string.IsNullOrEmpty(text) || toolCalls.Count == 0)
                {
                    writer.WriteStartObject();
                    writer.WriteString("type", "text");
                    writer.WriteString("text", text ?? "");
                    writer.WriteEndObject();
                }
            }
            writer.WriteEndArray();
            writer.WriteEndObject();

            // Tool results become the next user message
            var following = messages.SkipWhile(x => x != m).Skip(1).FirstOrDefault();
            if (m.ToolCalls.Count > 0)
            {
                var results = following?.Parts.OfType<ToolResultPart>().ToList() ?? new List<ToolResultPart>();
                writer.WriteStartObject();
                writer.WriteString("role", "user");
                writer.WriteStartArray("content");
                foreach (var tc in m.ToolCalls)
                {
                    var result = results.FirstOrDefault(r => r.ToolCallId == tc.Id);
                    writer.WriteStartObject();
                    writer.WriteString("type", "tool_result");
                    writer.WriteString("tool_use_id", tc.Id);
                    writer.WriteString("content", result?.Content ?? "");
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.Flush();
        return Encoding.UTF8.GetString(((MemoryStream)writer.Output).ToArray());
    }
}
