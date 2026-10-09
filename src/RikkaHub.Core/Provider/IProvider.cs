using System.Net.Http.Headers;

namespace RikkaHub.Core.Provider;

public interface IProvider
{
    /// <summary>Provider type key, e.g. "openai", "gemini", "claude".</summary>
    string Type { get; }

    Task<List<Model>> ListModelsAsync(ProviderSetting setting, CancellationToken ct = default);

    IAsyncEnumerable<StreamChunk> StreamTextAsync(
        ProviderSetting setting,
        List<UIMessage> messages,
        TextGenerationParams parameters,
        CancellationToken ct = default);

    Task<TextResult> GenerateTextAsync(
        ProviderSetting setting,
        List<UIMessage> messages,
        TextGenerationParams parameters,
        CancellationToken ct = default);
}

/// <summary>Port of TextGenerationResult.</summary>
public record TextResult(
    string Id,
    string Model,
    UIMessage Message,
    string? FinishReason = null,
    TokenUsage? Usage = null);

public abstract class ProviderBase : IProvider
{
    public abstract string Type { get; }

    protected static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            MaxConnectionsPerServer = 64,
        };
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("RikkaHub-Windows/1.0");
        return client;
    }

    public abstract Task<List<Model>> ListModelsAsync(ProviderSetting setting, CancellationToken ct = default);

    public abstract IAsyncEnumerable<StreamChunk> StreamTextAsync(
        ProviderSetting setting, List<UIMessage> messages, TextGenerationParams parameters, CancellationToken ct = default);

    public virtual async Task<TextResult> GenerateTextAsync(
        ProviderSetting setting, List<UIMessage> messages, TextGenerationParams parameters, CancellationToken ct = default)
    {
        var msg = new UIMessage { Role = MessageRole.Assistant, Pending = false };
        string id = "", model = "";
        string? finish = null;
        TokenUsage? usage = null;
        await foreach (var chunk in StreamTextAsync(setting, messages, parameters, ct))
        {
            switch (chunk)
            {
                case ChunkId c: id = c.Id; break;
                case ChunkModel c: model = c.Model; break;
                case TextDeltaChunk c:
                    var last = msg.Parts.LastOrDefault(p => p is TextPart) as TextPart;
                    if (last != null) last.Text += c.Text;
                    else msg.Parts.Add(new TextPart { Text = c.Text });
                    break;
                case ThinkingDeltaChunk c:
                    var lastTh = msg.Parts.LastOrDefault(p => p is ThinkingPart) as ThinkingPart;
                    if (lastTh != null) lastTh.Thinking += c.Thinking;
                    else msg.Parts.Add(new ThinkingPart { Thinking = c.Thinking });
                    break;
                case UsageChunk c: usage = c.Usage; break;
                case FinishReasonChunk c: finish = c.Reason; break;
            }
        }
        return new TextResult(id, model, msg, finish, usage);
    }

    /// <summary>Apply custom headers from provider/model/params.</summary>
    protected void ApplyCustomHeaders(HttpRequestMessage req, ProviderSetting setting, TextGenerationParams? parameters)
    {
        foreach (var h in setting.CustomHeaders)
            if (!string.IsNullOrWhiteSpace(h.Name)) req.Headers.TryAddWithoutValidation(h.Name, h.Value);
        if (parameters != null)
            foreach (var h in parameters.CustomHeaders)
                if (!string.IsNullOrWhiteSpace(h.Name)) req.Headers.TryAddWithoutValidation(h.Name, h.Value);
        foreach (var h in parameters?.Model.CustomHeaders ?? new List<CustomHeader>())
            if (!string.IsNullOrWhiteSpace(h.Name)) req.Headers.TryAddWithoutValidation(h.Name, h.Value);
    }

    /// <summary>Merge custom body entries into the request JSON.</summary>
    protected static string MergeCustomBody(string baseJson, IEnumerable<CustomBody> entries)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(baseJson);
        var root = doc.RootElement.Clone();
        var writer = new System.Text.Json.Utf8JsonWriter(new MemoryStream());
        writer.WriteStartObject();
        foreach (var prop in root.EnumerateObject())
            prop.WriteTo(writer);
        foreach (var cb in entries)
        {
            if (string.IsNullOrWhiteSpace(cb.Key) || root.TryGetProperty(cb.Key, out _)) continue;
            if (cb.Value is { } v) { writer.WritePropertyName(cb.Key); v.WriteTo(writer); }
        }
        writer.WriteEndObject();
        writer.Flush();
        var bytes = ((MemoryStream)writer.Output).ToArray();
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    /// <summary>Read an HttpResponseMessage with a useful error message.</summary>
    protected static async Task<string> ReadErrorAsync(HttpResponseMessage resp)
    {
        var body = await resp.Content.ReadAsStringAsync();
        return $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}: {Truncate(body, 2000)}";
    }

    protected static string Truncate(string s, int len) => s.Length <= len ? s : s[..len] + "…";

    /// <summary>SSE line reader over a response stream.</summary>
    protected static async IAsyncEnumerable<string> ReadSseLinesAsync(HttpResponseMessage resp, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null) break;
            yield return line;
        }
    }
}
