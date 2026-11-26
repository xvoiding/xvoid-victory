namespace Xvoid.Launcher.Core;

/// <summary>Where the engine builds this launcher manages come from.</summary>
public static class Branding
{
    public const string AppName = "xvoid Launcher";
    public const string RepoOwner = "xvoiding";
    public const string RepoName = "xvoid-victory";
    public const string EngineAsset = "victory-engine-win-x64.zip";
    public const string ChecksumSuffix = ".sha256";
    public const string EngineExe = "victory.exe";
    public static string RepoUrl => $"https://github.com/{RepoOwner}/{RepoName}";
}
