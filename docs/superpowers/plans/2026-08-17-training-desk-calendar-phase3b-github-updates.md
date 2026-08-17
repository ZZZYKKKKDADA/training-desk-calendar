# 训练桌历 Phase 3B GitHub 更新与发布实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 用构建时仓库元数据启用 GitHub Releases 更新检查，并建立可复现的 CI、签名外发布资产、校验值和中文维护文档。

**Architecture:** 更新服务只调用公开 GitHub Releases REST API，不读取或上传计划数据。仓库 URL 在 CI 中由 `github.repository` 注入程序集元数据，本地未配置时显示明确不可用状态。自动检查每天最多一次且网络失败静默；手动检查显示失败。发布工作流只在 `vMAJOR.MINOR.PATCH` 标签触发并调用 Phase 3A 打包脚本。

**Tech Stack:** `HttpClient`、`System.Text.Json`、GitHub REST API、GitHub Actions、PowerShell、xUnit。

---

### Task 1：版本与仓库元数据边界

**Files:**
- Create: `src/TrainingDeskCalendar.App/Updates/ReleaseVersion.cs`
- Create: `src/TrainingDeskCalendar.App/Updates/RepositoryMetadata.cs`
- Create: `tests/TrainingDeskCalendar.App.Tests/Updates/ReleaseVersionTests.cs`
- Modify: `src/TrainingDeskCalendar.App/TrainingDeskCalendar.App.csproj`

- [ ] **Step 1: 写失败测试，覆盖 `v1.2.3`、`1.2.3`、更高/相同/更低版本、预发布标签拒绝、非 GitHub RepositoryUrl 拒绝、从 `https://github.com/owner/repo` 得到 `owner/repo`。**
- [ ] **Step 2: 运行聚焦测试确认类型不存在。**
- [ ] **Step 3: 实现严格三段非负整数版本与仓库 URL 解析；项目默认不硬编码账号，CI 使用 `-p:RepositoryUrl=https://github.com/${{ github.repository }}` 注入。**
- [ ] **Step 4: 运行测试并提交 `feat: add release metadata boundaries`。**

### Task 2：GitHub Releases 更新服务

**Files:**
- Replace: `src/TrainingDeskCalendar.App/Services/IUpdateCheckService.cs`
- Create: `src/TrainingDeskCalendar.App/Updates/GitHubReleaseUpdateCheckService.cs`
- Create: `tests/TrainingDeskCalendar.App.Tests/Updates/GitHubReleaseUpdateCheckServiceTests.cs`

- [ ] **Step 1: 写失败测试，固定 `UpdateCheckResult` 状态：`Unavailable`、`UpToDate`、`UpdateAvailable`、`Failed`；验证 API 请求为 `/repos/{owner}/{repo}/releases/latest`、User-Agent 非空、响应只读取 `tag_name` 与 `html_url`。**
- [ ] **Step 2: 增加自动/手动模式测试：`LastUpdateCheckUtc` 距今不足 24 小时时自动检查跳过；自动网络失败返回静默失败；手动失败包含可显示错误；取消令牌正常传播。**
- [ ] **Step 3: 实现服务；使用注入的 `HttpClient`、`TimeProvider`、当前版本和设置保存回调，不缓存计划、路径或响应正文到日志。**
- [ ] **Step 4: 运行聚焦和全量测试，提交 `feat: check github releases for updates`。**

### Task 3：接入启动、托盘和设置页

**Files:**
- Modify: `src/TrainingDeskCalendar.App/Services/AppComposition.cs`
- Modify: `src/TrainingDeskCalendar.App/App.xaml.cs`
- Modify: `src/TrainingDeskCalendar.App/Settings/SettingsViewModel.cs`
- Modify: `src/TrainingDeskCalendar.App/Settings/SettingsWindow.xaml`
- Create: `tests/TrainingDeskCalendar.App.Tests/Updates/UpdatePresentationTests.cs`

- [ ] **Step 1: 写失败测试，验证自动检查只更新状态不弹网络错误；手动检查失败显示错误；发现新版本时提供版本号和 HTTPS release URL；用户确认后才调用浏览器打开；设置页 GitHub 链接来自 `RepositoryMetadata`。**
- [ ] **Step 2: 实现 `IExternalUriLauncher` 边界，使用 `ProcessStartInfo.UseShellExecute=true` 打开 HTTPS URI，拒绝其他 scheme。**
- [ ] **Step 3: 应用启动后低优先级触发自动检查；托盘和设置使用手动模式。无仓库元数据的本地构建显示“当前构建未配置 GitHub 仓库”，不联网。**
- [ ] **Step 4: 运行测试和受控启动，提交 `feat: connect release updates to desktop ui`。**

### Task 4：仓库维护文件和 CI

**Files:**
- Create: `LICENSE`
- Create: `README.md`
- Create: `CONTRIBUTING.md`
- Create: `.github/workflows/ci.yml`
- Create: `.github/dependabot.yml`
- Create: `tests/TrainingDeskCalendar.App.Tests/Release/RepositoryContractTests.cs`

- [ ] **Step 1: 写契约测试，验证 MIT 正文、README 的安装/使用/数据位置/导入导出/卸载保留/构建说明，以及 CI 的锁定还原、Release 构建、Debug/Release 测试和 `git diff --check`。**
- [ ] **Step 2: 实现维护文件；CI 权限保持只读，使用固定 major 版本官方 actions，不要求 secrets。**
- [ ] **Step 3: 使用本地命令复现 CI，提交 `ci: add repository build and maintenance files`。**

### Task 5：标签发布与资产校验

**Files:**
- Create: `.github/workflows/release.yml`
- Create: `scripts/write-checksums.ps1`
- Create: `docs/releasing.md`
- Create: `tests/TrainingDeskCalendar.App.Tests/Release/ReleaseWorkflowContractTests.cs`

- [ ] **Step 1: 写失败测试，验证只接受 `vMAJOR.MINOR.PATCH`，标签版本必须等于 `Versions.props`；工作流调用 Windows payload 与 Inno 脚本，生成 SHA-256 文件，并上传安装包、校验值和非空 release notes。**
- [ ] **Step 2: 实现 `contents: write` 的最小发布权限；构建命令注入当前 `github.repository`，不包含上传证书或计划数据的步骤。**
- [ ] **Step 3: 本地用临时标签字符串运行版本校验与校验值脚本；在 GitHub 仓库建立后执行 workflow_dispatch dry run。**
- [ ] **Step 4: 运行全量测试并提交 `ci: publish versioned windows releases`。**

## 完成门槛

- 更新检查只连接 GitHub，自动失败不打扰用户，手动失败明确可见，每日自动调用不超过一次。
- 源码仓库具备 MIT、中文用户说明、构建维护说明和自动化门禁。
- Release 包含 x64 安装器、非空版本说明和 SHA-256；真实仓库链接与工作流运行必须在用户提供/创建 GitHub 仓库后验证。
