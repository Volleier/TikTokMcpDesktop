using System.Text.Json;
using TikTokMcpDesktop.Core.Config;
using TikTokMcpDesktop.Core.Logging;
using TikTokMcpDesktop.Core.Mcp;
using TikTokMcpDesktop.Core.Store;

namespace TikTokMcpDesktop.Core.OpenAI;

public sealed class ChatAgent
{
    private readonly OpenAiClient _openAi;
    private readonly McpClient _mcp;
    private readonly ConfigStore _config;
    private readonly List<ChatMessage> _history = new();

    public ChatAgent(OpenAiClient openAi, McpClient mcp, ConfigStore config)
    {
        _openAi = openAi;
        _mcp = mcp;
        _config = config;
        _history.Add(new ChatMessage
        {
            Role = "system",
            Content = "你是 TikTok Ads 只读助手。只能调用已提供的只读 MCP 工具查询广告数据，禁止建议或执行任何创建/修改操作。用简洁中文回答。"
        });
    }

    public IReadOnlyList<ChatMessage> History => _history;

    public void Clear()
    {
        _history.RemoveRange(1, _history.Count - 1);
    }

    public async Task<string> SendAsync(string userText, int maxToolRounds = 5, CancellationToken ct = default)
    {
        var settings = _config.Load();
        var apiKey = _config.GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("请先在设置中填写 API Key。");
        if (string.IsNullOrWhiteSpace(settings.OpenAiBaseUrl) || string.IsNullOrWhiteSpace(settings.OpenAiModel))
            throw new InvalidOperationException("请先配置 base_url 与 model。");

        _history.Add(new ChatMessage { Role = "user", Content = userText });

        IReadOnlyList<object> tools;
        try
        {
            var mcpTools = await _mcp.ListToolsAsync(settings, ct);
            tools = MapTools(ToolWhitelist.FilterForModel(mcpTools));
        }
        catch (Exception ex)
        {
            AppLog.Warn($"tools/list for chat failed, continuing without tools: {ex.Message}");
            tools = Array.Empty<object>();
        }

        for (var round = 0; round < maxToolRounds; round++)
        {
            var result = await _openAi.ChatAsync(
                settings.OpenAiBaseUrl,
                apiKey!,
                settings.OpenAiModel,
                _history,
                tools.Count > 0 ? tools : null,
                ct);

            if (result.ToolCalls.Count == 0)
            {
                var content = result.Content ?? "";
                _history.Add(new ChatMessage { Role = "assistant", Content = content });
                return content;
            }

            _history.Add(new ChatMessage
            {
                Role = "assistant",
                Content = result.Content,
                ToolCalls = result.ToolCalls
            });

            foreach (var call in result.ToolCalls)
            {
                AppLog.Info($"Model tool_call: {call.Name}");
                string toolResult;
                try
                {
                    if (ToolWhitelist.IsWriteTool(call.Name))
                        throw new InvalidOperationException($"写工具已禁用: {call.Name}");

                    object? args = null;
                    if (!string.IsNullOrWhiteSpace(call.ArgumentsJson))
                    {
                        using var doc = JsonDocument.Parse(call.ArgumentsJson);
                        args = JsonSerializer.Deserialize<object>(doc.RootElement.GetRawText());
                    }

                    var raw = await _mcp.CallToolAsync(settings, call.Name, args, ct);
                    toolResult = Truncate(raw.GetRawText(), 24000);
                }
                catch (Exception ex)
                {
                    toolResult = JsonSerializer.Serialize(new { error = ex.Message });
                    AppLog.Warn($"tool {call.Name} failed: {ex.Message}");
                }

                _history.Add(new ChatMessage
                {
                    Role = "tool",
                    ToolCallId = call.Id,
                    Content = toolResult
                });
            }
        }

        var fallback = "工具调用轮次过多，请缩小问题后重试。";
        _history.Add(new ChatMessage { Role = "assistant", Content = fallback });
        return fallback;
    }

    private static List<object> MapTools(IReadOnlyList<McpTool> tools)
    {
        var list = new List<object>();
        foreach (var t in tools.Take(40))
        {
            var desc = t.Description ?? t.Name;
            if (desc.Length > 400) desc = desc[..400];
            object parameters = t.InputSchema.HasValue
                ? JsonSerializer.Deserialize<object>(t.InputSchema.Value.GetRawText())!
                : new { type = "object", properties = new { } };

            list.Add(new
            {
                type = "function",
                function = new
                {
                    name = t.Name,
                    description = desc,
                    parameters
                }
            });
        }
        return list;
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "...(truncated)";
}
