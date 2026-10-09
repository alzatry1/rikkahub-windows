using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace RikkaHub.Core.Provider;

/// <summary>
/// OpenAI-compatible provider — port of me.rerere.ai.provider.providers.openai.OpenAIProvider
/// (Chat Completions + SSE stream). Supports: OpenAI, DeepSeek, aihubmix, OpenRouter, custom.
/// Also handles common "reasoning_content" stream field (DeepSeek style).
/// </summary>
public class OpenAIProvider : ProviderBase
{
    public override string Type => "openai";

    public override async Task<List<Model>> ListModelsAsync(ProviderSetting setting, CancellationToken ct = default)
    {
        var s = (OpenAISetting)setting;
        var base_ = s.GetBaseUrl();
        var url = $"{base_.TrimEnd('/')}/models";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        if (!string.IsNullOrEmpty(s.ApiKey)) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", s.ApiKey);
        foreach (var h in s.CustomHeaders) if (!string.IsNullOrWhiteSpace(h.Name)) req.Headers.TryAddWithoutValidation(h.Name, h.Value);
        using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!resp.IsSuccessStatusCode) throw new Exception(await ReadErrorAsync(resp));
        var json = await resp.Content.ReadAsStringAsync(ct);
        var models = new List<Model>();
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.EnumerateArray())
            {
                var modelId = item.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                if (string.IsNullOrEmpty(modelId)) continue;
                var m = new Model
                {
                    ModelId = modelId!,
                    DisplayName = modelId!,
                    Type = GuessType(modelId!),
                };
                GuessAbilities(modelId!, m);
                models.Add(m);
            }
        }
        return models;
    }

    internal static ModelType GuessType(string modelId)
    {
        var id = modelId.ToLowerInvariant();
        if (id.Contains("embedding") || id.Contains("bge-") || id.Contains("-rerank") || id.Contains("text-embedding")) return ModelType.Embedding;
        if (id.Contains("dall-e") || id.Contains("flux") || id.Contains("stable-diffusion") || id.Contains("cogview") || id.Contains("image") && id.Contains("gen")) return ModelType.Image;
        return ModelType.Chat;
    }

    internal static void GuessAbilities(string modelId, Model m)
    {
        var id = modelId.ToLowerInvariant();
        if (id.Contains("gpt-4") || id.Contains("gpt-5") || id.Contains("claude") || id.Contains("gemini") ||
            id.Contains("qwen") || id.Contains("glm") || id.Contains("deepseek") || id.Contains("kimi") || id.Contains("grok") ||
            id.Contains("doubao") || id.Contains("ernie") || id.Contains("moonshot") || id.Contains("minimax"))
            m.Abilities.Add(ModelAbility.Tool);
        if (id.Contains("reasoner") || id.Contains("thinking") || id.Contains("r1") || id.Contains("o1") ||
            id.Contains("o3") || id.Contains("o4") || id.Contains("claude") || id.Contains("gemini-2") || id.Contains("gemini-3"))
            m.Abilities.Add(ModelAbility.Reasoning);
    }

    public override async IAsyncEnumerable<StreamChunk> StreamTextAsync(
        ProviderSetting setting, List<UIMessage> messages, TextGenerationParams p,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var s = (OpenAISetting)setting;
        var base_ = s.GetBaseUrl().TrimEnd('/');
        var path = s.UseResponseApi ? s.ResponsesPath : s.ChatCompletionsPath;
        if (!path.StartsWith('/')) path = "/" + path;
        var url = base_ + path;

        var body = BuildChatCompletionsBody(s, messages, p, stream: true);
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Content = new StringContent(body, Encoding.UTF8, "application/json");
        if (!string.IsNullOrEmpty(s.ApiKey)) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", s.ApiKey);
        ApplyCustomHeaders(req, s, p);

        HttpResponseMessage? resp = null;
        Exception? sendError = null;
        try
        {
            resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (Exception e)
        {
            sendError = e;
        }
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
            string? finish = null;
            await foreach (var line in ReadSseLinesAsync(resp, ct))
            {
                if (string.IsNullOrEmpty(line)) continue;
                if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) continue;
                var data = line[5..].Trim();
                if (data == "[DONE]") break;
                JsonDocument doc;
                try { doc = JsonDocument.Parse(data); }
                catch { continue; }
                using (doc)
                {
                    var root = doc.RootElement;
                    if (root.TryGetProperty("id", out var idp) && idp.ValueKind == JsonValueKind.String)
                        yield return new ChunkId { Id = idp.GetString() ?? "" };
                    if (root.TryGetProperty("model", out var mp) && mp.ValueKind == JsonValueKind.String)
                        yield return new ChunkModel { Model = mp.GetString() ?? "" };

                    if (root.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var choice in choices.EnumerateArray())
                        {
                            if (choice.TryGetProperty("finish_reason", out var fr) && fr.ValueKind != JsonValueKind.Null)
                            {
                                var reason = fr.ValueKind == JsonValueKind.String ? fr.GetString() : null;
                                if (reason != null) { finish = reason; yield return new FinishReasonChunk { Reason = reason }; }
                            }
                            if (!choice.TryGetProperty("delta", out var delta) && choice.TryGetProperty("message", out var message))
                                delta = message;
                            else if (!choice.TryGetProperty("delta", out delta)) continue;

                            // text content
                            if (delta.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
                            {
                                var text = content.GetString();
                                if (!string.IsNullOrEmpty(text)) yield return new TextDeltaChunk { Text = text };
                            }
                            // reasoning (deepseek reasoning_content / openai reasoning)
                            if (delta.TryGetProperty("reasoning_content", out var rc) && rc.ValueKind == JsonValueKind.String)
                            {
                                var text = rc.GetString();
                                if (!string.IsNullOrEmpty(text)) yield return new ThinkingDeltaChunk { Thinking = text };
                            }
                            else if (delta.TryGetProperty("reasoning", out var r) && r.ValueKind == JsonValueKind.String)
                            {
                                var text = r.GetString();
                                if (!string.IsNullOrEmpty(text)) yield return new ThinkingDeltaChunk { Thinking = text };
                            }
                            // tool calls
                            if (delta.TryGetProperty("tool_calls", out var toolCalls) && toolCalls.ValueKind == JsonValueKind.Array)
                            {
                                for (int i = 0; i < toolCalls.GetArrayLength(); i++)
                                {
                                    var tc = toolCalls[i];
                                    var idx = tc.TryGetProperty("index", out var ix) && ix.ValueKind == JsonValueKind.Number ? ix.GetInt32() : i;
                                    var fn = tc.TryGetProperty("function", out var f) ? f : (JsonElement?)null;
                                    yield return new ToolCallDeltaChunk
                                    {
                                        Index = idx,
                                        Id = tc.TryGetProperty("id", out var tid) ? (tid.GetString() ?? "") : "",
                                        Name = fn?.TryGetProperty("name", out var n) ?? false ? (n.GetString() ?? "") : "",
                                        ArgsDelta = fn?.TryGetProperty("arguments", out var a) ?? false ? (a.GetString() ?? "") : "",
                                    };
                                }
                            }
                        }
                    }
                    if (root.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object)
                    {
                        yield return new UsageChunk
                        {
                            Usage = ParseUsage(usage),
                        };
                    }
                }
            }
            if (finish == null) yield return new FinishReasonChunk { Reason = "stop" };
        }
    }

    internal static TokenUsage ParseUsage(JsonElement usage)
    {
        int GetNum(string name, string altName = "")
        {
            foreach (var n in new[] { name, altName })
                if (!string.IsNullOrEmpty(n) && usage.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Number)
                    return (int)v.GetInt64();
            return 0;
        }
        return new TokenUsage
        {
            InputTokens = GetNum("prompt_tokens"),
            OutputTokens = GetNum("completion_tokens"),
            ReasoningTokens = GetNum("reasoning_tokens", "completion_tokens_details.reasoning_tokens"),
        };
    }

    /// <summary>Build chat/completions request body from UI messages.</summary>
    public static string BuildChatCompletionsBody(OpenAISetting s, List<UIMessage> messages, TextGenerationParams p, bool stream)
    {
        using var writer = new Utf8JsonWriter(new MemoryStream(), new JsonWriterOptions { Indented = false });
        writer.WriteStartObject();
        writer.WriteString("model", p.Model.ModelId);
        if (p.Temperature is { } temp) writer.WriteNumber("temperature", temp);
        if (p.TopP is { } topP) writer.WriteNumber("top_p", topP);
        if (p.MaxTokens is { } mt) writer.WriteNumber("max_tokens", mt);
        writer.WriteBoolean("stream", stream);
        if (stream)
        {
            writer.WriteStartObject("stream_options");
            writer.WriteBoolean("include_usage", true);
            writer.WriteEndObject();
        }
        // Tools
        if (p.Tools.Count > 0)
        {
            writer.WriteStartArray("tools");
            foreach (var t in p.Tools)
            {
                writer.WriteStartObject();
                writer.WriteStartObject("function");
                writer.WriteString("type", "function");
                writer.WriteString("name", t.Name);
                writer.WriteString("description", t.Description);
                if (t.Parameters is { } schema) { writer.WritePropertyName("parameters"); schema.WriteTo(writer); }
                else { writer.WritePropertyName("parameters"); writer.WriteStartObject(); writer.WriteEndObject(); }
                writer.WriteEndObject();
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

        writer.WriteStartArray("messages");
        foreach (var m in messages)
        {
            switch (m.Role)
            {
                case MessageRole.System:
                    writer.WriteStartObject();
                    writer.WriteString("role", "system");
                    writer.WriteString("content", m.Text ?? "");
                    writer.WriteEndObject();
                    break;
                case MessageRole.User:
                {
                    var images = m.Images;
                    if (images.Count > 0)
                    {
                        writer.WriteStartObject();
                        writer.WriteString("role", "user");
                        writer.WriteStartArray("content");
                        foreach (var img in images)
                        {
                            writer.WriteStartObject();
                            writer.WriteString("type", "image_url");
                            writer.WriteStartObject("image_url");
                            var dataUri = !string.IsNullOrEmpty(img.Data) ? $"data:{img.MimeType};base64,{img.Data}" : img.Url;
                            writer.WriteString("url", dataUri);
                            writer.WriteEndObject();
                            writer.WriteEndObject();
                        }
                        if (!string.IsNullOrEmpty(m.Text))
                        {
                            writer.WriteStartObject();
                            writer.WriteString("type", "text");
                            writer.WriteString("text", m.Text);
                            writer.WriteEndObject();
                        }
                        writer.WriteEndArray();
                        writer.WriteEndObject();
                    }
                    else
                    {
                        writer.WriteStartObject();
                        writer.WriteString("role", "user");
                        writer.WriteString("content", m.Text ?? "");
                        writer.WriteEndObject();
                    }
                    break;
                }
                case MessageRole.Assistant:
                {
                    var toolCalls = m.ToolCalls;
                    var thinking = m.Thinking;
                    var includeThinking = s.IncludeHistoryReasoning && !string.IsNullOrEmpty(thinking);
                    writer.WriteStartObject();
                    writer.WriteString("role", "assistant");
                    if (includeThinking)
                        writer.WriteString("reasoning_content", thinking);
                    if (toolCalls.Count > 0)
                    {
                        writer.WriteStartArray("tool_calls");
                        foreach (var tc in toolCalls)
                        {
                            writer.WriteStartObject();
                            writer.WriteString("id", tc.Id);
                            writer.WriteString("type", "function");
                            writer.WriteStartObject("function");
                            writer.WriteString("name", tc.Name);
                            writer.WriteString("arguments", string.IsNullOrWhiteSpace(tc.ArgsJson) ? "{}" : tc.ArgsJson);
                            writer.WriteEndObject();
                            writer.WriteEndObject();
                        }
                        writer.WriteEndArray();
                        if (string.IsNullOrEmpty(m.Text))
                            writer.WriteNull("content");
                        else
                            writer.WriteString("content", m.Text);
                    }
                    else
                    {
                        writer.WriteString("content", m.Text ?? "");
                    }
                    writer.WriteEndObject();
                    break;
                }
                case MessageRole.Tool:
                {
                    foreach (var tr in m.Parts.OfType<ToolResultPart>())
                    {
                        writer.WriteStartObject();
                        writer.WriteString("role", "tool");
                        writer.WriteString("tool_call_id", tr.ToolCallId);
                        writer.WriteString("content", tr.Content);
                        writer.WriteEndObject();
                    }
                    break;
                }
            }
        }
        writer.WriteEndArray(); // messages
        writer.WriteEndObject();
        writer.Flush();
        var json = Encoding.UTF8.GetString(((MemoryStream)writer.Output).ToArray());
        return MergeCustomBody(json, s.CustomBodies(p));
    }
}

internal static class OpenAISettingExt
{
    public static IEnumerable<CustomBody> CustomBodies(this OpenAISetting s, TextGenerationParams p)
    {
        foreach (var b in s.Models.FirstOrDefault(m => m.Id == p.Model.Id)?.CustomBodies ?? Enumerable.Empty<CustomBody>()) yield return b;
        foreach (var b in p.Model.CustomBodies) yield return b;
        foreach (var b in p.CustomBody) yield return b;
    }
}
