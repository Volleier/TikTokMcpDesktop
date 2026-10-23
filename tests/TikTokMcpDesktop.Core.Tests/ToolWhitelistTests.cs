using TikTokMcpDesktop.Core.Mcp;

namespace TikTokMcpDesktop.Core.Tests;

public class ToolWhitelistTests
{
    [Theory]
    [InlineData("campaign_create", true)]
    [InlineData("ad_update", true)]
    [InlineData("advertiser_info_get", false)]
    [InlineData("report_integrated_get", false)]
    public void Write_Detection(string name, bool isWrite)
    {
        Assert.Equal(isWrite, ToolWhitelist.IsWriteTool(name));
    }

    [Fact]
    public void FilterForPull_Prefers_Read_Report_Tools()
    {
        var tools = new[]
        {
            new McpTool { Name = "campaign_create" },
            new McpTool { Name = "advertiser_info_get" },
            new McpTool { Name = "foo_bar" }
        };
        var pull = ToolWhitelist.FilterForPull(tools);
        Assert.Contains(pull, t => t.Name == "advertiser_info_get");
        Assert.DoesNotContain(pull, t => t.Name == "campaign_create");
    }
}
