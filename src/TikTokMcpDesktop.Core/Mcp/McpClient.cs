using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TikTokMcpDesktop.Core.Config;
using TikTokMcpDesktop.Core.Logging;
using TikTokMcpDesktop.Core.OAuth;

namespace TikTokMcpDesktop.Core.Mcp;

public sealed class McpTool
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public JsonElement? InputSchema { get; set; }
}

public sealed class McpClient
{
    private readonly OAuthClient _oauth;
    private readonly HttpClient _http;
    private long _id;

    public McpClient(OAuthClient oauth, HttpClient? http = null)
    {
        _oauth = oauth;
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
    }

    public async Task<IReadOnlyList<McpTool>> ListToolsAsync(AppSettings settings, CancellationToken ct = default)
    {
        var result = await CallAsync(settings, "tools/list", new { }, ct);
        var tools = new List<McpTool>();
        if (result.ValueKind == JsonValueKind.Object && result.TryGetProperty("tools", out var arr))
        {
            foreach (var item in arr.EnumerateArray())
            {
                tools.Add(new McpTool
                {
                    Name = item.GetProperty("name").GetString() ?? "",
                    Description = item.TryGetProperty("description", out var d) ? d.GetString() : null,
                    InputSchema = item.TryGetProperty("inputSchema", out var s) ? s.Clone() :
                                  item.TryGetProperty("input_schema", out var s2) ? s2.Clone() : null
                });
            }
        }
        AppLog.Info($"tools/list returned {tools.Count} tools");
        return tools;
    }

    public async Task<JsonElement> CallToolAsync(
        AppSettings settings,
        string name,
        object? arguments,
        CancellationToken ct = default)
    {
        // AIEcomPlex: not in allowlist / write → 403
        if (!ToolWhitelist.IsAllowedForModel(name))
            throw new InvalidOperationException($"工具未授权或已禁用: {name}");

        var args = arguments ?? new { };
        var result = await CallAsync(settings, "tools/call", new { name, arguments = args }, ct);
        return result;
    }

    public async Task<JsonElement> CallAsync(
        AppSettings settings,
        string method,
        object? parameters,
        CancellationToken ct = default)
    {
        var token = await _oauth.EnsureAccessTokenAsync(settings, ct);
        var url = settings.GetMcpUrl();
        var id = Interlocked.Increment(ref _id);
        var payload = new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = method,
            ["params"] = parameters ?? new { }
        };

        var json = JsonSerializer.Serialize(payload);
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        req.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        var media = resp.Content.Headers.ContentType?.MediaType ?? "";
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            AppLog.Error($"MCP {method} HTTP {(int)resp.StatusCode}: {AppLog.Redact(body)}");
            if ((int)resp.StatusCode is 401 or 403)
                throw new ReauthRequiredException("授权过期，请重连。");
            throw new InvalidOperationException($"MCP 调用失败 HTTP {(int)resp.StatusCode}");
        }

        var rpcJson = media.Contains("event-stream", StringComparison.OrdinalIgnoreCase)
            ? ExtractLastSseData(body)
            : body;

        using var doc = JsonDocument.Parse(rpcJson);
        var root = doc.RootElement;
        if (root.TryGetProperty("error", out var err))
        {
            var msg = err.TryGetProperty("message", out var m) ? m.GetString() : err.ToString();
            throw new InvalidOperationException($"MCP error: {msg}");
        }

        if (root.TryGetProperty("result", out var result))
            return result.Clone();

        return root.Clone();
    }

    internal static string ExtractLastSseData(string sse)
    {
        string? last = null;
        using var reader = new StringReader(sse);
        string? line;
        var buffer = new StringBuilder();
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                var part = line.Length > 5 ? line[5..].TrimStart() : "";
                if (buffer.Length > 0) buffer.Append('\n');
                buffer.Append(part);
            }
            else if (string.IsNullOrWhiteSpace(line))
            {
                if (buffer.Length > 0)
                {
                    last = buffer.ToString();
                    buffer.Clear();
                }
            }
        }
        if (buffer.Length > 0)
            last = buffer.ToString();

        return last ?? throw new InvalidOperationException("SSE 响应中未找到 data 帧");
    }
}
