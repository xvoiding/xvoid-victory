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
    public bool SetupDone { get; set; }
    /// <summary>The title's executable, in the folder the engine reads its files from.</summary>
    public string? GameExe { get; set; }
    public EngineSource EngineSource { get; set; } = EngineSource.Release;
    public string? LocalEnginePath { get; set; }
    public string? InstalledEngineTag { get; set; }
    public Preset Preset { get; set; } = Preset.Auto;
    /// <summary>series-x or series-s.</summary>
    public string Console { get; set; } = "series-x";
    /// <summary>0: no limit.</summary>
    public int TextureBudgetMb { get; set; }
    /// <summary>0: chosen from the number of processors.</summary>
    public int ListThreads { get; set; }
    public bool RayTracing { get; set; }
    /// <summary>The stand-in for ray-traced indirect light. Off: it blew the picture out on an RTX 4060 Laptop.</summary>
    public bool AmbientLight { get; set; }
    public bool RecordPipelines { get; set; } = true;
    public Dictionary<string, string> ExtraEnvironment { get; set; } = new();
    public HostProfile Host { get; set; } = new();
    /// <summary>Engine tag the shader cache was last filled for.</summary>
    public string? ShadersPreparedFor { get; set; }
    public string? PipelinesPreparedFor { get; set; }

    [JsonIgnore]
    public string? GameFolder => GameExe is null ? null : Path.GetDirectoryName(GameExe);

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
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(file), Json) ?? new Settings();
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
