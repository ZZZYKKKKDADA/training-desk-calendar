# 发布训练桌历

本文描述为 [ZZZYKKKKDADA/training-desk-calendar](https://github.com/ZZZYKKKKDADA/training-desk-calendar) 创建 Windows x64 Release 的维护流程。推送版本标签后，以下流程由 `.github/workflows/release.yml` 自动执行。

## 版本与标签

1. 修改 `eng/Versions.props` 中的 `VersionPrefix`。
2. 顺序执行锁定还原、Debug/Release 测试和 `git diff --check`。
3. 创建与版本完全一致的标签，例如 `VersionPrefix=0.1.0` 时使用 `v0.1.0`。

Release 工作流会用 `scripts/validate-release-tag.ps1` 校验标签，标签不是 `vMAJOR.MINOR.PATCH` 或与 `VersionPrefix` 不一致时会停止。

## 本地构建资产

在 Windows x64、.NET 10 SDK 和 Inno Setup 6 环境中运行：

```powershell
dotnet restore TrainingDeskCalendar.sln --runtime win-x64 --locked-mode
dotnet test TrainingDeskCalendar.sln --configuration Release --no-restore
powershell -ExecutionPolicy Bypass -File scripts/package-windows.ps1 `
  -RepositoryUrl 'https://github.com/ZZZYKKKKDADA/training-desk-calendar'
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" /Qp installer\TrainingDeskCalendar.iss
```

`package-windows.ps1` 生成未压缩 self-contained single-file payload，并写入 `package-manifest.json`。`RepositoryUrl` 会被写入应用元数据，供设置页和更新检查使用；不传入时应用保持离线的未配置仓库状态。

生成 SHA-256 校验文件：

```powershell
powershell -ExecutionPolicy Bypass -File scripts/write-checksums.ps1 `
  -Path 'artifacts\installer\TrainingDeskCalendar-Setup-0.1.0-x64.exe' `
  -OutputPath 'artifacts\installer\TrainingDeskCalendar-Setup-0.1.0-x64.sha256.txt'
```

## GitHub Release

推送提交和标签后，GitHub Actions 会：

- 在 Windows runner 上验证标签并执行 Release 测试；
- 使用 `https://github.com/${{ github.repository }}` 注入仓库地址；
- 构建当前用户 Inno Setup 安装器；
- 生成文本和 JSON 两种 SHA-256 校验文件；
- 使用 `gh release create` 发布安装器、校验文件和中文说明。

安装器文件名和校验文件名从 `vMAJOR.MINOR.PATCH` 标签动态派生；不需要为每次版本发布手工修改工作流中的文件名。

工作流使用 GitHub 自动提供的 `GITHUB_TOKEN`，不需要把个人账号令牌写入仓库。`v0.1.0` 的 CI、Release 工作流、安装器和校验文件已经在真实仓库验证，证据见 [github-release-results.md](validation/github-release-results.md)。
