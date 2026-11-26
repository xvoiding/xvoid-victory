using Xvoid.Launcher.Core;
using Xunit;

namespace Xvoid.Launcher.Core.Tests;

public class LogParserTests
{
    // Lines copied from a run on an RTX 4060 Laptop, colours stripped by Clean().
    const string FrameLine = "2026-11-26T00:24:49.063874Z  INFO victory_gpu::host: 5.8 fps, 172.2 ms/frame (89.5 preparing: copies 41.4 ms";

    [Fact]
    public void Frame_line_gives_fps_and_ms()
    {
        var f = LogParser.ParseFrame(FrameLine);
        Assert.NotNull(f);
        Assert.Equal(5.8, f!.Fps);
        Assert.Equal(172.2, f.Ms);
        Assert.Null(f.Draws);
    }

    [Fact]
    public void Draw_count_can_come_on_the_wrapped_line()
    {
        Assert.Equal(408, LogParser.ParseDrawsOnly("MB, textures 6.4 ms 90.2 MB; 0.0 on the GPU; 29.9 copying back 26.1 MB), 408 draws/frame, dropped 1326"));
    }

    [Fact]
    public void Title_bar_is_parsed()
    {
        var f = LogParser.ParseTitle("Americas | VIctory 5 fps 197.9 ms/frame 3668 draws/frame");
        Assert.Equal(new FrameStat(5, 197.9, 3668), f);
        Assert.Null(LogParser.ParseTitle("Americas"));
        Assert.Null(LogParser.ParseTitle(null));
    }

    [Fact]
    public void Memory_line_gives_private_and_commit_left()
    {
        var m = LogParser.ParseMemory("memory: 22820 MB private (14949 MB of it in memory; the PC has 406 MB free and 666 MB left to commit); host copies");
        Assert.NotNull(m);
        Assert.Equal(22820 / 1024.0, m!.PrivateGb, 3);
        Assert.Equal(666 / 1024.0, m.CommitLeftGb!.Value, 3);
    }

    [Fact]
    public void Memory_line_without_the_commit_half_still_reports_private()
    {
        var m = LogParser.ParseMemory("memory: 22820 MB private (14949 MB of it in memory; the PC");
        Assert.NotNull(m);
        Assert.Null(m!.CommitLeftGb);
    }

    [Fact]
    public void Host_profile_lines()
    {
        Assert.Equal((32, 32), LogParser.ParseWave("INFO victory_gpu::host: host wave lanes min=32 max=32"));
        Assert.Equal(1_000_000L, LogParser.ParseViewLimit("host view heaps hold 1000000 views; view indices are"));
    }

    [Fact]
    public void Warm_progress()
    {
        Assert.Equal(new WarmProgress(1000, 1584, 226), LogParser.ParseWarm("1000 of 1584, 226 s"));
        Assert.Null(LogParser.ParseWarm("pipeline 3 abc: failed"));
    }

    [Fact]
    public void Allocation_failure_and_refusals()
    {
        Assert.True(LogParser.IsAllocationFailure("memory allocation of 1048576 bytes failed"));
        Assert.False(LogParser.IsAllocationFailure("allocated 3 buffers"));
        Assert.True(LogParser.IsPipelineRefused("WARN victory_gpu::pipeline::host: host pipeline refused"));
    }

    [Fact]
    public void Ansi_colours_are_removed()
    {
        Assert.Equal("INFO host ready", LogParser.Clean("\u001b[32m INFO\u001b[0m host ready".Replace(" INFO", "INFO")));
    }
}
