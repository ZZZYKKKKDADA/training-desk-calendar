# 训练桌历 v1 首版门禁

本文件按设计规格第 13 节逐项列出证据。`PASS` 只表示证据已经实际获得，`PENDING` 表示仍需要外部 Windows 环境。

| 规格要求 | 证据 | 状态 |
| --- | --- | --- |
| 核心两周交互、颜色、编辑、复制和设置 | Debug/Release 全量 xUnit 测试 | PASS |
| 当前用户安装、快捷方式、自启动、卸载数据选择 | [installer-results.md](installer-results.md) | PASS（当前机器） |
| Windows 10 22H2 和 Windows 11 x64 安装与基本工作流 | [windows-release-matrix.md](windows-release-matrix.md) | PENDING |
| 启动、CPU、工作集、保存延迟和体积门槛 | [release-performance-results.md](release-performance-results.md) | PASS（当前机器） |
| 导入导出、损坏文件、未知版本、非法颜色、写失败回滚、数据库损坏 | [data-recovery-results.md](data-recovery-results.md) | PASS（当前机器） |
| MIT 许可证、构建说明和用户说明 | [README.md](../../README.md)、[LICENSE](../../LICENSE)、[CONTRIBUTING.md](../../CONTRIBUTING.md) | PASS |
| 公开 GitHub 仓库源码与 CI | [github-release-results.md](github-release-results.md)；`main` CI run `32093223682` | PASS |
| GitHub Release 安装器、版本说明、SHA-256、在线更新 | [github-release-results.md](github-release-results.md)；Release run `32093326360` | PASS |

## 当前结论

源码、CI、GitHub Release、安装器和 SHA-256 校验资产已经完成真实仓库验证。首版发布门禁仍不能标记为全部通过，因为 Windows 10/11 目标环境的完整人工矩阵尚未执行；当前机器结果不能替代这些外部环境证据。
