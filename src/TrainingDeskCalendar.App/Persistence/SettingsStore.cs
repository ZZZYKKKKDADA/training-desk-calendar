using System.IO;
using System.Text;
using System.Text.Json;

namespace TrainingDeskCalendar.App.Persistence;

internal delegate void SettingsFileCommit(string temporaryPath, string destinationPath);

internal sealed class SettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string settingsPath;
    private readonly SettingsFileCommit commit;
    private readonly TimeProvider timeProvider;

    public SettingsStore(
        string settingsPath,
        SettingsFileCommit? commit = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsPath);
        this.settingsPath = Path.GetFullPath(settingsPath);
        this.commit = commit ?? CommitFile;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(settingsPath))
        {
            return AppSettings.Defaults;
        }

        try
        {
            string json = await File.ReadAllTextAsync(settingsPath, cancellationToken);
            AppSettings settings = JsonSerializer.Deserialize<AppSettings>(
                    json,
                    SerializerOptions)
                ?? throw new InvalidDataException("Settings JSON is empty.");
            return settings.Validate();
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidDataException or NotSupportedException)
        {
            PreserveCorruptFile();
            return AppSettings.Defaults;
        }
    }

    public async Task SaveAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();

        string directory = Path.GetDirectoryName(settingsPath)
            ?? throw new InvalidOperationException("Settings path has no parent directory.");
        Directory.CreateDirectory(directory);

        string temporaryPath = Path.Combine(
            directory,
            $"settings.{Guid.NewGuid():N}.tmp");
        string json = JsonSerializer.Serialize(settings, SerializerOptions) + Environment.NewLine;

        try
        {
            await File.WriteAllTextAsync(
                temporaryPath,
                json,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken);
            commit(temporaryPath, settingsPath);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static void CommitFile(string temporaryPath, string destinationPath) =>
        File.Move(temporaryPath, destinationPath, overwrite: true);

    private void PreserveCorruptFile()
    {
        string directory = Path.GetDirectoryName(settingsPath)
            ?? throw new InvalidOperationException("Settings path has no parent directory.");
        string timestamp = timeProvider.GetUtcNow().ToString("yyyyMMddHHmmssfff");
        string corruptPath = Path.Combine(
            directory,
            $"settings.corrupt-{timestamp}-{Guid.NewGuid():N}.json");
        File.Move(settingsPath, corruptPath);
    }
}
