using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using TrainingDeskCalendar.App.Domain;
using TrainingDeskCalendar.App.Services;

namespace TrainingDeskCalendar.App.Persistence;

internal sealed class SqlitePlanStore(string databasePath) : ITrainingPlanStore
{
    private const int CurrentSchemaVersion = 1;
    private const string DateFormat = "yyyy-MM-dd";
    private readonly string databasePath = Path.GetFullPath(
        string.IsNullOrWhiteSpace(databasePath)
            ? throw new ArgumentException("A database path is required.", nameof(databasePath))
            : databasePath);

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        await using SqliteConnection connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using SqliteTransaction transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await using (SqliteCommand command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS schema_info (
                    version INTEGER NOT NULL
                );
                INSERT INTO schema_info(version)
                SELECT 1
                WHERE NOT EXISTS (SELECT 1 FROM schema_info);
                CREATE TABLE IF NOT EXISTS plans (
                    date TEXT NOT NULL PRIMARY KEY,
                    text TEXT NOT NULL,
                    color_id INTEGER NOT NULL CHECK(color_id BETWEEN 1 AND 6),
                    is_completed INTEGER NOT NULL CHECK(is_completed IN (0, 1)),
                    updated_at_utc TEXT NOT NULL
                );
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        int version;
        await using (SqliteCommand command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "SELECT version FROM schema_info LIMIT 1;";
            object? value = await command.ExecuteScalarAsync(cancellationToken);
            version = Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        if (version > CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"Database schema version {version} is newer than supported version {CurrentSchemaVersion}.");
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<TrainingPlan?> GetAsync(
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT date, text, color_id, is_completed, updated_at_utc
            FROM plans
            WHERE date = $date;
            """;
        command.Parameters.AddWithValue("$date", FormatDate(date));
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadPlan(reader) : null;
    }

    public async Task<IReadOnlyList<TrainingPlan>> GetRangeAsync(
        DateOnly start,
        DateOnly end,
        CancellationToken cancellationToken = default)
    {
        if (end < start)
        {
            throw new ArgumentOutOfRangeException(nameof(end));
        }

        await using SqliteConnection connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT date, text, color_id, is_completed, updated_at_utc
            FROM plans
            WHERE date BETWEEN $start AND $end
            ORDER BY date;
            """;
        command.Parameters.AddWithValue("$start", FormatDate(start));
        command.Parameters.AddWithValue("$end", FormatDate(end));
        return await ReadPlansAsync(command, cancellationToken);
    }

    public async Task SaveAsync(
        TrainingPlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        await using SqliteConnection connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await SaveCoreAsync(connection, transaction: null, plan, cancellationToken);
    }

    public async Task SaveManyAsync(
        IReadOnlyCollection<TrainingPlan> plans,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plans);
        await using SqliteConnection connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using SqliteTransaction transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (TrainingPlan plan in plans)
            {
                await SaveCoreAsync(connection, transaction, plan, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task DeleteAsync(
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await DeleteCoreAsync(connection, transaction: null, date, cancellationToken);
    }

    public async Task<IReadOnlyList<TrainingPlan>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT date, text, color_id, is_completed, updated_at_utc
            FROM plans
            ORDER BY date;
            """;
        return await ReadPlansAsync(command, cancellationToken);
    }

    public async Task ReplaceAllAsync(
        IReadOnlyCollection<TrainingPlan> plans,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plans);
        await using SqliteConnection connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using SqliteTransaction transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using (SqliteCommand delete = connection.CreateCommand())
            {
                delete.Transaction = transaction;
                delete.CommandText = "DELETE FROM plans;";
                await delete.ExecuteNonQueryAsync(cancellationToken);
            }

            foreach (TrainingPlan plan in plans)
            {
                await SaveCoreAsync(connection, transaction, plan, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private SqliteConnection CreateConnection() => new(
        new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString());

    private static async Task SaveCoreAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        TrainingPlan plan,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.IsDefaultEmpty)
        {
            await DeleteCoreAsync(connection, transaction, plan.Date, cancellationToken);
            return;
        }

        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO plans(date, text, color_id, is_completed, updated_at_utc)
            VALUES($date, $text, $colorId, $isCompleted, $updatedAtUtc)
            ON CONFLICT(date) DO UPDATE SET
                text = excluded.text,
                color_id = excluded.color_id,
                is_completed = excluded.is_completed,
                updated_at_utc = excluded.updated_at_utc;
            """;
        command.Parameters.AddWithValue("$date", FormatDate(plan.Date));
        command.Parameters.AddWithValue("$text", plan.Text);
        command.Parameters.AddWithValue("$colorId", (int)plan.Color);
        command.Parameters.AddWithValue("$isCompleted", plan.IsCompleted ? 1 : 0);
        command.Parameters.AddWithValue("$updatedAtUtc", plan.UpdatedAtUtc.ToUniversalTime().ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task DeleteCoreAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM plans WHERE date = $date;";
        command.Parameters.AddWithValue("$date", FormatDate(date));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<TrainingPlan>> ReadPlansAsync(
        SqliteCommand command,
        CancellationToken cancellationToken)
    {
        var plans = new List<TrainingPlan>();
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            plans.Add(ReadPlan(reader));
        }

        return plans;
    }

    private static TrainingPlan ReadPlan(SqliteDataReader reader)
    {
        if (!DateOnly.TryParseExact(
                reader.GetString(0),
                DateFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateOnly date))
        {
            throw new InvalidDataException("Stored plan date is invalid.");
        }

        var color = (TaskColorId)reader.GetInt32(2);
        if (!Enum.IsDefined(color))
        {
            throw new InvalidDataException("Stored plan color is invalid.");
        }

        int completed = reader.GetInt32(3);
        if (completed is not 0 and not 1)
        {
            throw new InvalidDataException("Stored completion state is invalid.");
        }

        if (!DateTimeOffset.TryParse(
                reader.GetString(4),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out DateTimeOffset updatedAtUtc))
        {
            throw new InvalidDataException("Stored update timestamp is invalid.");
        }

        return TrainingPlan.Create(
            date,
            reader.GetString(1),
            color,
            completed == 1,
            updatedAtUtc);
    }

    private static string FormatDate(DateOnly date) =>
        date.ToString(DateFormat, CultureInfo.InvariantCulture);
}
