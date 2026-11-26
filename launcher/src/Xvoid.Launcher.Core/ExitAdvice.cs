namespace Xvoid.Launcher.Core;

/// <summary>What an exit code and the last log lines most likely mean, in words for a person.</summary>
public static class ExitAdvice
{
    public static string? Explain(int exitCode, bool allocationFailed, bool sawPipelineRefusals)
    {
        if (exitCode == 0) return null;
        // Rust aborts with STATUS_STACK_BUFFER_OVERRUN (0xC0000409) when an allocation fails.
        if (allocationFailed || exitCode == unchecked((int)0xC0000409))
            return allocationFailed
                ? "The engine ran out of memory. Windows had no commit left: set a larger paging file (Setup, Paging file), close other programs, and try the Low memory preset."
                : "The engine aborted. If the log ends with a memory allocation failure, the paging file is too small.";
        return unchecked((uint)exitCode) switch
        {
            0xC0000005 => "The engine crashed (access violation). Try the Safe preset; if it still happens, create a report and send it.",
            0x887A0005 or 0x887A0006 or 0x887A0007 => "The graphics driver reset (device removed). Update the GPU driver; the Safe preset can avoid the faster, riskier paths.",
            0xC000013A => "The engine was stopped (Ctrl+C or the console closed).",
            0xFFFFFFFF => sawPipelineRefusals
                ? "The engine stopped with an error. Some GPU pipelines were refused earlier in the log; that is a known limit on 32-lane GPUs."
                : "The engine stopped with an error. Open the log for the last lines.",
            _ => $"The engine ended with code {exitCode} (0x{unchecked((uint)exitCode):X8}). Open the log, or create a report.",
        };
    }
}
