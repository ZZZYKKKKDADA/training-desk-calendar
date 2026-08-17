# 训练桌历 Phase 3A 当前用户安装器实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 生成无需管理员权限、无需预装 .NET、可创建桌面与开始菜单快捷方式，并支持安全保留或删除个人数据的 x64 安装包。

**Architecture:** WPF 主程序以未压缩 self-contained single-file `win-x64` 形式发布，直接使用 `TrainingDeskCalendar.App.exe` 启动。托盘使用 WPF `HwndSource` 和 Win32 `Shell_NotifyIconW`，不引用 WinForms；应用目标框架为 `net10.0-windows`，从构建依赖图中自然排除未使用的 Windows SDK 投影。未压缩单文件同时满足 2 秒启动、200 MiB 工作集和 150 MiB payload 门槛。Inno Setup 以当前用户模式安装到 `%LOCALAPPDATA%\Programs\TrainingDeskCalendar`，卸载默认保留 `%LOCALAPPDATA%\TrainingDeskCalendar` 数据。

**Tech Stack:** .NET 10.0.400 SDK、WPF、Win32 interop、PowerShell、Inno Setup 6、xUnit。

---

## 方案变更依据

- 私有 framework-dependent 应用加 Base/Desktop Runtime 的实测 payload 为 199.5 MB，超过 150 MB。
- 压缩 self-contained single-file 的稳定工作集约 351 MiB，超过批准后的 200 MiB 门槛。
- 带 WinForms 托盘的普通 self-contained 目录约 197.5 MB、稳定工作集约 192 MB，两项均超标。
- 原生托盘的普通 self-contained 目录虽满足体积与内存，但 5 次新路径启动最大 3401.7 ms，超过 2 秒门槛。
- `net10.0-windows` 未压缩 single-file 正式 payload 为 127.58 MiB；5 次新路径启动最大 1268.4 ms，15 秒采样工作集最大 172.5 MiB，两项均通过。
- 上述数据否决了私有运行时、普通目录、压缩 single-file 和 NativeAOT 启动器方向；对应项目、环境变量和分层 payload 不进入安装器。

## 文件结构

```text
eng/Versions.props                          应用版本与 win-x64 RID
scripts/package-windows.ps1                发布并验证 self-contained payload
scripts/measure-release-payload.ps1        5 次启动、内存和注册表恢复验证
src/TrainingDeskCalendar.App/Windows/
  TrayService.cs                           无 WinForms 的原生托盘
installer/TrainingDeskCalendar.iss         当前用户安装和卸载脚本
tests/TrainingDeskCalendar.App.Tests/Packaging/
  PackageScriptContractTests.cs            payload 组装契约
  ReleasePayloadMeasurementContractTests.cs 性能测量契约
  InstallerContractTests.cs                安装范围和卸载数据契约
scripts/test-installer.ps1                 安装/升级/卸载端到端验证
docs/validation/installer-results.md       本机验证结果
```

### Task 1：固化轻量化 self-contained payload

**Files:**
- Modify: `TrainingDeskCalendar.sln`
- Modify: `eng/Versions.props`
- Modify: `src/TrainingDeskCalendar.App/TrainingDeskCalendar.App.csproj`
- Modify: `src/TrainingDeskCalendar.App/Services/AppComposition.cs`
- Modify: `src/TrainingDeskCalendar.App/Windows/TrayService.cs`
- Delete: `src/TrainingDeskCalendar.Launcher/`
- Create: `scripts/package-windows.ps1`
- Create: `tests/TrainingDeskCalendar.App.Tests/Packaging/PackageScriptContractTests.cs`

- [ ] **Step 1: 保留已经先行完成的失败契约测试。** 测试要求 `--self-contained true`、固定 `artifacts\windows-x64\payload`、未压缩 single-file、原生库打包、不使用私有 runtime 下载或 launcher，并要求 150 MiB 硬门槛。
- [ ] **Step 2: 运行 `dotnet test tests/TrainingDeskCalendar.App.Tests/TrainingDeskCalendar.App.Tests.csproj -c Debug --filter FullyQualifiedName~PackageScriptContractTests`，确认旧私有运行时脚本不能满足契约。**
- [ ] **Step 3: 以 `Shell_NotifyIconW`、`CreatePopupMenu` 和消息窗口替换 WinForms `NotifyIcon`；删除 launcher 和 `UseWindowsForms`；由主 EXE 直接注册 HKCU Run。**
- [ ] **Step 4: 脚本执行 Release 未压缩 self-contained single-file publish，确认写 manifest 前仅存在主 EXE，生成带 SHA-256 和稳定精确字节数的 JSON manifest，并在最终 payload 大于等于 150 MiB 时失败。**
- [ ] **Step 5: 运行聚焦测试、Debug/Release 全量测试、Release 发布和真实启动，确认托盘、ready 信号、定时退出与体积契约。**

### Task 2：建立 Release payload 性能门禁

**Files:**
- Create: `scripts/measure-release-payload.ps1`
- Create: `tests/TrainingDeskCalendar.App.Tests/Packaging/ReleasePayloadMeasurementContractTests.cs`
- Create: `docs/validation/phase3a-payload-results.md`

- [ ] **Step 1: 写失败契约测试。** 脚本必须默认至少 5 次；每次把源 payload 物化到新的唯一目录，只启动一次并使用唯一 ready 文件；ready 后持续采样至少 15 秒，覆盖 5 秒桌面 watchdog；以最大启动时间不超过 2000 ms、最大工作集不超过 200 MiB 为门槛。
- [ ] **Step 2: 测试还必须要求脚本在 `finally` 中恢复测试前的 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\TrainingDeskCalendar` 状态并删除自己的临时目录。JSON 和提交的 Markdown 必须记录逐次原始值、源 EXE SHA-256、payload 字节数和文件数、OS build、Git commit、参数以及 `fresh-materialized-path` 分类。**
- [ ] **Step 3: 运行 `dotnet test ... --filter FullyQualifiedName~ReleasePayloadMeasurementContractTests`，确认缺少脚本而失败。**
- [ ] **Step 4: 实现最小脚本；它只接受 `artifacts` 下的 payload，拒绝少于 5 次，ready 超时、异常退出、超时不退出或任一性能门槛失败均返回非零。**
- [ ] **Step 5: 运行聚焦测试后执行 `powershell -ExecutionPolicy Bypass -File scripts/measure-release-payload.ps1`，记录至少 5 次真实结果；若失败，先按系统化调试定位根因，不放宽门槛。**

### Task 3：实现当前用户 Inno Setup 安装器

**Files:**
- Create: `installer/TrainingDeskCalendar.iss`
- Create: `tests/TrainingDeskCalendar.App.Tests/Packaging/InstallerContractTests.cs`

- [ ] **Step 1: 写失败测试，要求 `PrivilegesRequired=lowest`、不允许切换到管理员安装，默认目录为 `{localappdata}\Programs\TrainingDeskCalendar`，且不出现 HKLM 或 `{autopf}`。**
- [ ] **Step 2: 测试要求桌面和开始菜单快捷方式及 HKCU Run 均指向 `TrainingDeskCalendar.App.exe`；卸载默认保留 `{localappdata}\TrainingDeskCalendar`，只有用户明确勾选时删除。**
- [ ] **Step 3: 运行聚焦测试，确认安装脚本不存在而失败。**
- [ ] **Step 4: 实现安装脚本；升级前关闭已安装进程，安装完成可启动；卸载移除程序、快捷方式和 Run 值。删除数据前把目标解析并校验为当前用户应用数据目录。**
- [ ] **Step 5: 使用 `ISCC.exe installer\TrainingDeskCalendar.iss` 生成 `artifacts\installer\TrainingDeskCalendar-Setup-0.1.0-x64.exe`，断言安装包小于 80 MiB、payload 小于 150 MiB。**

### Task 4：安装、升级与卸载自动验证

**Files:**
- Create: `scripts/test-installer.ps1`
- Create: `tests/TrainingDeskCalendar.App.Tests/Packaging/InstallerValidationScriptContractTests.cs`
- Create: `docs/validation/installer-results.md`

- [ ] **Step 1: 写失败契约测试，要求唯一测试目录、当前用户安装、桌面/开始菜单快捷方式目标检查、ready-file 启动检查和 HKCU Run 检查。**
- [ ] **Step 2: 测试要求验证同版本覆盖安装保留数据库、默认卸载保留数据、明确选择删除时只删除 `%LOCALAPPDATA%\TrainingDeskCalendar`，并始终恢复测试前 Run 值。**
- [ ] **Step 3: 实现验证脚本并运行；任何安装、启动、升级、卸载或安全边界断言失败均返回非零。**
- [ ] **Step 4: 记录安装包 SHA-256、大小、安装目录大小、应用版本、操作系统和每项本机结果；未执行的 Windows 版本矩阵保持未通过。**
- [ ] **Step 5: 运行 Debug/Release 全量测试、`git diff --check`，请求独立规格与代码质量复查。**

## 完成门槛

- 安装和卸载不触发 UAC，不写 `%ProgramFiles%` 或 HKLM。
- 首次安装不依赖机器预装 .NET。
- Release payload 的 5 次启动最大值不超过 2 秒，工作集最大值不超过 200 MiB。
- 桌面、开始菜单、卸载、自启动和数据保留选择均有契约测试与本机端到端证据。
- 安装包小于 80 MiB，安装目录小于 150 MiB；未满足时先优化，不能只记录偏差。
