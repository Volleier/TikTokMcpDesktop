namespace TikTokMcpDesktop.Core.Dashboards;

public sealed class DashboardSummary
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
}

public sealed class DashboardLayout
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<DashboardWidget> Widgets { get; set; } = new();
}

public sealed class DashboardWidget
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "kpi"; // kpi | chart-line | chart-bar | chart-pie | chart-tree | data-table
    public string Title { get; set; } = "";
    public int ColSpan { get; set; } = 1;
    public string? Metric { get; set; }
}

public sealed class DashboardData
{
    public string Id { get; set; } = "";
    public Dictionary<string, object?> Widgets { get; set; } = new();
    public bool Empty { get; set; }
    public string? Message { get; set; }
}
