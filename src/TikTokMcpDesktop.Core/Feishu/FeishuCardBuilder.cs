using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TikTokMcpDesktop.Core.Feishu;

/// <summary>Builds Feishu interactive card JSON for TikTok Ads MCP summaries.</summary>
public static class FeishuCardBuilder
{
    public static string BuildAdsSummaryCard(
        double spend,
        double impressions,
        double clicks,
        double conversions,
        IReadOnlyList<(string Name, double Spend)> topCampaigns,
        string? syncMessage = null)
    {
        var lines = new List<string>
        {
            $"**消耗** {spend.ToString("N2", CultureInfo.InvariantCulture)} CNY",
            $"**展示** {impressions.ToString("N0", CultureInfo.InvariantCulture)}",
            $"**点击** {clicks.ToString("N0", CultureInfo.InvariantCulture)}",
            $"**转化** {conversions.ToString("N0", CultureInfo.InvariantCulture)}"
        };
        if (topCampaigns.Count > 0)
        {
            lines.Add("");
            lines.Add("**系列消耗 Top**");
            foreach (var (name, s) in topCampaigns.Take(5))
                lines.Add($"· {name}: {s.ToString("N2", CultureInfo.InvariantCulture)}");
        }
        if (!string.IsNullOrWhiteSpace(syncMessage))
        {
            lines.Add("");
            lines.Add($"_{syncMessage}_");
        }

        var card = new JsonObject
        {
            ["config"] = new JsonObject { ["wide_screen_mode"] = true },
            ["header"] = new JsonObject
            {
                ["template"] = "turquoise",
                ["title"] = new JsonObject
                {
                    ["tag"] = "plain_text",
                    ["content"] = $"TikTok Ads 数据 · {DateTimeOffset.Now:MM-dd HH:mm}"
                }
            },
            ["elements"] = new JsonArray
            {
                new JsonObject
                {
                    ["tag"] = "div",
                    ["text"] = new JsonObject
                    {
                        ["tag"] = "lark_md",
                        ["content"] = string.Join("\n", lines)
                    }
                },
                new JsonObject
                {
                    ["tag"] = "note",
                    ["elements"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["tag"] = "plain_text",
                            ["content"] = "数据来源：TikTok for Business Open MCP（只读）"
                        }
                    }
                }
            }
        };

        return card.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    public static string BuildTextFallback(
        double spend,
        double impressions,
        double clicks,
        double conversions) =>
        JsonSerializer.Serialize(new
        {
            text =
                $"TikTok Ads 汇总\n消耗 {spend:N2} CNY\n展示 {impressions:N0}\n点击 {clicks:N0}\n转化 {conversions:N0}"
        });
}
