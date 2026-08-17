# 训练桌历 v1 首版门禁

本文件按设计规格第 13 节逐项列出证据。`PASS` 只表示证据已在当前环境实际获得，`PENDING` 表示需要外部 Windows 环境或 GitHub 仓库。

| 规格要求 | 证据 | 状态 |
| --- | --- | --- |
| 核心两周交互、颜色、编辑、复制和设置 | Debug/Release 全量 xUnit 测试 | PASS |
| 当前用户安装、快捷方式、自启动、卸载数据选择 | [installer-results.md](installer-results.md) | PASS（当前机器） |
| Windows 10 22H2 和 Windows 11 x64 安装与基本工作流 | [windows-release-matrix.md](windows-release-matrix.md) | PENDING |
| 启动、CPU、工作集、保存延迟和体积门槛 | [release-performance-results.md](release-performance-results.md) | PASS（当前机器） |
| 导入导出、损坏文件、未知版本、非法颜色、写失败回滚、数据库损坏 | [data-recovery-results.md](data-recovery-results.md) | PASS（当前机器） |
| MIT 许可证、构建说明和用户说明 | [README.md](../../README.md)、[LICENSE](../../LICENSE)、[CONTRIBUTING.md](../../CONTRIBUTING.md) | PASS |
| 公开 GitHub 仓库源码与 CI | 当前分支无 remote | PENDING |
| GitHub Release 安装器、版本说明、SHA-256、在线更新 | [docs/releasing.md](../../docs/releasing.md)；真实仓库尚未提供 | PENDING |

## 当前结论

本地软件和可复现发布资产已完成，当前工作树可以交给维护者接收 GitHub 仓库。首版发布门禁不能在没有 Windows 10/11 测试环境和真实 GitHub 仓库的情况下标记为全部通过；这些是外部验证项，不应由当前机器结果推断。
