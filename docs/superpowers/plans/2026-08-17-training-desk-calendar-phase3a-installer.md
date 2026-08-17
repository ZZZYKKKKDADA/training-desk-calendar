# 训练桌历 Phase 3A 当前用户安装器实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 生成无需管理员权限、无需预装 .NET、可创建桌面与开始菜单快捷方式并支持选择性删除个人数据的 x64 安装包。

**Architecture:** WPF 主程序保持 framework-dependent；安装包内置固定版本的私有 Windows Desktop Runtime。一个 NativeAOT WinExe 启动器只负责设置进程级 `DOTNET_ROOT_X64` 并启动主程序。Inno Setup 以当前用户模式安装到 `%LOCALAPPDATA%\Programs\TrainingDeskCalendar`，卸载时默认保留 `%LOCALAPPDATA%\TrainingDeskCalendar` 数据，并提供明确删除选项。

**Tech Stack:** .NET 10.0.400 SDK、Windows Desktop Runtime 10.0.11、C# NativeAOT、PowerShell、Inno Setup 6、xUnit。

---

## 文件结构

```text
eng/Versions.props                         固定应用和私有运行时版本
scripts/package-windows.ps1               下载校验运行时、发布、组装 payload
src/TrainingDeskCalendar.Launcher/         无托管运行时依赖的启动器
installer/TrainingDeskCalendar.iss         当前用户安装和卸载脚本
tests/TrainingDeskCalendar.App.Tests/Packaging/
  LaunchLayoutTests.cs                     启动路径与运行时布局规则
  InstallerContractTests.cs                安装范围、快捷方式、卸载数据契约
docs/validation/installer-results.md        安装资产和本机验证结果
```

### Task 1：固定发布版本与私有运行时布局

**Files:**
- Create: `eng/Versions.props`
- Create: `src/TrainingDeskCalendar.Launcher/TrainingDeskCalendar.Launcher.csproj`
- Create: `src/TrainingDeskCalendar.Launcher/LaunchLayout.cs`
- Create: `tests/TrainingDeskCalendar.App.Tests/Packaging/LaunchLayoutTests.cs`
- Modify: `TrainingDeskCalendar.sln`
- Modify: `Directory.Build.props`
- Modify: `tests/TrainingDeskCalendar.App.Tests/TrainingDeskCalendar.App.Tests.csproj`

- [ ] **Step 1: 写失败测试**

```csharp
LaunchLayout layout = LaunchLayout.FromBaseDirectory(@"C:\App");
Assert.Equal(@"C:\App\runtime", layout.DotNetRoot);
Assert.Equal(@"C:\App\app\TrainingDeskCalendar.App.exe", layout.ApplicationPath);
Assert.Throws<DirectoryNotFoundException>(() => layout.Validate(fileSystem));
```

- [ ] **Step 2: 运行 `dotnet test ... --filter FullyQualifiedName~LaunchLayoutTests`，确认类型不存在而失败。**
- [ ] **Step 3: 创建启动器项目和测试项目引用，实现不可接受相对路径、缺失 runtime 或缺失主程序的 `LaunchLayout`；在 `Versions.props` 固定 `Version=0.1.0`、`DotNetRuntimeVersion=10.0.11`、`RuntimeIdentifier=win-x64`，由 `Directory.Build.props` 导入。**
- [ ] **Step 4: 运行聚焦测试和 Debug 构建，确认通过。**
- [ ] **Step 5: 提交 `build: define private runtime package layout`。**

### Task 2：实现 NativeAOT 无控制台启动器

**Files:**
- Create: `src/TrainingDeskCalendar.Launcher/Program.cs`
- Create: `src/TrainingDeskCalendar.App/Windows/StartupExecutableResolver.cs`
- Create: `tests/TrainingDeskCalendar.App.Tests/Packaging/LauncherCommandTests.cs`
- Create: `tests/TrainingDeskCalendar.App.Tests/Windows/StartupExecutableResolverTests.cs`
- Modify: `src/TrainingDeskCalendar.App/Services/AppComposition.cs`

- [ ] **Step 1: 写失败测试，验证启动命令使用 payload 内的主程序、工作目录为 `app`、只在子进程环境中设置 `DOTNET_ROOT_X64` 和 `TRAINING_DESK_CALENDAR_LAUNCHER`，参数原样转发；自启动路径优先使用该环境变量中的绝对现存启动器，开发运行才回退 `Environment.ProcessPath`。**
- [ ] **Step 2: 运行聚焦测试，确认启动命令工厂不存在而失败。**
- [ ] **Step 3: 将启动器项目配置为 `OutputType=WinExe`、`PublishAot=true`、`SelfContained=true`；使用 `ProcessStartInfo.UseShellExecute=false` 启动主程序，不写永久用户环境变量。组合根通过 `StartupExecutableResolver` 构造 `StartupRegistration`，保证应用设置开关不会把 HKCU Run 改回无法独立启动的主 EXE。布局无效时显示不含用户数据的原生错误并返回非零退出码。**
- [ ] **Step 4: 执行以下发布并确认产物无需已安装 .NET 即可启动：**

```powershell
dotnet publish src/TrainingDeskCalendar.Launcher/TrainingDeskCalendar.Launcher.csproj -c Release -r win-x64
```

- [ ] **Step 5: 提交 `feat: add private runtime launcher`。**

### Task 3：实现可重复的 Windows payload 组装

**Files:**
- Create: `scripts/package-windows.ps1`
- Create: `tests/TrainingDeskCalendar.App.Tests/Packaging/PackageScriptContractTests.cs`

- [ ] **Step 1: 写失败契约测试，解析脚本并验证其读取 `Versions.props`、使用 Release framework-dependent publish、下载 `windowsdesktop-runtime-10.0.11-win-x64.zip`、从官方 release metadata 读取 SHA-512 并校验、拒绝散列不匹配。**
- [ ] **Step 2: 运行测试确认失败。**
- [ ] **Step 3: 实现脚本，将输出固定为 `artifacts/windows-x64/payload/{launcher.exe,app/,runtime/}`；下载缓存放在 `artifacts/cache`，任何网络、JSON、散列、解压或发布错误都以非零退出。**
- [ ] **Step 4: 运行脚本两次，确认第二次复用已校验缓存；删除系统 `DOTNET_ROOT*` 的子进程环境后通过启动器完成 ready-file/定时退出验证。**
- [ ] **Step 5: 提交 `build: assemble private runtime windows payload`。**

### Task 4：实现当前用户 Inno Setup 安装器

**Files:**
- Create: `installer/TrainingDeskCalendar.iss`
- Create: `tests/TrainingDeskCalendar.App.Tests/Packaging/InstallerContractTests.cs`

- [ ] **Step 1: 写失败测试，验证安装脚本包含以下不可变契约：**

```text
PrivilegesRequired=lowest
DefaultDirName={localappdata}\Programs\TrainingDeskCalendar
桌面快捷方式 -> TrainingDeskCalendar.Launcher.exe
开始菜单快捷方式 -> TrainingDeskCalendar.Launcher.exe
HKCU Run -> quoted launcher path
卸载默认不删除 {localappdata}\TrainingDeskCalendar
只有用户勾选“同时删除个人数据”时才删除该目录
```

- [ ] **Step 2: 运行聚焦测试确认安装脚本不存在而失败。**
- [ ] **Step 3: 实现安装脚本；升级安装先结束已有进程，安装完成默认启动；卸载移除程序、快捷方式和 HKCU Run 值。个人数据复选框默认不选，删除前将目标解析并校验为 `%LOCALAPPDATA%\TrainingDeskCalendar`。**
- [ ] **Step 4: 使用 `ISCC.exe installer\TrainingDeskCalendar.iss` 生成 `artifacts/installer/TrainingDeskCalendar-Setup-0.1.0-x64.exe`，确认压缩包小于 80 MB、安装目录小于 150 MB。**
- [ ] **Step 5: 提交 `feat: add per-user windows installer`。**

### Task 5：安装、升级与卸载自动验证

**Files:**
- Create: `scripts/test-installer.ps1`
- Create: `docs/validation/installer-results.md`

- [ ] **Step 1: 脚本静默安装到唯一测试目录，断言无管理员提升、主文件/私有运行时/卸载器/当前用户桌面与开始菜单快捷方式存在。**
- [ ] **Step 2: 从快捷方式启动，使用 ready-file 验证主程序加载，并断言 HKCU Run 指向带引号的启动器绝对路径。**
- [ ] **Step 3: 覆盖安装同版本并验证用户数据库仍存在；卸载默认保留数据；重新安装后选择删除数据并验证仅删除应用数据目录。**
- [ ] **Step 4: 记录安装包 SHA-256、大小、安装目录大小、运行时版本和本机结果；未执行的 Windows 版本矩阵保持未通过。**
- [ ] **Step 5: 运行全量 Debug/Release 测试、`git diff --check`，提交 `test: gate per-user installer workflow`。**

## 完成门槛

- 安装和卸载不触发 UAC，不写 `%ProgramFiles%` 或 HKLM。
- 首次安装不依赖机器预装 .NET。
- 桌面、开始菜单、卸载、自启动和数据保留选择均有契约测试与本机端到端证据。
- 安装包和安装目录满足 80 MB / 150 MB 目标；未满足时先优化，不能只记录偏差。
