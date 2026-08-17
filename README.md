# 训练桌历

训练桌历是一个面向 Windows 10/11 x64 的轻量 WPF 桌面组件，用连续两周的日历展示健身计划。计划、完成状态和颜色保存在本机，不上传训练内容。

## 功能

- 连续两周日历视图，可前后翻页并回到今天。
- 每天计划可原位编辑、完成/取消完成，支持固定颜色选择。
- 浅色/深色背景、透明度、位置尺寸和锁定设置。
- 托盘显示/隐藏、退出、锁定和开机自启动开关。
- 当前用户桌面和开始菜单快捷方式；不需要管理员权限或预装 .NET。
- 数据导出与导入。导入前会刷新当前编辑，失败时保留原数据。
- GitHub Releases 更新检查只读取公开版本信息，不读取或上传训练计划。

## 安装

下载 Release 中的 `TrainingDeskCalendar-Setup-0.1.0-x64.exe`，双击安装。安装位置默认是当前用户的 `%LOCALAPPDATA%\Programs\TrainingDeskCalendar`，不会写入 `Program Files` 或 HKLM，也不会弹出管理员权限提示。

安装完成后默认启用当前用户开机自启动，可以在设置页或托盘菜单关闭。桌面快捷方式和开始菜单入口均显示为“训练桌历”。

当前工作树尚未绑定具体 GitHub 账号或仓库；收到维护者提供的仓库后，再配置 CI 注入的仓库 URL 并创建 Release。

## 数据与卸载

训练计划、设置和恢复副本位于 `%LOCALAPPDATA%\TrainingDeskCalendar`。数据只保存在当前 Windows 用户目录。

卸载默认保留上述个人数据，同时删除程序、快捷方式和自启动项。若确定不再保留数据，在卸载窗口勾选删除个人数据；静默部署可显式传入 `/DELETEUSERDATA`。删除逻辑只接受精确的当前用户应用数据目录。

## 构建

需要 .NET 10 SDK、Windows x64 和 Inno Setup 6。开发构建和测试：

```powershell
dotnet restore TrainingDeskCalendar.sln --runtime win-x64 --locked-mode
dotnet test TrainingDeskCalendar.sln --configuration Debug --no-restore
dotnet test TrainingDeskCalendar.sln --configuration Release --no-restore
```

生成未压缩 self-contained single-file Windows payload：

```powershell
powershell -ExecutionPolicy Bypass -File scripts/package-windows.ps1
```

生成当前用户安装器并执行本机安装/升级/卸载验证：

```powershell
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" /Qp installer/TrainingDeskCalendar.iss
powershell -ExecutionPolicy Bypass -File scripts/test-installer.ps1
```

发布前还应执行 `scripts/measure-release-payload.ps1`，确认五次 fresh-path 启动、工作集、安装包和安装目录门槛。

Phase 3 最终本地验收还可执行 `scripts/measure-release.ps1` 和 `scripts/test-data-recovery.ps1`；结果分别写入 `docs/validation/release-performance-results.md` 和 `docs/validation/data-recovery-results.md`。Windows 版本矩阵见 `docs/validation/windows-release-matrix.md`，未实际执行的环境保持待验证。

## 维护

提交前请阅读 [CONTRIBUTING.md](CONTRIBUTING.md)；发布步骤见 [docs/releasing.md](docs/releasing.md)。许可证为 [MIT](LICENSE)。
