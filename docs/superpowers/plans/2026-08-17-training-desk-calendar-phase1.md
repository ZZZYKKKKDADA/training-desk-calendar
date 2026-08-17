# 训练桌历阶段 1：核心领域与本地数据实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**目标：** 在不依赖 WPF 窗口的前提下，实现两周日期领域、训练计划记录、SQLite 持久化、设置、自动保存、复制、导入导出和恢复副本。

**架构：** 领域层只使用 `DateOnly`、不可变记录和纯函数；应用服务负责校验、复制冲突和自动保存；存储层通过接口隔离 SQLite 与 JSON 文件。所有事务、覆盖和恢复规则都由 xUnit 测试验证，Phase 2 的 WPF 界面只能调用应用服务，不能直接读写数据库或注册表。

**技术栈：** C# 14、.NET 10、`Microsoft.Data.Sqlite` 10.0.0、`System.Text.Json`、WPF 现有解决方案、xUnit。

---

## 范围与约定

- 本计划只处理当前用户本地数据，不实现托盘、单实例、自启动、安装器、更新检查或正式编辑界面。
- 所有日期使用本地 `DateOnly`，数据库使用 `yyyy-MM-dd` ISO 文本；时间戳使用 UTC `DateTimeOffset`。
- 默认任务色为灰色 `TaskColorId.Gray`。空文本、未完成、灰色默认色的记录从数据库删除。
- 复制空源日期不清除目标；只有存在计划记录的源日期才参与复制和冲突确认。
- 设置文件损坏时保留损坏文件副本并返回默认设置；导入文件损坏、版本未知或字段非法时拒绝导入且不改动现有数据。
- 导入前创建恢复副本；导入数据库和设置任一环节失败时，使用恢复副本回滚两者。
- 180 MB 工作集门槛和跨版本 Windows 手工矩阵属于阶段 0/3 验收，不在本计划中重复实现。

## 目录结构

```text
src/TrainingDeskCalendar.App/
  Domain/
    TaskColorId.cs
    TrainingPlan.cs
    TwoWeekRange.cs
    CalendarRangeService.cs
  Application/
    ITrainingPlanStore.cs
    TrainingPlanService.cs
    PlanAutosaveCoordinator.cs
    CopyPlanResult.cs
    DataTransferService.cs
  Persistence/
    AppDataPaths.cs
    SqlitePlanStore.cs
    SettingsStore.cs
    AppSettings.cs
    SnapshotFormat.cs
tests/TrainingDeskCalendar.App.Tests/
  Domain/CalendarRangeServiceTests.cs
  Domain/TrainingPlanTests.cs
  Persistence/SqlitePlanStoreTests.cs
  Persistence/SettingsStoreTests.cs
  Application/TrainingPlanServiceTests.cs
  Application/PlanAutosaveCoordinatorTests.cs
  Application/DataTransferServiceTests.cs
```

## Task 1：建立领域值对象与两周日期规则

**文件：**

- 创建 `src/TrainingDeskCalendar.App/Domain/TaskColorId.cs`
- 创建 `src/TrainingDeskCalendar.App/Domain/TrainingPlan.cs`
- 创建 `src/TrainingDeskCalendar.App/Domain/TwoWeekRange.cs`
- 创建 `src/TrainingDeskCalendar.App/Domain/CalendarRangeService.cs`
- 创建 `tests/TrainingDeskCalendar.App.Tests/Domain/CalendarRangeServiceTests.cs`
- 创建 `tests/TrainingDeskCalendar.App.Tests/Domain/TrainingPlanTests.cs`

- [ ] **步骤 1：先写日期范围失败测试**

```csharp
using TrainingDeskCalendar.App.Domain;
using Xunit;

namespace TrainingDeskCalendar.App.Tests.Domain;

public sealed class CalendarRangeServiceTests
{
    [Fact]
    public void Containing_ReturnsMondayThroughSundayForTwoWeeks()
    {
        var service = new CalendarRangeService();

        TwoWeekRange result = service.Containing(new DateOnly(2026, 8, 19));

        Assert.Equal(new DateOnly(2026, 8, 17), result.Start);
        Assert.Equal(new DateOnly(2026, 8, 30), result.End);
        Assert.Equal(14, result.Days.Count);
        Assert.Equal(DayOfWeek.Monday, result.Start.DayOfWeek);
        Assert.Equal(DayOfWeek.Sunday, result.End.DayOfWeek);
    }

    [Fact]
    public void Containing_HandlesSundayAndYearBoundary()
    {
        var service = new CalendarRangeService();

        TwoWeekRange result = service.Containing(new DateOnly(2027, 1, 3));

        Assert.Equal(new DateOnly(2026, 12, 28), result.Start);
        Assert.Equal(new DateOnly(2027, 1, 10), result.End);
    }

    [Fact]
    public void Move_AdvancesByExactlyFourteenDaysPerPage()
    {
        var service = new CalendarRangeService();
        var current = service.Containing(new DateOnly(2026, 8, 19));

        TwoWeekRange result = service.Move(current, 2);

        Assert.Equal(new DateOnly(2026, 9, 14), result.Start);
        Assert.Equal(new DateOnly(2026, 9, 27), result.End);
    }
}
```

- [ ] **步骤 2：运行测试确认失败**

运行：

```powershell
dotnet test tests/TrainingDeskCalendar.App.Tests/TrainingDeskCalendar.App.Tests.csproj --filter CalendarRangeServiceTests
```

预期：因 `TrainingDeskCalendar.App.Domain` 和日期类型不存在而编译失败。

- [ ] **步骤 3：实现最小日期领域代码**

```csharp
// TwoWeekRange.cs
namespace TrainingDeskCalendar.App.Domain;

internal sealed record TwoWeekRange(DateOnly Start, DateOnly End)
{
    public IReadOnlyList<DateOnly> Days =>
        Enumerable.Range(0, End.DayNumber - Start.DayNumber + 1)
            .Select(Start.AddDays)
            .ToArray();
}
```

```csharp
// CalendarRangeService.cs
namespace TrainingDeskCalendar.App.Domain;

internal sealed class CalendarRangeService
{
    public TwoWeekRange Containing(DateOnly date)
    {
        int daysFromMonday = ((int)date.DayOfWeek + 6) % 7;
        DateOnly start = date.AddDays(-daysFromMonday);
        return new TwoWeekRange(start, start.AddDays(13));
    }

    public TwoWeekRange Move(TwoWeekRange current, int pages)
    {
        ArgumentNullException.ThrowIfNull(current);
        DateOnly start = current.Start.AddDays(pages * 14);
        return new TwoWeekRange(start, start.AddDays(13));
    }
}
```

- [ ] **步骤 4：先写计划值对象测试，再实现计划值对象**

测试必须覆盖：1 至 6 的色值合法，0 和 7 被拒绝；文本允许换行；更新日期使用 UTC；完成状态不由颜色推断。实现使用如下 API：

```csharp
// TaskColorId.cs
namespace TrainingDeskCalendar.App.Domain;

internal enum TaskColorId
{
    Teal = 1,
    Blue = 2,
    Orange = 3,
    Red = 4,
    Purple = 5,
    Gray = 6
}
```

```csharp
// TrainingPlan.cs
namespace TrainingDeskCalendar.App.Domain;

internal sealed record TrainingPlan(
    DateOnly Date,
    string Text,
    TaskColorId Color,
    bool IsCompleted,
    DateTimeOffset UpdatedAtUtc)
{
    public static TrainingPlan Create(
        DateOnly date,
        string text,
        TaskColorId color = TaskColorId.Gray,
        bool isCompleted = false,
        DateTimeOffset? updatedAtUtc = null)
    {
        if (!Enum.IsDefined(color))
        {
            throw new ArgumentOutOfRangeException(nameof(color));
        }

        return new TrainingPlan(
            date,
            text ?? throw new ArgumentNullException(nameof(text)),
            color,
            isCompleted,
            (updatedAtUtc ?? DateTimeOffset.UtcNow).ToUniversalTime());
    }

    public bool IsDefaultEmpty =>
        string.IsNullOrWhiteSpace(Text) && !IsCompleted && Color == TaskColorId.Gray;
}
```

- [ ] **步骤 5：运行领域测试并提交**

运行：

```powershell
dotnet test tests/TrainingDeskCalendar.App.Tests/TrainingDeskCalendar.App.Tests.csproj --filter "FullyQualifiedName~Domain"
```

预期：日期和计划领域测试全部通过；提交：

```powershell
git add src/TrainingDeskCalendar.App/Domain tests/TrainingDeskCalendar.App.Tests/Domain
git commit -m "feat: define calendar and training plan domain"
```

## Task 2：引入 SQLite 存储与迁移边界

**文件：**

- 修改 `src/TrainingDeskCalendar.App/TrainingDeskCalendar.App.csproj`，增加 `<PackageReference Include="Microsoft.Data.Sqlite" Version="10.0.0" />`
- 创建 `src/TrainingDeskCalendar.App/Persistence/AppDataPaths.cs`
- 创建 `src/TrainingDeskCalendar.App/Application/ITrainingPlanStore.cs`
- 创建 `src/TrainingDeskCalendar.App/Persistence/SqlitePlanStore.cs`
- 创建 `tests/TrainingDeskCalendar.App.Tests/Persistence/SqlitePlanStoreTests.cs`

- [ ] **步骤 1：先定义存储接口和失败测试**

接口固定为：

```csharp
namespace TrainingDeskCalendar.App.Application;

internal interface ITrainingPlanStore
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<TrainingPlan?> GetAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TrainingPlan>> GetRangeAsync(
        DateOnly start,
        DateOnly end,
        CancellationToken cancellationToken = default);
    Task SaveAsync(TrainingPlan plan, CancellationToken cancellationToken = default);
    Task SaveManyAsync(
        IReadOnlyCollection<TrainingPlan> plans,
        CancellationToken cancellationToken = default);
    Task DeleteAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TrainingPlan>> GetAllAsync(CancellationToken cancellationToken = default);
    Task ReplaceAllAsync(
        IReadOnlyCollection<TrainingPlan> plans,
        CancellationToken cancellationToken = default);
}
```

失败测试必须使用一个临时数据库路径，验证：首次初始化创建表；保存后可按日期和日期范围读取；空默认记录被删除；`SaveManyAsync` 是单事务；非法色值不会写入。

- [ ] **步骤 2：实现数据路径和值映射**

```csharp
// AppDataPaths.cs
namespace TrainingDeskCalendar.App.Persistence;

internal sealed record AppDataPaths(
    string Root,
    string DatabasePath,
    string SettingsPath,
    string BackupDirectory)
{
    public static AppDataPaths ForCurrentUser()
    {
        string root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TrainingDeskCalendar");
        return ForRoot(root);
    }

    internal static AppDataPaths ForRoot(string root) => new(
        root,
        Path.Combine(root, "training-desk-calendar.db"),
        Path.Combine(root, "settings.json"),
        Path.Combine(root, "backups"));
}
```

数据库表固定为：

```sql
CREATE TABLE IF NOT EXISTS schema_info (version INTEGER NOT NULL);
CREATE TABLE IF NOT EXISTS plans (
    date TEXT NOT NULL PRIMARY KEY,
    text TEXT NOT NULL,
    color_id INTEGER NOT NULL CHECK(color_id BETWEEN 1 AND 6),
    is_completed INTEGER NOT NULL CHECK(is_completed IN (0, 1)),
    updated_at_utc TEXT NOT NULL
);
```

`SqlitePlanStore.InitializeAsync` 必须在事务中创建 `schema_info`，将版本设为 `1`；已存在版本大于 `1` 时抛出 `InvalidDataException`；读取时验证日期、色值和布尔值，非法数据抛出而不是静默修复。

- [ ] **步骤 3：实现 upsert、删除和批量事务**

`SaveAsync` 对 `TrainingPlan.IsDefaultEmpty` 调用 `DELETE`，否则使用参数化 `INSERT ... ON CONFLICT(date) DO UPDATE`。`SaveManyAsync` 对所有记录使用一个 SQLite transaction；任意记录失败时回滚全部变更。`ReplaceAllAsync` 在同一个事务中删除全部计划后插入传入记录。

- [ ] **步骤 4：运行存储测试并提交**

```powershell
dotnet test tests/TrainingDeskCalendar.App.Tests/TrainingDeskCalendar.App.Tests.csproj --filter SqlitePlanStoreTests
dotnet build TrainingDeskCalendar.sln --configuration Debug
git add src/TrainingDeskCalendar.App/TrainingDeskCalendar.App.csproj src/TrainingDeskCalendar.App/Persistence src/TrainingDeskCalendar.App/Application/ITrainingPlanStore.cs tests/TrainingDeskCalendar.App.Tests/Persistence
git commit -m "feat: persist training plans in sqlite"
```

## Task 3：实现计划应用服务与复制冲突规则

**文件：**

- 创建 `src/TrainingDeskCalendar.App/Application/CopyPlanResult.cs`
- 创建 `src/TrainingDeskCalendar.App/Application/TrainingPlanService.cs`
- 创建 `tests/TrainingDeskCalendar.App.Tests/Application/TrainingPlanServiceTests.cs`

- [ ] **步骤 1：定义服务 API 并写失败测试**

```csharp
namespace TrainingDeskCalendar.App.Application;

internal sealed record CopyConflict(DateOnly SourceDate, DateOnly TargetDate);

internal sealed record CopyPlanResult(
    bool Applied,
    IReadOnlyList<CopyConflict> Conflicts);

internal sealed class TrainingPlanService
{
    public Task<IReadOnlyList<TrainingPlan>> GetRangeAsync(
        DateOnly start,
        DateOnly end,
        CancellationToken cancellationToken = default);

    public Task SaveAsync(TrainingPlan plan, CancellationToken cancellationToken = default);

    public Task SetCompletedAsync(
        DateOnly date,
        bool isCompleted,
        CancellationToken cancellationToken = default);

    public Task<CopyPlanResult> CopyDayToNextWeekAsync(
        DateOnly sourceDate,
        bool overwrite,
        CancellationToken cancellationToken = default);

    public Task<CopyPlanResult> CopyWeekToNextWeekAsync(
        DateOnly weekStart,
        bool overwrite,
        CancellationToken cancellationToken = default);
}
```

测试必须先证明：单日复制目标已有记录时只返回冲突不写入；确认覆盖后复制文本和色值但把 `IsCompleted` 重置为 `false`；整周复制一次性列出所有冲突；空源日期不删除目标；保存默认空记录会调用存储删除路径。

- [ ] **步骤 2：实现日期映射和冲突预览**

单日目标日期为 `sourceDate.AddDays(7)`；整周要求 `weekStart.DayOfWeek == Monday`，逐日映射 `weekStart.AddDays(index)` 到 `weekStart.AddDays(index + 7)`。先读取源和目标，再构造 `CopyConflict` 列表；存在冲突且 `overwrite == false` 时返回 `Applied=false`，不得调用写入。

- [ ] **步骤 3：实现确认后的事务覆盖**

确认覆盖时复制源文本和颜色，更新时间使用新的 UTC 时间，完成状态固定为 `false`，通过 `SaveManyAsync` 一次提交。整周复制不得逐条独立提交，以避免中途失败留下半周结果。

- [ ] **步骤 4：运行应用服务测试并提交**

```powershell
dotnet test tests/TrainingDeskCalendar.App.Tests/TrainingDeskCalendar.App.Tests.csproj --filter TrainingPlanServiceTests
git add src/TrainingDeskCalendar.App/Application tests/TrainingDeskCalendar.App.Tests/Application
git commit -m "feat: add plan editing and copy application service"
```

## Task 4：实现 250 毫秒自动保存协调器

**文件：**

- 创建 `src/TrainingDeskCalendar.App/Application/PlanAutosaveCoordinator.cs`
- 创建 `tests/TrainingDeskCalendar.App.Tests/Application/PlanAutosaveCoordinatorTests.cs`

- [ ] **步骤 1：定义可测试的延迟边界和红灯测试**

```csharp
internal delegate Task AutosaveDelay(
    TimeSpan delay,
    CancellationToken cancellationToken);
```

`PlanAutosaveCoordinator` 构造函数接收 `TrainingPlanService`、`AutosaveDelay` 和 `TimeSpan debounce = 250ms`，公开 `void Queue(TrainingPlan plan)`、`Task FlushAsync(CancellationToken)` 和 `ValueTask DisposeAsync()`。测试使用 `TaskCompletionSource` 控制延迟，不使用真实 `Task.Delay`，验证同一日期连续输入只保存最后一次；250ms 前不保存；`FlushAsync` 立即保存；`DisposeAsync` 取消延迟并保存最后待写内容。

- [ ] **步骤 2：实现排队、取消和刷新**

`Queue` 替换日期的待保存草稿并取消旧 token；延迟完成后调用 `TrainingPlanService.SaveAsync`。保存失败时保留待保存草稿并让异常返回给调用方；`FlushAsync` 取消所有延迟并按日期顺序保存待保存草稿。退出和翻页调用 `FlushAsync`，不能丢失内存内容。

- [ ] **步骤 3：运行自动保存测试并提交**

```powershell
dotnet test tests/TrainingDeskCalendar.App.Tests/TrainingDeskCalendar.App.Tests.csproj --filter PlanAutosaveCoordinatorTests
git add src/TrainingDeskCalendar.App/Application/PlanAutosaveCoordinator.cs tests/TrainingDeskCalendar.App.Tests/Application/PlanAutosaveCoordinatorTests.cs
git commit -m "feat: debounce plan autosave"
```

## Task 5：实现版本化设置文件

**文件：**

- 创建 `src/TrainingDeskCalendar.App/Persistence/AppSettings.cs`
- 创建 `src/TrainingDeskCalendar.App/Persistence/SettingsStore.cs`
- 创建 `tests/TrainingDeskCalendar.App.Tests/Persistence/SettingsStoreTests.cs`

- [ ] **步骤 1：写设置校验测试**

测试覆盖：默认值为 `Version=1`、浅色、`Opacity=1.0`、未锁定、默认开机自启动；透明度小于 `0.4` 或大于 `1.0` 被拒绝；窗口宽高不能小于原型最小值 `840x360`；JSON 版本大于 1 被拒绝；写入失败不损坏上一次有效文件。

- [ ] **步骤 2：实现设置模型与原子 JSON 写入**

```csharp
namespace TrainingDeskCalendar.App.Persistence;

internal enum AppTheme
{
    Light,
    Dark
}

internal sealed record AppSettings(
    int Version,
    double WindowX,
    double WindowY,
    double WindowWidth,
    double WindowHeight,
    string MonitorId,
    bool IsLocked,
    AppTheme Theme,
    double Opacity,
    bool StartWithWindows,
    DateTimeOffset? LastUpdateCheckUtc)
{
    public static AppSettings Defaults => new(
        1, 100, 100, 1120, 470, string.Empty, false,
        AppTheme.Light, 1.0, true, null);

    public AppSettings Validate()
    {
        if (Version != 1 || WindowWidth < 840 || WindowHeight < 360 ||
            Opacity is < 0.4 or > 1.0 || !Enum.IsDefined(Theme))
        {
            throw new InvalidDataException("Settings are invalid.");
        }

        return this;
    }
}
```

`SettingsStore.LoadAsync` 读取 UTF-8 JSON 并校验；损坏文件重命名为 `settings.corrupt-<timestamp>.json` 后返回默认值。`SaveAsync` 先写同目录临时文件，再用同卷替换目标文件，避免进程中断留下半个 JSON。

- [ ] **步骤 3：运行设置测试并提交**

```powershell
dotnet test tests/TrainingDeskCalendar.App.Tests/TrainingDeskCalendar.App.Tests.csproj --filter SettingsStoreTests
git add src/TrainingDeskCalendar.App/Persistence/AppSettings.cs src/TrainingDeskCalendar.App/Persistence/SettingsStore.cs tests/TrainingDeskCalendar.App.Tests/Persistence/SettingsStoreTests.cs
git commit -m "feat: store validated user settings"
```

## Task 6：实现导出、导入和恢复副本

**文件：**

- 创建 `src/TrainingDeskCalendar.App/Persistence/SnapshotFormat.cs`
- 创建 `src/TrainingDeskCalendar.App/Application/DataTransferService.cs`
- 创建 `tests/TrainingDeskCalendar.App.Tests/Application/DataTransferServiceTests.cs`

- [ ] **步骤 1：先写完整恢复失败测试**

测试必须覆盖：导出包含格式版本、全部计划和设置但不包含诊断日志；导入前创建恢复副本；完整导入替换而非合并；未知版本、非法颜色、损坏 JSON 被拒绝且现有数据库和设置保持不变；数据库替换成功但设置写入失败时，两者都回滚到导入前状态。

- [ ] **步骤 2：定义快照格式**

```csharp
namespace TrainingDeskCalendar.App.Persistence;

internal sealed record SnapshotFormat(
    int FormatVersion,
    DateTimeOffset ExportedAtUtc,
    IReadOnlyList<TrainingPlan> Plans,
    AppSettings Settings);
```

JSON 使用 camelCase，日期写为 ISO `yyyy-MM-dd`，色值写整数 `colorId`，时间戳写 UTC ISO 字符串。反序列化后必须逐项调用 `TrainingPlan.Create` 和 `AppSettings.Validate`，不能直接信任 JSON 对象。

- [ ] **步骤 3：实现导出和恢复副本**

`DataTransferService.ExportAsync` 从 store 读取全部计划、从 settings store 读取设置，写入用户指定路径。`ImportAsync` 先完整读取并校验文件，再把当前数据库和设置写入 `AppDataPaths.BackupDirectory` 的带 UTC 文件名快照；备份成功后调用 `ReplaceAllAsync` 和设置写入。

- [ ] **步骤 4：实现回滚和备份清理策略**

导入任一步骤抛异常时，从刚创建的恢复副本重新调用 `ReplaceAllAsync` 和 settings save，然后重新抛出原异常。恢复副本不得在导入成功后删除；只保留最近 5 份，删除前先按文件名排序并限制目标目录为应用数据目录。

- [ ] **步骤 5：运行传输测试并提交**

```powershell
dotnet test tests/TrainingDeskCalendar.App.Tests/TrainingDeskCalendar.App.Tests.csproj --filter DataTransferServiceTests
dotnet test TrainingDeskCalendar.sln --configuration Release
git add src/TrainingDeskCalendar.App/Persistence/SnapshotFormat.cs src/TrainingDeskCalendar.App/Application/DataTransferService.cs tests/TrainingDeskCalendar.App.Tests/Application/DataTransferServiceTests.cs
git commit -m "feat: add versioned backup import and export"
```

## Task 7：Phase 1 集成门禁

**文件：**

- 创建 `tests/TrainingDeskCalendar.App.Tests/Phase1/Phase1DataFlowTests.cs`
- 修改 `docs/validation/desktop-prototype-results.md`，仅记录原型门禁和 Phase 1 输入，不添加未完成的 Windows 通过结论

- [ ] **步骤 1：编写端到端本地数据测试**

测试使用临时根目录创建 `AppDataPaths`，按“保存计划 -> 读取两周 -> 完成 -> 单日复制 -> 整周复制 -> 导出 -> 清空 -> 导入”顺序验证最终数据与原始数据一致，并验证自动保存 flush 后没有待写草稿。

- [ ] **步骤 2：运行全部验证**

```powershell
dotnet build TrainingDeskCalendar.sln --configuration Debug
dotnet test TrainingDeskCalendar.sln --configuration Debug
dotnet test TrainingDeskCalendar.sln --configuration Release
git diff --check
git status --short --branch
```

预期：构建无警告无错误，所有测试通过，工作树只包含本任务明确的文档变更或为空。

- [ ] **步骤 3：提交 Phase 1 集成门禁**

```powershell
git add tests/TrainingDeskCalendar.App.Tests/Phase1 docs/validation/desktop-prototype-results.md
git commit -m "test: gate phase one local data flow"
```

## Phase 1 完成条件

只有 Task 1 至 Task 7 全部完成、SQLite/JSON/事务/回滚/复制/自动保存测试通过，且没有 WPF 窗口直接访问持久化实现时，才进入阶段 2 详细计划。Windows 10/11 多版本手工矩阵继续由 `docs/validation/desktop-prototype-manual-checks.md` 跟踪，不得在本计划中标记为已通过。
