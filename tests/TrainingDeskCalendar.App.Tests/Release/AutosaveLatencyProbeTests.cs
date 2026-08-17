using System.Diagnostics;
using System.Text.Json;
using TrainingDeskCalendar.App.Diagnostics;
using Xunit;

namespace TrainingDeskCalendar.App.Tests.Release;

public sealed class AutosaveLatencyProbeTests
{
    [Fact]
    public async Task MeasureAsync_RecordsEverySaveAndPreservesOrder()
    {
        var calls = new List<int>();

        IReadOnlyList<AutosaveLatencyMeasurement> measurements =
            await AutosaveLatencyProbe.MeasureAsync(
                3,
                async sample =>
                {
                    calls.Add(sample);
                    await Task.Yield();
                });

        Assert.Equal([1, 2, 3], calls);
        Assert.Equal([1, 2, 3], measurements.Select(item => item.Sample));
        Assert.All(measurements, item => Assert.True(item.ElapsedMilliseconds >= 0));
    }

    [Fact]
    public async Task WriteAsync_EmitsMachineReadableSamples()
    {
        string path = Path.Combine(Path.GetTempPath(), $"autosave-{Guid.NewGuid():N}.json");
        try
        {
            await AutosaveLatencyProbe.WriteAsync(
                path,
                2,
                _ => Task.CompletedTask);

            using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(path));
            Assert.Equal(2, document.RootElement.GetProperty("sampleCount").GetInt32());
            Assert.Equal(2, document.RootElement.GetProperty("samples").GetArrayLength());
        }
        finally
        {
            File.Delete(path);
        }
    }
}
