using TikTokMcpDesktop.Core.Mcp;

namespace TikTokMcpDesktop.Core.Tests;

public class ToolWhitelistTests
{
    [Theory]
    [InlineData("campaign_create", true)]
    [InlineData("ad_update", true)]
    [InlineData("smart_plus_campaign_create", true)]
    [InlineData("auth_advertiser_get", false)]
    [InlineData("report_integrated_get", false)]
    [InlineData("campaign_get", false)]
    public void Write_Detection(string name, bool isWrite)
    {
        Assert.Equal(isWrite, ToolWhitelist.IsWriteTool(name));
    }

    [Fact]
    public void Model_Allows_AIEcomPlex_Read_Catalog()
    {
        Assert.True(ToolWhitelist.IsAllowedForModel("auth_advertiser_get"));
        Assert.True(ToolWhitelist.IsAllowedForModel("report_integrated_get"));
        Assert.False(ToolWhitelist.IsAllowedForModel("campaign_create"));
    }

    [Fact]
    public void FilterForPull_Prefers_Read_Report_Tools()
    {
        var tools = new[]
        {
            new McpTool { Name = "campaign_create" },
            new McpTool { Name = "auth_advertiser_get" },
            new McpTool { Name = "foo_bar" }
        };
        var pull = ToolWhitelist.FilterForPull(tools);
        Assert.Contains(pull, t => t.Name == "auth_advertiser_get");
        Assert.DoesNotContain(pull, t => t.Name == "campaign_create");
    }
}
