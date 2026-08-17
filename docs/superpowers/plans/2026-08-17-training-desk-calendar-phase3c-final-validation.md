# 训练桌历 Phase 3C 最终 Windows 验收实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 对发布安装包执行性能、安装、数据安全和 Windows 桌面集成矩阵，只有全部阻断项有证据时才宣布首版完成。

**Architecture:** 自动测量脚本输出结构化 JSON，验证文档只从结果生成，不人工推断。每个 Windows/DPI 环境使用相同安装器和测试清单；无法在当前机器覆盖的环境保持未通过，等待真实环境执行。破坏性数据测试只使用独立测试用户或唯一临时应用数据目录。

**Tech Stack:** PowerShell、ETW/进程计数器、Windows 10/11 x64、Inno Setup 安装包、xUnit。

---

### Task 1：发布包性能测量脚本

**Files:**
- Create: `scripts/measure-release.ps1`
- Create: `tests/TrainingDeskCalendar.App.Tests/Release/PerformanceScriptContractTests.cs`
- Create: `docs/validation/release-performance-results.md`

- [ ] **Step 1: 写脚本契约测试，要求至少 5 次冷启动、ready-file 计时、60 秒空闲 CPU、30 个工作集样本、10 次 250 ms 自动保存延迟、安装包和安装目录大小。**
- [ ] **Step 2: 实现结构化 JSON 输出并固定失败门槛：冷启动最大 2 秒、平均空闲 CPU 小于 0.5%、工作集最大 200 MiB、安装包小于 80 MiB、安装目录小于 150 MiB、保存最大 300 ms。**
- [ ] **Step 3: 在 100% 和 150% DPI 分别运行；任何样本失败则脚本非零退出，文档记录原始最大值和平均值。**
- [ ] **Step 4: 提交 `test: measure release performance gates`。**

### Task 2：数据与恢复破坏性验收

**Files:**
- Create: `scripts/test-data-recovery.ps1`
- Create: `docs/validation/data-recovery-results.md`

- [ ] **Step 1: 在唯一临时数据根验证正常导出/完整导入、损坏 JSON、未知版本、非法颜色、数据库损坏、设置写失败和恢复副本回滚。**
- [ ] **Step 2: 每个失败场景记录数据库与设置文件 SHA-256 前后值，证明被拒绝的导入不改变现有数据；日志扫描确认不含计划正文。**
- [ ] **Step 3: 运行自动化和脚本，提交 `test: validate destructive recovery scenarios`。**

### Task 3：Windows 桌面与安装矩阵

**Files:**
- Modify: `docs/validation/desktop-prototype-manual-checks.md`
- Create: `docs/validation/windows-release-matrix.md`

- [ ] **Step 1: 对 Windows 10 22H2 x64 100% DPI 执行安装、快捷方式、自启动、单实例、托盘、拖动缩放、锁定、重启恢复、Win+D、Explorer 重启、休眠恢复、降级和卸载数据选择。**
- [ ] **Step 2: 对 Windows 11 24H2 x64 150% DPI 重复相同清单。**
- [ ] **Step 3: 对发布时最新稳定 Windows 11 x64 重复相同清单，并至少加入双显示器、断开保存显示器和混合 DPI。**
- [ ] **Step 4: 每项记录 OS build、DPI、显示器布局、安装包 SHA-256、实际结果和证据路径；只有实际执行成功才勾选。**
- [ ] **Step 5: 提交 `test: record windows release matrix`。**

### Task 4：GitHub 发布端到端验收

**Files:**
- Create: `docs/validation/github-release-results.md`
- Modify: `README.md`

- [ ] **Step 1: 在用户指定的公开 GitHub 仓库运行 CI，确认所有门禁通过且默认分支无未提交生成物。**
- [ ] **Step 2: 创建与 `Versions.props` 一致的版本标签，确认 Release 自动包含安装器、SHA-256 和版本说明。**
- [ ] **Step 3: 从干净 Windows 用户下载 Release 安装器，核对散列并完成安装；用低版本测试构建验证更新提示和下载页。**
- [ ] **Step 4: 将真实仓库和 Release 链接写入 README 与验证文档，提交 `docs: record github release validation`。**

### Task 5：首版完成审计

**Files:**
- Create: `docs/validation/v1-release-gate.md`

- [ ] **Step 1: 逐条对照设计规格第 13 节，链接对应自动化、安装器、Windows 矩阵、性能、恢复和 GitHub Release 证据。**
- [ ] **Step 2: 运行 Debug/Release 全量测试、安装器契约、打包、校验值、`git diff --check` 和工作树状态检查。**
- [ ] **Step 3: 若任何 Windows 环境、性能门槛或 GitHub 资产没有证据，保持首版门禁失败并列出具体阻断项；不得用当前开发机结果替代。**
- [ ] **Step 4: 全部证据通过后提交 `release: gate training desk calendar v1`。**

## 完成门槛

- 三个指定 Windows 环境的完整矩阵全部通过。
- 所有性能硬门槛和数据恢复破坏性测试通过。
- 真实公开 GitHub Release 可下载、散列匹配、安装和更新提示可用。
- 未通过项目为零时，首版才可声明完成。
