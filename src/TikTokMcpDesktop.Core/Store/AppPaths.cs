namespace TikTokMcpDesktop.Core.Store;

public sealed class AppPaths
{
    public string Root { get; }
    public string DataDir => Path.Combine(Root, "data");
    public string PullsDir => Path.Combine(DataDir, "pulls");
    public string ExportsDir => Path.Combine(DataDir, "exports");
    public string DbPath => Path.Combine(DataDir, "app.db");
    public string SettingsPath => Path.Combine(DataDir, "settings.json");
    public string SecretsPath => Path.Combine(DataDir, "secrets.bin");
    public string LogPath => Path.Combine(DataDir, "app.log");
    public string WebUiDir => Path.Combine(Root, "web", "ui", "dist");

    public AppPaths(string? root = null)
    {
        Root = root ?? FindRoot();
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(PullsDir);
        Directory.CreateDirectory(ExportsDir);
    }

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "TikTokMcpDesktop.sln")) ||
                Directory.Exists(Path.Combine(dir.FullName, "web", "ui", "dist")) ||
                Directory.Exists(Path.Combine(dir.FullName, "web", "ui")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }

        var local = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TikTokMcpDesktop");
        Directory.CreateDirectory(local);
        return local;
    }
}
