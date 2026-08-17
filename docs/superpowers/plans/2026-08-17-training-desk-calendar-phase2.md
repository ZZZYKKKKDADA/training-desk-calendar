# 训练桌历阶段 2：完整桌面体验与 Windows 集成实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**目标：** 将阶段 0 的 WPF 桌面原型连接到阶段 1 的本地数据服务，交付可查看、编辑、完成、复制和恢复设置的两周训练桌历，并加入可测试的单实例、托盘、自启动、锁定、降级和多显示器集成边界。

**架构：** WPF 视图只绑定 `CalendarViewModel`、`DayCardViewModel` 和 `SettingsViewModel`，所有计划读写经过 `TrainingPlanService` 与 `PlanAutosaveCoordinator`，所有设置经过 `SettingsStore`。Windows 能力分别由 `IAppSingleInstance`, `ITrayService`, `IStartupRegistration` 和现有 `DesktopHostService`/`WindowPlacementCoordinator` 隔离；真实 Win32 实现只存在于基础设施类，测试使用内存替身。阶段 2 不创建安装器、不实现 GitHub 更新联网、不上传用户数据，这些属于阶段 3。

**技术栈：** C# 14、.NET 10、WPF、SQLite、`System.Text.Json`、xUnit；UI 使用原生 WPF 控件和简体中文资源，不新增第三方 UI 框架。

---

## 范围与验收边界

- 默认范围是包含当前日期的周一至下周日，共 14 天；上一页/下一页移动 14 天，“今天”恢复默认范围。
- 日期卡片单击原位展开编辑；编辑区包含多行文本、6 个固定颜色、完成复选框和保存命令；空白、切换日期、翻页、退出会强制刷新自动保存。
- 完成复选框不触发编辑展开；完成状态同时使用勾选标记与弱化样式表达。
- 全局背景支持浅色/深色和 `0.4..1.0` 透明度；卡片颜色仍使用阶段 1 的 6 色。
- 锁定时禁止拖动和缩放，但仍允许查看、完成切换、托盘显示/隐藏和设置解锁。
- 托盘菜单包含显示、锁定、开机自启动、设置、手动更新检查占位入口和退出；更新检查本阶段只保留接口和“阶段 3 提供”状态，不联网。
- 启动默认仅当前用户自启动；单实例第二次启动只通知第一实例显示窗口并退出。
- 阶段 0 手工 Windows 矩阵和阶段 3 安装/发布门禁保持未完成，不得在本计划中标为通过。

## 目录结构

```text
src/TrainingDeskCalendar.App/
  Calendar/
    CalendarViewModel.cs
    DayCardViewModel.cs
    CalendarCommand.cs
  Windows/
    IAppSingleInstance.cs
    AppSingleInstance.cs
    ITrayService.cs
    TrayService.cs
    IStartupRegistration.cs
    StartupRegistration.cs
    IUpdateCheckService.cs
    DeferredUpdateCheckService.cs
  Settings/
    SettingsViewModel.cs
  MainWindow.xaml
  MainWindow.xaml.cs
  App.xaml.cs
tests/TrainingDeskCalendar.App.Tests/
  Calendar/CalendarViewModelTests.cs
  Calendar/DayCardViewModelTests.cs
  Windows/AppSingleInstanceTests.cs
  Windows/StartupRegistrationTests.cs
  Windows/TrayServiceTests.cs
  Phase2/Phase2WorkflowTests.cs
docs/validation/phase2-results.md
```

## Task 1：建立可测试的日历视图模型边界

**文件：**

- 创建 `src/TrainingDeskCalendar.App/Calendar/CalendarCommand.cs`
- 创建 `src/TrainingDeskCalendar.App/Calendar/DayCardViewModel.cs`
- 创建 `src/TrainingDeskCalendar.App/Calendar/CalendarViewModel.cs`
- 创建 `tests/TrainingDeskCalendar.App.Tests/Calendar/DayCardViewModelTests.cs`
- 创建 `tests/TrainingDeskCalendar.App.Tests/Calendar/CalendarViewModelTests.cs`

- [ ] **步骤 1：先写失败测试**

测试固定以下 API 和行为：

```csharp
var viewModel = new CalendarViewModel(service, autosave, new CalendarRangeService(), today);
Assert.Equal(14, viewModel.Days.Count);
Assert.Equal(DayOfWeek.Monday, viewModel.Range.Start.DayOfWeek);
await viewModel.MoveAsync(1);
Assert.Equal(viewModel.Range.Start.AddDays(14), viewModel.Range.Start);
await viewModel.GoToTodayAsync();
```

还要测试：加载范围只创建已有记录的内容；`BeginEdit` 只展开目标卡片；`CancelEdit` 恢复原始草稿；`SaveEditAsync` 调用服务；完成切换不改变编辑状态；颜色选择只接受 `TaskColorId` 1 至 6。

- [ ] **步骤 2：运行 `CalendarViewModelTests` 和 `DayCardViewModelTests`，确认因类型不存在而失败。**

- [ ] **步骤 3：实现最小 view model**

`CalendarViewModel` 持有 `TwoWeekRange Range`、只读 `ObservableCollection<DayCardViewModel> Days`、`PreviousAsync`、`NextAsync`、`GoToTodayAsync`、`BeginEdit`、`CancelEdit`、`SaveEditAsync` 和 `FlushAsync`。每次翻页先调用 `FlushAsync`，再通过 `TrainingPlanService.GetRangeAsync` 读取新范围。`DayCardViewModel` 持有 `Date`, `Text`, `SelectedColor`, `IsCompleted`, `IsEditing`, `IsDirty`，文本变更只更新内存并交给协调器排队；完成切换调用 `SetCompletedAsync`，不调用 `BeginEdit`。

- [ ] **步骤 4：运行聚焦测试和全部既有测试，确认通过。**

- [ ] **步骤 5：提交 `feat: add calendar editing view models`。**

## Task 2：接入真实 SQLite、自动保存和两周 WPF 界面

**文件：**

- 修改 `src/TrainingDeskCalendar.App/MainWindow.xaml`
- 修改 `src/TrainingDeskCalendar.App/MainWindow.xaml.cs`
- 修改 `src/TrainingDeskCalendar.App/App.xaml.cs`
- 修改 `src/TrainingDeskCalendar.App/PrototypeDay.cs`，由 `DayCardViewModel` 替代原型数据
- 创建 `tests/TrainingDeskCalendar.App.Tests/Phase2/Phase2WorkflowTests.cs`

- [ ] **步骤 1：先写工作流失败测试**

使用临时 `AppDataPaths`、真实 `SqlitePlanStore`、`SettingsStore` 和 `TrainingPlanService`，验证“打开默认两周 → 编辑文本 → 250 ms 协调器刷新 → 重新加载仍存在 → 完成切换 → 单日复制 → 整周复制冲突确认”。测试必须断言数据库中的最终文本、色值和完成状态，而不是只检查 view model 属性。

- [ ] **步骤 2：运行 `Phase2WorkflowTests`，确认缺少视图组合根/绑定实现时失败。**

- [ ] **步骤 3：实现组合根**

`App.OnStartup` 创建 `AppDataPaths.ForCurrentUser()`、初始化 `SqlitePlanStore`、构造 `SettingsStore`、加载并校验 `AppSettings`，再创建 `TrainingPlanService`、`PlanAutosaveCoordinator` 和 `CalendarViewModel` 注入 `MainWindow`。初始化失败时显示不包含计划正文的错误并以默认设置启动；数据库初始化不得静默覆盖原文件。

- [ ] **步骤 4：替换原型 XAML**

窗口继续无边框、无任务栏按钮、非置顶；根布局为标题栏、命令行和两行七列卡片。标题栏提供“上一页”“今天”“下一页”、锁定图标按钮、设置按钮和关闭/隐藏按钮。卡片用 `ItemsControl` 绑定 `Days`；编辑状态使用卡片内部 `Grid` 的 `Visibility` 切换，保持卡片位置，不让其他日期重新排列；颜色使用 6 个 `RadioButton` 色块，必须有 `ToolTip` 和键盘焦点样式。

- [ ] **步骤 5：实现卡片交互和错误状态**

卡片单击进入编辑；完成 `CheckBox` 使用独立命令；保存失败时显示状态文本、保留输入内容、允许再次保存；窗口关闭先调用 `CalendarViewModel.FlushAsync` 和 `PlanAutosaveCoordinator.DisposeAsync`。

- [ ] **步骤 6：运行聚焦工作流、全量测试和 Debug 构建，提交 `feat: connect editable calendar to local data`。**

## Task 3：实现窗口外观、锁定、位置恢复和桌面降级

**文件：**

- 修改 `src/TrainingDeskCalendar.App/MainWindow.xaml.cs`
- 修改 `src/TrainingDeskCalendar.App/MainWindow.xaml`
- 修改 `src/TrainingDeskCalendar.App/Windowing/WindowPlacementCoordinator.cs`
- 创建 `tests/TrainingDeskCalendar.App.Tests/Calendar/WindowInteractionTests.cs`

- [ ] **步骤 1：先写失败测试**

通过窗口交互边界测试验证：锁定后忽略拖动/尺寸变化；解锁后允许；浅色和深色分别选择足够对比的前景/边框；透明度只接受 `0.4..1.0`；保存 `WindowX/Y/Width/Height/MonitorId` 后重启恢复；保存显示器不存在时调用现有 `WindowPlacementService.Normalize` 回到主屏。

- [ ] **步骤 2：实现设置驱动的外观和锁定**

窗口将 `AllowsTransparency` 保持启用，背景颜色通过 `AppSettings.Theme` 与 `AppSettings.Opacity` 映射；锁定时设置 `ResizeMode=NoResize` 并移除拖动处理，解锁时恢复 `CanResizeWithGrip`。每次位置/尺寸变化只更新内存设置，停止变化后异步保存；退出前强制保存。

- [ ] **步骤 3：实现启动恢复与降级**

加载设置后先应用归一化位置，再显示窗口；桌面挂载失败继续调用 `DesktopHostService` 的 fallback，状态栏和托盘仍可用。Explorer 重启消息继续触发重挂载，`WindowPlacementCoordinator.EnsureVisible` 只在窗口未锁定或显示器变化时修正位置。

- [ ] **步骤 4：运行窗口交互、窗口位置和全量测试，提交 `feat: persist desktop appearance and lock state`。**

## Task 4：实现单实例与当前用户开机自启动

**文件：**

- 创建 `src/TrainingDeskCalendar.App/Windows/IAppSingleInstance.cs`
- 创建 `src/TrainingDeskCalendar.App/Windows/AppSingleInstance.cs`
- 创建 `src/TrainingDeskCalendar.App/Windows/IStartupRegistration.cs`
- 创建 `src/TrainingDeskCalendar.App/Windows/StartupRegistration.cs`
- 创建 `tests/TrainingDeskCalendar.App.Tests/Windows/AppSingleInstanceTests.cs`
- 创建 `tests/TrainingDeskCalendar.App.Tests/Windows/StartupRegistrationTests.cs`

- [ ] **步骤 1：先写失败测试**

测试使用内存/文件替身验证：第一个实例获得当前用户范围的互斥；第二个实例返回 `AlreadyRunning` 并发送显示消息；释放后可重新获取；自启动开关只读写 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`，值为完整引用路径，关闭时删除，读取异常不抛出到 UI。

- [ ] **步骤 2：实现可测试边界和 Win32 实现**

`IAppSingleInstance.TryAcquire(string key, Action showExisting)` 不暴露互斥句柄给 UI；`AppSingleInstance` 使用 `Global` 之外的当前用户命名互斥和本地 IPC/窗口消息。`IStartupRegistration.IsEnabled`, `SetEnabled(bool)` 由 `StartupRegistration` 使用 `Microsoft.Win32.Registry.CurrentUser` 实现，不请求管理员权限，不写 `HKLM`。

- [ ] **步骤 3：在 `App.OnStartup` 最早阶段接入**

获取实例失败时只发送显示命令并退出，不初始化数据库、不创建第二个窗口；首次实例退出时释放互斥。应用路径必须使用 `Environment.ProcessPath`，写入前验证为绝对路径。

- [ ] **步骤 4：运行 Windows 边界单测和全量测试，提交 `feat: add per-user instance and startup integration`。**

## Task 5：实现系统托盘、设置窗口和数据传输入口

**文件：**

- 创建 `src/TrainingDeskCalendar.App/Windows/ITrayService.cs`
- 创建 `src/TrainingDeskCalendar.App/Windows/TrayService.cs`
- 创建 `src/TrainingDeskCalendar.App/Settings/SettingsViewModel.cs`
- 创建 `src/TrainingDeskCalendar.App/Settings/SettingsWindow.xaml`
- 创建 `src/TrainingDeskCalendar.App/Settings/SettingsWindow.xaml.cs`
- 修改 `src/TrainingDeskCalendar.App/App.xaml.cs`
- 修改 `src/TrainingDeskCalendar.App/MainWindow.xaml`
- 创建 `tests/TrainingDeskCalendar.App.Tests/Windows/TrayServiceTests.cs`

- [ ] **步骤 1：先写失败测试**

测试菜单命令包含显示、锁定、开机自启动、设置、手动更新检查、退出；点击显示调用窗口显示并激活，锁定切换保存设置，退出只由明确退出命令触发；关闭设置窗口不退出应用。`SettingsViewModel` 测试浅色/深色、透明度、重置窗口、导出和导入命令均调用对应边界服务。

- [ ] **步骤 2：实现托盘抽象和 Windows 托盘适配器**

托盘服务只接受命令回调，不直接读写 SQLite/注册表；当前 WPF 托盘实现使用 `System.Windows.Forms.NotifyIcon`，项目启用 `UseWindowsForms`，退出和资源释放在 `Dispose` 中完成。所有菜单文字为简体中文。

- [ ] **步骤 3：实现设置页**

设置页提供主题 `ComboBox`、透明度 `Slider`、锁定和自启动 `CheckBox`、重置窗口、导出、导入、检查更新、版本和 GitHub 链接。导入成功后刷新日历；导入失败显示错误且不关闭设置页；导出路径由 `SaveFileDialog` 选择，导入路径由 `OpenFileDialog` 选择。

- [ ] **步骤 4：运行托盘/设置测试和全量构建，提交 `feat: add tray and settings workflow`。**

## Task 6：Phase 2 集成门禁与验证记录

**文件：**

- 修改 `tests/TrainingDeskCalendar.App.Tests/Phase2/Phase2WorkflowTests.cs`
- 创建 `docs/validation/phase2-results.md`
- 修改 `docs/validation/desktop-prototype-results.md`，只补充 Phase 2 输入，不改写未完成的手工矩阵

- [ ] **步骤 1：扩展端到端测试**

验证真实组合根使用临时数据目录；验证启动默认范围、连续编辑自动保存、完成切换、前后翻页、单日/整周复制冲突、主题/透明度设置、导出导入、锁定和退出刷新。

- [ ] **步骤 2：运行 Phase 2 自动门禁**

```powershell
dotnet build TrainingDeskCalendar.sln --configuration Debug
dotnet test TrainingDeskCalendar.sln --configuration Debug
dotnet test TrainingDeskCalendar.sln --configuration Release
git diff --check
```

预期：构建 0 警告 0 错误；Debug/Release 全部测试通过；文档只记录自动化结果，不把未执行的跨版本 Windows 手工检查标为通过。

- [ ] **步骤 3：提交 `test: gate phase two desktop workflow`。**

## Phase 2 完成条件

Task 1 至 Task 6 全部提交且验证通过；WPF 不直接访问 SQLite 或注册表；核心本地工作流和设置入口可运行；单实例、自启动、托盘、锁定、位置恢复和桌面降级均有自动化边界测试。Windows 10/11 安装、DPI、Explorer 重启、休眠、多显示器和发布资产仍由阶段 3 最终验收。

## Spec Coverage Review

- 设计规格 3.1-3.2：Task 2-4；3.3-3.5：Task 1-2；3.6：Task 3。
- 设计规格 4：Task 5；5.1-5.4：阶段 1 已完成，Task 5 提供入口。
- 设计规格 6-7：Task 2-4；异常保存和隐私边界由阶段 1 与 Task 2/6 验证。
- 设计规格 8 的安装器、GitHub Actions、Releases、更新联网明确留给阶段 3，不在本计划中声称完成。
- 设计规格 10 的跨版本性能复测和 11.2 Windows 矩阵明确留给阶段 3。
