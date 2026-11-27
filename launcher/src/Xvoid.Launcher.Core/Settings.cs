using System.Text.Json;
using System.Text.Json.Serialization;

namespace Xvoid.Launcher.Core;

public enum Preset
{
    /// <summary>The fast ways, sized to this PC.</summary>
    Auto,
    /// <summary>Without the newer, faster ways. Try it first when something is wrong.</summary>
    Safe,
    /// <summary>Series S model and a texture budget, for 8 GB cards. Experimental.</summary>
    LowMemory,
}

public enum EngineSource { Release, Local }

/// <summary>NVIDIA engine: what the picture is drawn at. Auto is 1440p with DLSS and 1080p upscaled by the game without it.</summary>
public enum Resolution { Auto, P1440, P1080To1440 }

/// <summary>NVIDIA engine: DLSS Super Resolution in place of the game's own upscaling (needs NVIDIA's files).</summary>
public enum DlssMode { Off, Quality, Balanced, Performance }

/// <summary>
/// NVIDIA engine: DLSS frame generation, RTX 40 and newer, 2x. Ready is how the engine's own launcher starts: made ready but
/// not on, so F1 in the game switches it. Off does not make it ready, which leaves its memory to the textures.
/// </summary>
public enum FrameGeneration { Ready, Off, On2x }

/// <summary>What the engine told us about the GPU it runs on; the shader cache is keyed by it.</summary>
public sealed class HostProfile
{
    public int WaveMin { get; set; }
    public int WaveMax { get; set; }
    public long ViewLimit { get; set; }
    public bool IsKnown => WaveMax > 0 && ViewLimit > 0;
}

public sealed class Settings
{
    /// <summary>2: the engine toggle (per-engine builds and caches) and the ambient light that follows the engine.</summary>
    public const int CurrentVersion = 2;

    public int Version { get; set; } = CurrentVersion;
    public bool SetupDone { get; set; }
    /// <summary>The title's executable, in the folder the engine reads its files from.</summary>
    public string? GameExe { get; set; }
    /// <summary>The toggle: the NVIDIA engine, the AMD engine, or whichever suits this PC's graphics card.</summary>
    public EngineChoice Engine { get; set; } = EngineChoice.Auto;
    public EngineSource EngineSource { get; set; } = EngineSource.Release;
    /// <summary>A victory.exe built by hand; it is taken to be of the engine the toggle names.</summary>
    public string? LocalEnginePath { get; set; }
    public string? InstalledNvidiaTag { get; set; }
    public string? InstalledAmdTag { get; set; }
    public Preset Preset { get; set; } = Preset.Auto;
    /// <summary>series-x or series-s.</summary>
    public string Console { get; set; } = "series-x";
    /// <summary>0: no limit.</summary>
    public int TextureBudgetMb { get; set; }
    /// <summary>0: chosen from the number of processors.</summary>
    public int ListThreads { get; set; }
    public bool RayTracing { get; set; }
    /// <summary>
    /// The stand-in for ray-traced indirect light. Null: the engine's own choice (on for the NVIDIA engine, whose launcher
    /// sets it; off for the AMD one, where it blew the picture out on an RTX 4060 Laptop).
    /// </summary>
    public bool? AmbientLight { get; set; }
    public bool RecordPipelines { get; set; } = true;

    // NVIDIA engine only.
    public Resolution Resolution { get; set; } = Resolution.Auto;
    public DlssMode Dlss { get; set; } = DlssMode.Quality;
    public FrameGeneration FrameGeneration { get; set; } = FrameGeneration.Ready;
    /// <summary>
    /// A new victory.exe runs at a flat ~14 fps on its first start and normally from then on (the engine's own notes), so the
    /// launcher plays a build once for about 20 seconds, closes it and starts it again.
    /// </summary>
    public bool WarmUp { get; set; } = true;

    public Dictionary<string, string> ExtraEnvironment { get; set; } = new();
    public HostProfile Host { get; set; } = new();
    /// <summary>Per engine (its folder name): the engine label ("NVIDIA v0.1.5") the shader cache was last filled for.</summary>
    public Dictionary<string, string> ShadersPrepared { get; set; } = new();
    public Dictionary<string, string> PipelinesPrepared { get; set; } = new();

    [JsonIgnore]
    public string? GameFolder => GameExe is null ? null : Path.GetDirectoryName(GameExe);

    public string? InstalledTag(EngineKind kind) => kind == EngineKind.Nvidia ? InstalledNvidiaTag : InstalledAmdTag;

    public void SetInstalledTag(EngineKind kind, string? tag)
    {
        if (kind == EngineKind.Nvidia) InstalledNvidiaTag = tag; else InstalledAmdTag = tag;
    }

    /// <summary>Which engine the toggle means on this PC.</summary>
    public EngineKind Kind(SystemSnapshot? system) => EngineKinds.Resolve(Engine, system);

    /// <summary>Older files carry choices that were made for an engine this launcher no longer treats as the only one.</summary>
    void Upgrade()
    {
        if (Version < 2)
        {
            // The ambient light was saved as off, but only because the old default was off for every engine.
            AmbientLight = null;
        }
        Version = CurrentVersion;
    }

    static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static Settings Load(string file)
    {
        try
        {
            if (File.Exists(file))
            {
                var loaded = JsonSerializer.Deserialize<Settings>(File.ReadAllText(file), Json) ?? new Settings();
                // A file from before the Version field reads as 2 (the new default), so look at the raw text.
                using var doc = JsonDocument.Parse(File.ReadAllText(file));
                if (!doc.RootElement.TryGetProperty(nameof(Version), out _)) loaded.Version = 1;
                loaded.Upgrade();
                return loaded;
            }
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            // A damaged file must not stop the launcher; keep it for inspection and start clean.
            try { File.Move(file, file + ".bad", overwrite: true); } catch (IOException) { }
        }
        return new Settings();
    }

    public void Save(string file)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var temp = file + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, Json));
        File.Move(temp, file, overwrite: true);
    }
}
