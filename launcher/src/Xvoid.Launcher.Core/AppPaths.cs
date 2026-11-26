namespace Xvoid.Launcher.Core;

/// <summary>Every folder the launcher writes to, under one root.</summary>
public sealed class AppPaths
{
    public string Root { get; }
    public string Engines => Path.Combine(Root, "engines");
    /// <summary>Translated shaders and pipelines (VICTORY_CACHE_DIR).</summary>
    public string Cache => Path.Combine(Root, "cache");
    /// <summary>The engine's own data folder (VICTORY_DATA_DIR).</summary>
    public string Data => Path.Combine(Root, "data");
    public string Logs => Path.Combine(Root, "logs");
    public string Reports => Path.Combine(Root, "reports");
    public string SettingsFile => Path.Combine(Root, "settings.json");

    public AppPaths(string? root = null)
    {
        Root = root ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "xvoid-launcher");
    }

    public void EnsureCreated()
    {
        foreach (var dir in new[] { Root, Engines, Cache, Data, Logs, Reports })
            Directory.CreateDirectory(dir);
    }

    /// <summary>The engine keeps host pipelines next to its shader cache.</summary>
    public string PipelineFolder => Path.Combine(Cache, "pipelines");
    public string ShaderFolder => Path.Combine(Cache, "shaders");
}
