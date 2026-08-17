using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace TrainingDeskCalendar.App.Diagnostics;

internal sealed record AutosaveLatencyMeasurement(
    int Sample,
    double ElapsedMilliseconds);

internal static class AutosaveLatencyProbe
{
    public static async Task<IReadOnlyList<AutosaveLatencyMeasurement>> MeasureAsync(
        int sampleCount,
        Func<int, Task> saveAsync)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleCount);
        ArgumentNullException.ThrowIfNull(saveAsync);

        var measurements = new List<AutosaveLatencyMeasurement>(sampleCount);
        for (int sample = 1; sample <= sampleCount; sample++)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            await saveAsync(sample);
            stopwatch.Stop();
            measurements.Add(new AutosaveLatencyMeasurement(
                sample,
                Math.Round(stopwatch.Elapsed.TotalMilliseconds, 1)));
        }

        return measurements;
    }

    public static async Task WriteAsync(
        string outputPath,
        int sampleCount,
        Func<int, Task> saveAsync)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        IReadOnlyList<AutosaveLatencyMeasurement> measurements =
            await MeasureAsync(sampleCount, saveAsync);

        string fullPath = Path.GetFullPath(outputPath);
        string directory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("Autosave probe path has no parent directory.");
        Directory.CreateDirectory(directory);
        var payload = new
        {
            measuredAtUtc = DateTimeOffset.UtcNow,
            sampleCount = measurements.Count,
            samples = measurements
        };
        await File.WriteAllTextAsync(
            fullPath,
            JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }) +
            Environment.NewLine);
    }
}
