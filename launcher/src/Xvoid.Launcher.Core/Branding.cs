namespace Xvoid.Launcher.Core;

/// <summary>Where the engine builds this launcher manages come from.</summary>
public static class Branding
{
    public const string AppName = "xvoid Launcher";
    public const string RepoOwner = "xvoiding";
    public const string RepoName = "xvoid-victory";
    public const string ChecksumSuffix = ".sha256";
    public const string EngineExe = "victory.exe";
    public static string RepoUrl => $"https://github.com/{RepoOwner}/{RepoName}";

    /// <summary>The release file that holds one engine: victory-engine-nvidia-win-x64.zip, victory-engine-amd-win-x64.zip.</summary>
    public static string EngineAsset(EngineKind kind) => $"victory-engine-{EngineKinds.Folder(kind)}-win-x64.zip";
}
