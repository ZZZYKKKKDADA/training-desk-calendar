using TrainingDeskCalendar.App.Diagnostics;
using Xunit;

namespace TrainingDeskCalendar.App.Tests.Release;

public sealed class DiagnosticRunOptionsTests
{
    [Fact]
    public void Parse_RecognizesIsolatedDataRootAndAutosaveProbe()
    {
        DiagnosticRunOptions options = DiagnosticRunOptions.Parse([
            "--data-root", "C:\\temp\\training-desk",
            "--save-latency-file", "C:\\temp\\latency.json",
            "--save-latency-samples", "10",
            "--ready-file", "C:\\temp\\ready.txt",
            "--exit-after-seconds", "75"]);

        Assert.Equal("C:\\temp\\training-desk", options.DataRoot);
        Assert.Equal("C:\\temp\\latency.json", options.SaveLatencyFile);
        Assert.Equal(10, options.SaveLatencySamples);
        Assert.Equal("C:\\temp\\ready.txt", options.ReadyFile);
        Assert.Equal(TimeSpan.FromSeconds(75), options.ExitAfter);
    }
}
