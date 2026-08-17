using System.Security.Cryptography;
using System.Text.Json;
using TrainingDeskCalendar.App.Domain;
using TrainingDeskCalendar.App.Persistence;
using TrainingDeskCalendar.App.Services;
using Xunit;

namespace TrainingDeskCalendar.App.Tests.Release;

public sealed class RecoveryAuditTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 18, 4, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Audit_RecordsRejectedImportsRollbackAndLogBoundary()
    {
        string root = Environment.GetEnvironmentVariable(
                "TRAINING_DESK_CALENDAR_RECOVERY_ROOT")
            ?? Path.Combine(Path.GetTempPath(), $"training-desk-recovery-{Guid.NewGuid():N}");
        string? reportPath = Environment.GetEnvironmentVariable(
            "TRAINING_DESK_CALENDAR_RECOVERY_REPORT");
        Directory.CreateDirectory(root);
        var records = new List<RecoveryAuditRecord>();

        try
        {
            await RunRejectedImportAsync(
                root,
                "corrupt-json",
                "{not-json",
                records);
            await RunRejectedImportAsync(
                root,
                "unknown-version",
                """
                {
                  "formatVersion": 2,
                  "exportedAtUtc": "2026-08-18T04:00:00+00:00",
                  "plans": [],
                  "settings": null
                }
                """,
                records);
            await RunRejectedImportAsync(
                root,
                "invalid-color",
                InvalidColorSnapshot,
                records);
            await RunSettingsCommitRollbackAsync(root, records);
            await RunDatabaseCorruptionCopyAsync(root, records);

            string[] logFiles = Directory.Exists(root)
                ? Directory.GetFiles(root, "*.log", SearchOption.AllDirectories)
                : [];
            bool logsContainPlanText = logFiles.Any(path =>
                File.ReadAllText(path).Contains("保留计划", StringComparison.Ordinal) ||
                File.ReadAllText(path).Contains("不应保留", StringComparison.Ordinal));

            var report = new
            {
                auditedAtUtc = DateTimeOffset.UtcNow,
                root,
                scenarios = records,
                logFiles,
                logsContainPlanText,
                allScenariosPassed = records.All(item => item.Passed) && !logsContainPlanText
            };
            if (!string.IsNullOrWhiteSpace(reportPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
                await File.WriteAllTextAsync(
                    reportPath,
                    JsonSerializer.Serialize(report, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    }) + Environment.NewLine);
            }

            Assert.All(records, item => Assert.True(item.Passed, item.Scenario));
            Assert.False(logsContainPlanText);
        }
        finally
        {
            if (string.IsNullOrWhiteSpace(reportPath) && Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static async Task RunRejectedImportAsync(
        string root,
        string scenario,
        string invalidJson,
        ICollection<RecoveryAuditRecord> records)
    {
        (AppDataPaths paths, SqlitePlanStore plans, SettingsStore settings) =
            await CreateStoresAsync(Path.Combine(root, scenario));
        await plans.SaveAsync(Plan(new DateOnly(2026, 8, 18), "保留计划", TaskColorId.Teal));
        await settings.SaveAsync(AppSettings.Defaults with { Opacity = 0.85 });
        string importPath = Path.Combine(paths.Root, "invalid.json");
        await File.WriteAllTextAsync(importPath, invalidJson);
        StateHashes before = await HashStateAsync(paths, plans, settings);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            new DataTransferService(plans, settings, paths, new FixedTimeProvider(Now))
                .ImportAsync(importPath));

        StateHashes after = await HashStateAsync(paths, plans, settings);
        records.Add(new RecoveryAuditRecord(
            scenario,
            "rejected-without-mutation",
            before,
            after,
            before.IsLogicalStateEqual(after)));
    }

    private static async Task RunSettingsCommitRollbackAsync(
        string root,
        ICollection<RecoveryAuditRecord> records)
    {
        string scenarioRoot = Path.Combine(root, "settings-write-failure");
        (AppDataPaths paths, SqlitePlanStore plans, SettingsStore settings) =
            await CreateStoresAsync(scenarioRoot);
        await plans.SaveAsync(Plan(new DateOnly(2026, 8, 18), "保留计划", TaskColorId.Orange));
        await settings.SaveAsync(AppSettings.Defaults with { Opacity = 0.85 });
        string importPath = Path.Combine(scenarioRoot, "valid.json");
        await File.WriteAllTextAsync(importPath, ValidSnapshot(
            Plan(new DateOnly(2026, 8, 23), "不应保留", TaskColorId.Red),
            AppSettings.Defaults with { Opacity = 0.55 }));
        int commitAttempts = 0;
        var failingSettings = new SettingsStore(
            paths.SettingsPath,
            (temporaryPath, destinationPath) =>
            {
                commitAttempts++;
                if (commitAttempts == 1)
                {
                    throw new IOException("Simulated settings commit failure.");
                }

                File.Move(temporaryPath, destinationPath, overwrite: true);
            });
        StateHashes before = await HashStateAsync(paths, plans, settings);

        await Assert.ThrowsAsync<IOException>(() =>
            new DataTransferService(plans, failingSettings, paths, new FixedTimeProvider(Now))
                .ImportAsync(importPath));

        StateHashes after = await HashStateAsync(paths, plans, failingSettings);
        records.Add(new RecoveryAuditRecord(
            "settings-write-failure-rollback",
            "rollback-restored",
            before,
            after,
            commitAttempts == 2 && before.IsLogicalStateEqual(after)));
    }

    private static async Task RunDatabaseCorruptionCopyAsync(
        string root,
        ICollection<RecoveryAuditRecord> records)
    {
        string scenarioRoot = Path.Combine(root, "database-corruption");
        (AppDataPaths paths, SqlitePlanStore plans, SettingsStore settings) =
            await CreateStoresAsync(scenarioRoot);
        await plans.SaveAsync(Plan(new DateOnly(2026, 8, 19), "完整数据库", TaskColorId.Blue));
        await settings.SaveAsync(AppSettings.Defaults);
        StateHashes before = await HashStateAsync(paths, plans, settings);
        string corruptCopy = Path.Combine(scenarioRoot, "corrupt.db");
        File.Copy(paths.DatabasePath, corruptCopy, overwrite: true);
        await File.WriteAllTextAsync(corruptCopy, "not a sqlite database");
        await Assert.ThrowsAnyAsync<Exception>(() =>
            new SqlitePlanStore(corruptCopy).GetAllAsync());
        StateHashes after = await HashStateAsync(paths, plans, settings);

        records.Add(new RecoveryAuditRecord(
            "database-corruption-isolated-copy",
            "rejected-with-source-unchanged",
            before,
            after,
            before.IsLogicalStateEqual(after)));
    }

    private static async Task<(AppDataPaths, SqlitePlanStore, SettingsStore)> CreateStoresAsync(
        string root)
    {
        AppDataPaths paths = AppDataPaths.ForRoot(root);
        var plans = new SqlitePlanStore(paths.DatabasePath);
        await plans.InitializeAsync();
        return (paths, plans, new SettingsStore(paths.SettingsPath));
    }

    private static async Task<StateHashes> HashStateAsync(
        AppDataPaths paths,
        ITrainingPlanStore plans,
        SettingsStore settings)
    {
        IReadOnlyList<TrainingPlan> planState = await plans.GetAllAsync();
        AppSettings settingsState = await settings.LoadAsync();
        return new StateHashes(
            Sha256File(paths.DatabasePath),
            Sha256File(paths.SettingsPath),
            Sha256Text(JsonSerializer.Serialize(planState)),
            Sha256Text(JsonSerializer.Serialize(settingsState)));
    }

    private static string Sha256File(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private static string Sha256Text(string text) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    private static TrainingPlan Plan(DateOnly date, string text, TaskColorId color) =>
        TrainingPlan.Create(date, text, color, updatedAtUtc: Now.AddHours(-1));

    private static string ValidSnapshot(TrainingPlan plan, AppSettings settings) => $$"""
        {
          "formatVersion": 1,
          "exportedAtUtc": "{{Now:O}}",
          "plans": [{
            "date": "{{plan.Date:yyyy-MM-dd}}",
            "text": {{JsonSerializer.Serialize(plan.Text)}},
            "colorId": {{(int)plan.Color}},
            "isCompleted": false,
            "updatedAtUtc": "{{plan.UpdatedAtUtc:O}}"
          }],
          "settings": {{JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            })}}
        }
        """;

    private const string InvalidColorSnapshot = """
        {
          "formatVersion": 1,
          "exportedAtUtc": "2026-08-18T04:00:00+00:00",
          "plans": [{
            "date": "2026-08-22",
            "text": "非法颜色",
            "colorId": 7,
            "isCompleted": false,
            "updatedAtUtc": "2026-08-18T04:00:00+00:00"
          }],
          "settings": {
            "version": 1,
            "windowX": 100,
            "windowY": 100,
            "windowWidth": 1120,
            "windowHeight": 470,
            "monitorId": "",
            "isLocked": false,
            "theme": 0,
            "opacity": 1.0,
            "startWithWindows": true,
            "lastUpdateCheckUtc": null
          }
        }
        """;

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed record StateHashes(
        string DatabaseSha256,
        string SettingsSha256,
        string PlansContentSha256,
        string SettingsContentSha256)
    {
        public bool IsLogicalStateEqual(StateHashes other) =>
            PlansContentSha256 == other.PlansContentSha256 &&
            SettingsContentSha256 == other.SettingsContentSha256;
    }

    private sealed record RecoveryAuditRecord(
        string Scenario,
        string Outcome,
        StateHashes Before,
        StateHashes After,
        bool Passed);
}
