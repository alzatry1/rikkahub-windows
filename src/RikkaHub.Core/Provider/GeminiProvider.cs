using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace RikkaHub.Core.Provider;

/// <summary>
/// Google Gemini provider — port of me.rerere.ai.provider.providers.google.GoogleProvider.
/// v1beta streamGenerateContent (SSE) + model listing.
/// </summary>
public class GeminiProvider : ProviderBase
{
    public override string Type => "gemini";

    public override async Task<List<Model>> ListModelsAsync(ProviderSetting setting, CancellationToken ct = default)
    {
        var s = (GeminiSetting)setting;
        var base_ = s.GetBaseUrl().TrimEnd('/');
        var url = $"{base_}/models";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        if (!string.IsNullOrEmpty(s.ApiKey)) req.Headers.Add("x-goog-api-key", s.ApiKey);
        foreach (var h in s.CustomHeaders) if (!string.IsNullOrWhiteSpace(h.Name)) req.Headers.TryAddWithoutValidation(h.Name, h.Value);
        using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!resp.IsSuccessStatusCode) throw new Exception(await ReadErrorAsync(resp));
        var json = await resp.Content.ReadAsStringAsync(ct);
        var models = new List<Model>();
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("models", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in arr.EnumerateArray())
            {
                var name = item.TryGetProperty("name", out var n) ? n.GetString() : null;
                if (string.IsNullOrEmpty(name)) continue;
                var modelId = name.StartsWith("models/") ? name["models/".Length..] : name;
                var m = new Model
                {
                    ModelId = modelId,
                    DisplayName = item.TryGetProperty("displayName", out var d) ? (d.GetString() ?? modelId) : modelId,
                    Type = ModelType.Chat,
                };
                if (item.TryGetProperty("supportedGenerationMethods", out var methods) && methods.ValueKind == JsonValueKind.Array)
                {
                    foreach (var method in methods.EnumerateArray())
                    {
                        var methodStr = method.GetString();
                        if (methodStr == "generateContent") { }
                        else if (methodStr == "embedContent") m.Type = ModelType.Embedding;
                    }
                }
                var id = modelId.ToLowerInvariant();
                if (id.Contains("thinking") || id.Contains("reasoning") || id.Contains("-pro") || id.Contains("flash-thinking") || id.Contains("2.5"))
                    m.Abilities.Add(ModelAbility.Reasoning);
                if (id.Contains("gemini")) m.Abilities.Add(ModelAbility.Tool);
                if (id.Contains("image") || id.Contains("nano-banana") || id.Contains("imagen")) m.InputModalities.Add(Modality.Image);
                models.Add(m);
            }
        }
        return models;
    }

    public override async IAsyncEnumerable<StreamChunk> StreamTextAsync(
        ProviderSetting setting, List<UIMessage> messages, TextGenerationParams p,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var s = (GeminiSetting)setting;
        var base_ = s.GetBaseUrl().TrimEnd('/');
        var model = p.Model.ModelId.TrimStart('/');
        var url = $"{base_}/models/{model}:streamGenerateContent?alt=sse";
        if (!string.IsNullOrEmpty(s.ApiKey)) url += $"&key={Uri.EscapeDataString(s.ApiKey)}";

        var body = BuildGeminiBody(messages, p);
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Content = new StringContent(body, Encoding.UTF8, "application/json");
        if (!string.IsNullOrEmpty(s.ApiKey)) req.Headers.Add("x-goog-api-key", s.ApiKey);
        ApplyCustomHeaders(req, s, p);

        HttpResponseMessage resp;
        try { resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct); }
        catch (Exception e) { yield return new ErrorChunk { Error = e.Message, IsStream = false }; yield break; }
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
                if (string.IsNullOrEmpty(line) || !line.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) continue;
                var data = line[5..].Trim();
                JsonDocument doc;
                try { doc = JsonDocument.Parse(data); }
                catch { continue; }
                using (doc)
                {
                    var root = doc.RootElement;
                    yield return new ChunkId { Id = root.TryGetProperty("responseId", out var rid) ? (rid.GetString() ?? "") : Guid.NewGuid().ToString() };
                    if (root.TryGetProperty("modelVersion", out var mv) && mv.ValueKind == JsonValueKind.String)
                        yield return new ChunkModel { Model = mv.GetString() ?? model };

                    if (root.TryGetProperty("candidates", out var candidates) && candidates.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var cand in candidates.EnumerateArray())
                        {
                            if (cand.TryGetProperty("finishReason", out var fr) && fr.ValueKind == JsonValueKind.String)
                            {
                                finish = fr.GetString();
                                yield return new FinishReasonChunk { Reason = finish };
                            }
                            if (cand.TryGetProperty("content", out var content) &&
                                content.ValueKind == JsonValueKind.Object &&
                                content.TryGetProperty("parts", out var parts) &&
                                parts.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var part in parts.EnumerateArray())
                                {
                                    if (part.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String)
                                    {
                                        var isThinking = part.TryGetProperty("thought", out var th) && th.ValueKind == JsonValueKind.True;
                                        var text = t.GetString();
                                        if (!string.IsNullOrEmpty(text))
                                        {
                                            if (isThinking) yield return new ThinkingDeltaChunk { Thinking = text };
                                            else yield return new TextDeltaChunk { Text = text };
                                        }
                                    }
                                }
                            }
                        }
                    }
                    if (root.TryGetProperty("usageMetadata", out var um) && um.ValueKind == JsonValueKind.Object)
                    {
                        yield return new UsageChunk
                        {
                            Usage = new TokenUsage
                            {
                                InputTokens = GetNum(um, "promptTokenCount"),
                                OutputTokens = GetNum(um, "candidatesTokenCount"),
                                ReasoningTokens = GetNum(um, "thoughtsTokenCount"),
                            },
                        };
                    }
                }
            }
            if (finish == null) yield return new FinishReasonChunk { Reason = "stop" };
        }
    }

    private static int GetNum(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? (int)v.GetInt64() : 0;

    public static string BuildGeminiBody(List<UIMessage> messages, TextGenerationParams p)
    {
        using var writer = new Utf8JsonWriter(new MemoryStream());
        writer.WriteStartObject();
        writer.WriteString("model", p.Model.ModelId);
        if (p.Temperature is { } temp) writer.WriteNumber("temperature", temp);
        if (p.TopP is { } topP) writer.WriteNumber("topP", topP);
        if (p.MaxTokens is { } mt) writer.WriteNumber("maxOutputTokens", mt);

        // System instructions (system messages)
        var systemTexts = messages.Where(m => m.Role == MessageRole.System).Select(m => m.Text).Where(t => !string.IsNullOrEmpty(t)).ToList();
        if (systemTexts.Count > 0)
        {
            writer.WriteStartObject("systemInstruction");
            writer.WriteStartArray("parts");
            foreach (var st in systemTexts)
            {
                writer.WriteStartObject();
                writer.WriteString("text", st);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        // Reasoning effort config (Gemini 2.5+ style thinkingBudget)
        if (p.ReasoningLevel is ReasoningLevel.On or ReasoningLevel.High or ReasoningLevel.Medium or ReasoningLevel.Low or ReasoningLevel.Auto)
        {
            var budget = p.ReasoningLevel switch
            {
                ReasoningLevel.High or ReasoningLevel.On => 8192,
                ReasoningLevel.Medium => 4096,
                ReasoningLevel.Low => 1024,
                _ => 2048, // Auto
            };
            writer.WriteStartObject("generationConfig");
            if (p.Temperature is { } temp2) writer.WriteNumber("temperature", temp2);
            if (p.TopP is { } topP2) writer.WriteNumber("topP", topP2);
            if (p.MaxTokens is { } mt2) writer.WriteNumber("maxOutputTokens", mt2);
            writer.WriteStartObject("thinkingConfig");
            writer.WriteNumber("thinkingBudget", budget);
            writer.WriteBoolean("includeThoughts", true);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        writer.WriteStartArray("contents");
        foreach (var m in messages)
        {
            if (m.Role == MessageRole.System) continue;
            var role = m.Role == MessageRole.Assistant ? "model" : "user";
            writer.WriteStartObject();
            writer.WriteString("role", role);
            writer.WriteStartArray("parts");
            if (m.Role == MessageRole.Tool)
            {
                foreach (var tr in m.Parts.OfType<ToolResultPart>())
                {
                    writer.WriteStartObject();
                    writer.WriteString("text", tr.Content);
                    writer.WriteEndObject();
                }
            }
            else
            {
                foreach (var part in m.Parts)
                {
                    switch (part)
                    {
                        case TextPart tp:
                            if (!string.IsNullOrEmpty(tp.Text))
                            {
                                writer.WriteStartObject();
                                writer.WriteString("text", tp.Text);
                                writer.WriteEndObject();
                            }
                            break;
                        case ThinkingPart:
                            // History thinking is not sent back for Gemini
                            break;
                        case ImagePart ip when !string.IsNullOrEmpty(ip.Data):
                            writer.WriteStartObject();
                            writer.WriteStartObject("inlineData");
                            writer.WriteString("mimeType", ip.MimeType);
                            writer.WriteString("data", ip.Data);
                            writer.WriteEndObject();
                            writer.WriteEndObject();
                            break;
                        case ToolCallPart tc:
                            // model tool call -> functionCall part
                            writer.WriteStartObject();
                            writer.WriteStartObject("functionCall");
                            writer.WriteString("name", tc.Name);
                            JsonElement args;
                            try { args = JsonDocument.Parse(string.IsNullOrWhiteSpace(tc.ArgsJson) ? "{}" : tc.ArgsJson).RootElement.Clone(); }
                            catch { args = JsonDocument.Parse("{}").RootElement.Clone(); }
                            foreach (var prop in args.EnumerateObject()) prop.WriteTo(writer);
                            writer.WriteEndObject();
                            writer.WriteEndObject();
                            break;
                        case ToolResultPart tr2:
                            writer.WriteStartObject();
                            writer.WriteStartObject("functionResponse");
                            writer.WriteString("name", tr2.ToolCallId);
                            writer.WriteStartObject("response");
                            writer.WriteString("result", tr2.Content);
                            writer.WriteEndObject();
                            writer.WriteEndObject();
                            writer.WriteEndObject();
                            break;
                    }
                }
                // Ensure non-empty parts for user messages
                if (m.Parts.Count == 0 && m.Role == MessageRole.User && !string.IsNullOrEmpty(m.Text))
                {
                    writer.WriteStartObject();
                    writer.WriteString("text", m.Text);
                    writer.WriteEndObject();
                }
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        writer.WriteEndArray(); // contents
        writer.WriteEndObject();
        writer.Flush();
        return Encoding.UTF8.GetString(((MemoryStream)writer.Output).ToArray());
    }
}
