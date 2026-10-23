using TikTokMcpDesktop.Core.Mcp;

namespace TikTokMcpDesktop.Core.Tests;

public class McpSseTests
{
    [Fact]
    public void ExtractLastSseData_Takes_Final_Data_Frame()
    {
        var sse = """
            event: message
            data: {"jsonrpc":"2.0","id":1,"result":{"partial":true}}

            data: {"jsonrpc":"2.0","id":1,"result":{"tools":[{"name":"a"}]}}

            """;
        var json = McpClient.ExtractLastSseData(sse);
        Assert.Contains("\"tools\"", json);
        Assert.DoesNotContain("partial", json);
    }
}
