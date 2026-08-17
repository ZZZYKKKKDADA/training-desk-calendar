using Microsoft.Data.Sqlite;
using TrainingDeskCalendar.App.Domain;
using TrainingDeskCalendar.App.Persistence;
using Xunit;

namespace TrainingDeskCalendar.App.Tests.Persistence;

public sealed class SqlitePlanStoreTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        "training-desk-calendar-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task InitializeAndSave_RoundTripsPlansInDateOrder()
    {
        SqlitePlanStore store = CreateStore();
        await store.InitializeAsync();
        await store.SaveAsync(Plan(new DateOnly(2026, 8, 20), "背部训练", TaskColorId.Blue));
        await store.SaveAsync(Plan(new DateOnly(2026, 8, 18), "胸部训练", TaskColorId.Teal));

        IReadOnlyList<TrainingPlan> result = await store.GetRangeAsync(
            new DateOnly(2026, 8, 17),
            new DateOnly(2026, 8, 30));

        Assert.Equal(2, result.Count);
        Assert.Equal(new DateOnly(2026, 8, 18), result[0].Date);
        Assert.Equal("背部训练", result[1].Text);
        Assert.All(result, plan => Assert.Equal(TimeSpan.Zero, plan.UpdatedAtUtc.Offset));
    }

    [Fact]
    public async Task Save_DefaultEmptyPlanDeletesExistingRecord()
    {
        SqlitePlanStore store = CreateStore();
        await store.InitializeAsync();
        DateOnly date = new(2026, 8, 19);
        await store.SaveAsync(Plan(date, "慢跑", TaskColorId.Orange));

        await store.SaveAsync(TrainingPlan.Create(date, string.Empty));

        Assert.Null(await store.GetAsync(date));
    }

    [Fact]
    public async Task SaveMany_RollsBackWhenAnyPlanViolatesSchema()
    {
        SqlitePlanStore store = CreateStore();
        await store.InitializeAsync();
        TrainingPlan valid = Plan(new DateOnly(2026, 8, 19), "慢跑", TaskColorId.Orange);
        var invalid = new TrainingPlan(
            new DateOnly(2026, 8, 20),
            "非法颜色",
            (TaskColorId)7,
            false,
            DateTimeOffset.UtcNow);

        await Assert.ThrowsAnyAsync<Exception>(() => store.SaveManyAsync([valid, invalid]));

        Assert.Empty(await store.GetAllAsync());
    }

    [Fact]
    public async Task ReplaceAll_RemovesRecordsNotPresentInReplacement()
    {
        SqlitePlanStore store = CreateStore();
        await store.InitializeAsync();
        await store.SaveManyAsync([
            Plan(new DateOnly(2026, 8, 18), "计划 A", TaskColorId.Teal),
            Plan(new DateOnly(2026, 8, 19), "计划 B", TaskColorId.Blue)
        ]);

        await store.ReplaceAllAsync([
            Plan(new DateOnly(2026, 8, 20), "计划 C", TaskColorId.Purple)
        ]);

        IReadOnlyList<TrainingPlan> result = await store.GetAllAsync();
        TrainingPlan plan = Assert.Single(result);
        Assert.Equal(new DateOnly(2026, 8, 20), plan.Date);
    }

    [Fact]
    public async Task Initialize_RejectsANewerSchemaVersion()
    {
        SqlitePlanStore store = CreateStore();
        await store.InitializeAsync();
        string connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Pooling = false
        }.ToString();
        await using (var connection = new SqliteConnection(connectionString))
        {
            await connection.OpenAsync();
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "UPDATE schema_info SET version = 2;";
            await command.ExecuteNonQueryAsync();
        }

        await Assert.ThrowsAsync<InvalidDataException>(() => store.InitializeAsync());
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private SqlitePlanStore CreateStore() =>
        new(DatabasePath);

    private string DatabasePath => Path.Combine(root, "training-desk-calendar.db");

    private static TrainingPlan Plan(DateOnly date, string text, TaskColorId color) =>
        TrainingPlan.Create(
            date,
            text,
            color,
            updatedAtUtc: new DateTimeOffset(2026, 8, 17, 0, 0, 0, TimeSpan.Zero));
}
