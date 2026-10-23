using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TikTokMcpDesktop.Core.Logging;

namespace TikTokMcpDesktop.Core.OpenAI;

public sealed class ChatMessage
{
    public string Role { get; set; } = "user";
    public string? Content { get; set; }
    public string? Name { get; set; }
    public string? ToolCallId { get; set; }
    public List<ToolCall>? ToolCalls { get; set; }
}

public sealed class ToolCall
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string ArgumentsJson { get; set; } = "{}";
}

public sealed class ChatResult
{
    public string? Content { get; set; }
    public List<ToolCall> ToolCalls { get; set; } = new();
    public string FinishReason { get; set; } = "";
}

public sealed class OpenAiClient
{
    private readonly HttpClient _http;

    public OpenAiClient(HttpClient? http = null)
    {
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(180) };
    }

    public async Task<ChatResult> ChatAsync(
        string baseUrl,
        string apiKey,
        string model,
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<object>? tools = null,
        CancellationToken ct = default)
    {
        var url = $"{baseUrl.TrimEnd('/')}/chat/completions";
        var body = new JsonObject
        {
            ["model"] = model,
            ["messages"] = ToMessagesNode(messages)
        };
        if (tools is { Count: > 0 })
        {
            body["tools"] = JsonSerializer.SerializeToNode(tools);
            body["tool_choice"] = "auto";
        }

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        req.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

        using var resp = await _http.SendAsync(req, ct);
        var text = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            AppLog.Error($"OpenAI HTTP {(int)resp.StatusCode}: {AppLog.Redact(text)}");
            throw new InvalidOperationException($"模型接口失败 HTTP {(int)resp.StatusCode}: {Trim(text, 300)}");
        }

        using var doc = JsonDocument.Parse(text);
        var choice = doc.RootElement.GetProperty("choices")[0];
        var message = choice.GetProperty("message");
        var result = new ChatResult
        {
            Content = message.TryGetProperty("content", out var c) && c.ValueKind != JsonValueKind.Null
                ? c.GetString()
                : null,
            FinishReason = choice.TryGetProperty("finish_reason", out var fr) ? fr.GetString() ?? "" : ""
        };

        if (message.TryGetProperty("tool_calls", out var tcs) && tcs.ValueKind == JsonValueKind.Array)
        {
            foreach (var tc in tcs.EnumerateArray())
            {
                var fn = tc.GetProperty("function");
                result.ToolCalls.Add(new ToolCall
                {
                    Id = tc.GetProperty("id").GetString() ?? "",
                    Name = fn.GetProperty("name").GetString() ?? "",
                    ArgumentsJson = fn.TryGetProperty("arguments", out var a) ? a.GetString() ?? "{}" : "{}"
                });
            }
        }

        return result;
    }

    private static JsonArray ToMessagesNode(IReadOnlyList<ChatMessage> messages)
    {
        var arr = new JsonArray();
        foreach (var m in messages)
        {
            var node = new JsonObject { ["role"] = m.Role };
            if (m.Content is not null) node["content"] = m.Content;
            if (m.Name is not null) node["name"] = m.Name;
            if (m.ToolCallId is not null) node["tool_call_id"] = m.ToolCallId;
            if (m.ToolCalls is { Count: > 0 })
            {
                var tcs = new JsonArray();
                foreach (var tc in m.ToolCalls)
                {
                    tcs.Add(new JsonObject
                    {
                        ["id"] = tc.Id,
                        ["type"] = "function",
                        ["function"] = new JsonObject
                        {
                            ["name"] = tc.Name,
                            ["arguments"] = tc.ArgumentsJson
                        }
                    });
                }
                node["tool_calls"] = tcs;
            }
            arr.Add(node);
        }
        return arr;
    }

    private static string Trim(string s, int n) => s.Length <= n ? s : s[..n] + "...";
}
