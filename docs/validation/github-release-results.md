# GitHub Release 验证结果

验证日期：2026-08-18

## 仓库与自动化

- 公开仓库：[ZZZYKKKKDADA/training-desk-calendar](https://github.com/ZZZYKKKKDADA/training-desk-calendar)
- 验证提交：`1c9c31eed60c0899cd50748eee53dc766c08c2fa`
- `main` CI：[run 32093223682](https://github.com/ZZZYKKKKDADA/training-desk-calendar/actions/runs/32093223682)，结论 `success`
- `v0.1.0` Release 工作流：[run 32093326360](https://github.com/ZZZYKKKKDADA/training-desk-calendar/actions/runs/32093326360)，结论 `success`
- Release：[训练桌历 v0.1.0](https://github.com/ZZZYKKKKDADA/training-desk-calendar/releases/tag/v0.1.0)，非草稿、非预发布

## 发布资产

| 文件 | 字节数 | SHA-256 |
| --- | ---: | --- |
| `TrainingDeskCalendar-Setup-0.1.0-x64.exe` | 43,795,604 | `33a21a1b1a668dba438ad670ddd4e7beed784be168c4592fa80302213c6288fc` |
| `TrainingDeskCalendar-Setup-0.1.0-x64.sha256.txt` | 108 | `9eb69379945cac41b54cccb217aa3e1b04714848f2e1aa524922d3e996d3fba6` |
| `TrainingDeskCalendar-Setup-0.1.0-x64.sha256.json` | 295 | `734c48ef2d59831427d148d7bdc43999fe9edfd012914d1d33cc7619b6f3ef10` |

安装器从 GitHub Release 独立下载后重新计算 SHA-256。实际文件哈希与 GitHub 资产 digest、文本校验文件和 JSON 校验文件一致；JSON 中的文件名和字节数也与下载资产一致。

## 边界

本记录验证公开仓库、GitHub Actions、Release 元数据和发布资产完整性。Windows 10/11 的安装、Explorer、休眠、多显示器和混合 DPI 人工矩阵仍按 [windows-release-matrix.md](windows-release-matrix.md) 保持 `PENDING` 或 `PARTIAL`。
