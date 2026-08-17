namespace TrainingDeskCalendar.App.Diagnostics;

internal sealed record DiagnosticRunOptions(
    string? DataRoot,
    string? ReadyFile,
    TimeSpan? ExitAfter,
    string? SaveLatencyFile,
    int SaveLatencySamples)
{
    public static DiagnosticRunOptions Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        string? dataRoot = null;
        string? readyFile = null;
        TimeSpan? exitAfter = null;
        string? saveLatencyFile = null;
        int saveLatencySamples = 0;

        for (int index = 0; index < args.Count; index++)
        {
            string argument = args[index];
            if (argument == "--data-root" && TryRead(args, ref index, out string? root))
            {
                dataRoot = root;
            }
            else if (argument == "--ready-file" && TryRead(args, ref index, out string? ready))
            {
                readyFile = ready;
            }
            else if (argument == "--exit-after-seconds" &&
                     TryRead(args, ref index, out string? secondsText) &&
                     int.TryParse(secondsText, out int seconds) &&
                     seconds > 0)
            {
                exitAfter = TimeSpan.FromSeconds(seconds);
            }
            else if (argument == "--save-latency-file" &&
                     TryRead(args, ref index, out string? latencyFile))
            {
                saveLatencyFile = latencyFile;
            }
            else if (argument == "--save-latency-samples" &&
                     TryRead(args, ref index, out string? samplesText) &&
                     int.TryParse(samplesText, out int samples) &&
                     samples > 0)
            {
                saveLatencySamples = samples;
            }
        }

        return new DiagnosticRunOptions(
            dataRoot,
            readyFile,
            exitAfter,
            saveLatencyFile,
            saveLatencySamples);
    }

    private static bool TryRead(
        IReadOnlyList<string> args,
        ref int index,
        out string? value)
    {
        if (index + 1 >= args.Count || string.IsNullOrWhiteSpace(args[index + 1]))
        {
            value = null;
            return false;
        }

        value = args[++index];
        return true;
    }
}
