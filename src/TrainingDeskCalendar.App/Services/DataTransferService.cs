using System.Globalization;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TrainingDeskCalendar.App.Domain;
using TrainingDeskCalendar.App.Persistence;

namespace TrainingDeskCalendar.App.Services;

internal sealed class DataTransferService
{
    private const int CurrentFormatVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    private readonly ITrainingPlanStore planStore;
    private readonly SettingsStore settingsStore;
    private readonly AppDataPaths paths;
    private readonly TimeProvider timeProvider;

    public DataTransferService(
        ITrainingPlanStore planStore,
        SettingsStore settingsStore,
        AppDataPaths paths,
        TimeProvider? timeProvider = null)
    {
        this.planStore = planStore ?? throw new ArgumentNullException(nameof(planStore));
        this.settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        this.paths = paths ?? throw new ArgumentNullException(nameof(paths));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task ExportAsync(
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        IReadOnlyList<TrainingPlan> plans = await planStore.GetAllAsync(cancellationToken);
        AppSettings settings = await settingsStore.LoadAsync(cancellationToken);
        var snapshot = new SnapshotFormat(
            CurrentFormatVersion,
            timeProvider.GetUtcNow().ToUniversalTime(),
            plans,
            settings);

        await WriteSnapshotAsync(destinationPath, snapshot, cancellationToken);
    }

    public async Task ImportAsync(
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        SnapshotFormat imported = await ReadSnapshotAsync(sourcePath, cancellationToken);

        IReadOnlyList<TrainingPlan> originalPlans =
            await planStore.GetAllAsync(cancellationToken);
        AppSettings originalSettings = await settingsStore.LoadAsync(cancellationToken);
        var original = new SnapshotFormat(
            CurrentFormatVersion,
            timeProvider.GetUtcNow().ToUniversalTime(),
            originalPlans,
            originalSettings);
        await CreateRecoveryBackupAsync(original, cancellationToken);

        try
        {
            await planStore.ReplaceAllAsync(imported.Plans, cancellationToken);
            await settingsStore.SaveAsync(imported.Settings, cancellationToken);
        }
        catch (Exception importException)
        {
            try
            {
                await planStore.ReplaceAllAsync(originalPlans, CancellationToken.None);
                await settingsStore.SaveAsync(originalSettings, CancellationToken.None);
            }
            catch (Exception rollbackException)
            {
                throw new AggregateException(
                    "Import failed and the recovery rollback also failed.",
                    importException,
                    rollbackException);
            }

            ExceptionDispatchInfo.Capture(importException).Throw();
            throw;
        }
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
        options.Converters.Add(new TrainingPlanJsonConverter());
        return options;
    }

    private static async Task<SnapshotFormat> ReadSnapshotAsync(
        string sourcePath,
        CancellationToken cancellationToken)
    {
        string fullPath = Path.GetFullPath(sourcePath);
        string json = await File.ReadAllTextAsync(fullPath, cancellationToken);
        SnapshotFormat snapshot;
        try
        {
            snapshot = JsonSerializer.Deserialize<SnapshotFormat>(json, SerializerOptions)
                ?? throw new InvalidDataException("Snapshot JSON is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Snapshot JSON is invalid.", exception);
        }

        if (snapshot.FormatVersion != CurrentFormatVersion ||
            snapshot.ExportedAtUtc.Offset != TimeSpan.Zero ||
            snapshot.Plans is null ||
            snapshot.Settings is null)
        {
            throw new InvalidDataException("Snapshot header is invalid or unsupported.");
        }

        snapshot.Settings.Validate();
        if (snapshot.Plans.Any(plan => plan is null || plan.IsDefaultEmpty) ||
            snapshot.Plans.Select(plan => plan.Date).Distinct().Count() != snapshot.Plans.Count)
        {
            throw new InvalidDataException("Snapshot plans are invalid.");
        }

        return snapshot;
    }

    private async Task CreateRecoveryBackupAsync(
        SnapshotFormat snapshot,
        CancellationToken cancellationToken)
    {
        EnsureBackupDirectoryIsUnderAppDataRoot();
        Directory.CreateDirectory(paths.BackupDirectory);
        string timestamp = timeProvider.GetUtcNow().ToUniversalTime()
            .ToString("yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture);
        string backupPath = Path.Combine(
            paths.BackupDirectory,
            $"backup-{timestamp}-{Guid.NewGuid():N}.json");

        await WriteSnapshotAsync(backupPath, snapshot, cancellationToken);
        DeleteOldRecoveryBackups();
    }

    private static async Task WriteSnapshotAsync(
        string destinationPath,
        SnapshotFormat snapshot,
        CancellationToken cancellationToken)
    {
        string fullPath = Path.GetFullPath(destinationPath);
        string directory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("Snapshot path has no parent directory.");
        Directory.CreateDirectory(directory);
        string temporaryPath = Path.Combine(
            directory,
            $"snapshot.{Guid.NewGuid():N}.tmp");
        string json = JsonSerializer.Serialize(snapshot, SerializerOptions) + Environment.NewLine;

        try
        {
            await File.WriteAllTextAsync(
                temporaryPath,
                json,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken);
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private void EnsureBackupDirectoryIsUnderAppDataRoot()
    {
        string relative = Path.GetRelativePath(
            Path.GetFullPath(paths.Root),
            Path.GetFullPath(paths.BackupDirectory));
        if (Path.IsPathRooted(relative) ||
            relative == ".." ||
            relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The backup directory must be inside the application data root.");
        }
    }

    private void DeleteOldRecoveryBackups()
    {
        string[] obsoleteFiles = Directory
            .EnumerateFiles(paths.BackupDirectory, "backup-*.json", SearchOption.TopDirectoryOnly)
            .OrderByDescending(Path.GetFileName, StringComparer.Ordinal)
            .Skip(5)
            .ToArray();

        foreach (string obsoleteFile in obsoleteFiles)
        {
            File.Delete(obsoleteFile);
        }
    }

    private sealed class TrainingPlanJsonConverter : JsonConverter<TrainingPlan>
    {
        public override TrainingPlan Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            try
            {
                using JsonDocument document = JsonDocument.ParseValue(ref reader);
                JsonElement plan = document.RootElement;
                string dateText = plan.GetProperty("date").GetString()
                    ?? throw new JsonException("Plan date is required.");
                string text = plan.GetProperty("text").GetString()
                    ?? throw new JsonException("Plan text is required.");
                int colorId = plan.GetProperty("colorId").GetInt32();
                bool isCompleted = plan.GetProperty("isCompleted").GetBoolean();
                string updatedText = plan.GetProperty("updatedAtUtc").GetString()
                    ?? throw new JsonException("Plan update timestamp is required.");

                if (!DateOnly.TryParseExact(
                        dateText,
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out DateOnly date) ||
                    !DateTimeOffset.TryParseExact(
                        updatedText,
                        "O",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind,
                        out DateTimeOffset updatedAtUtc) ||
                    updatedAtUtc.Offset != TimeSpan.Zero)
                {
                    throw new JsonException("Plan date or timestamp is invalid.");
                }

                return TrainingPlan.Create(
                    date,
                    text,
                    (TaskColorId)colorId,
                    isCompleted,
                    updatedAtUtc);
            }
            catch (Exception exception) when (
                exception is ArgumentException or InvalidOperationException or KeyNotFoundException)
            {
                throw new JsonException("Plan data is invalid.", exception);
            }
        }

        public override void Write(
            Utf8JsonWriter writer,
            TrainingPlan value,
            JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString(
                "date",
                value.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            writer.WriteString("text", value.Text);
            writer.WriteNumber("colorId", (int)value.Color);
            writer.WriteBoolean("isCompleted", value.IsCompleted);
            writer.WriteString("updatedAtUtc", value.UpdatedAtUtc.ToUniversalTime());
            writer.WriteEndObject();
        }
    }
}
