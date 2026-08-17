# Windows Release Validation Matrix

本表只勾选实际执行并保留证据的环境；当前机器的结果不能替代其他 Windows 版本、DPI 或多显示器环境。

| 环境 | DPI | 安装/升级/卸载 | 性能脚本 | 桌面/托盘/Explorer/休眠/多显示器手工项 | 状态 |
| --- | ---: | --- | --- | --- | --- |
| Windows 10 22H2 x64 | 100% | 未执行 | 未执行 | 未执行 | PENDING |
| Windows 11 24H2 x64 | 150% | 未执行 | 未执行 | 未执行 | PENDING |
| 当前 Windows 11 Pro x64 build 26200 | 未记录 | [x] 当前用户安装器 E2E | [x] 5 次新路径性能门禁 | 未执行完整人工矩阵 | PARTIAL |
| 发布时最新稳定 Windows 11 x64 | 待记录 | 未执行 | 未执行 | 未执行 | PENDING |

## 当前机器证据

- 安装器验证：[installer-results.md](installer-results.md)
- 发布性能验证：[release-performance-results.md](release-performance-results.md)
- 数据恢复验证：[data-recovery-results.md](data-recovery-results.md)
- 当前机器安装器 SHA-256：`65c4e32aae47a25d6443a3d4bc3f67cc3c8a5e56c4d134e39b075513148015ff`
- 当前机器性能结果：最大启动 1337.3ms、最大工作集 186.4MiB、最大空闲 CPU 0.053%、最大自动保存 285.2ms；安装目录 131.88MiB。

## 未决手工清单

在每个目标环境仍需实际记录：无任务栏按钮、普通窗口覆盖、Win+D、Explorer 重启后 5 秒内重连、桌面挂载失败降级、拖动缩放、锁定、显示器断开恢复、休眠恢复、单实例和托盘命令。最新稳定 Windows 11 还需双显示器、断开保存显示器和混合 DPI。

未提供这些环境前，首版 Windows 矩阵门禁保持未通过。
