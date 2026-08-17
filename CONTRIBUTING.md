# 贡献指南

## 本地流程

1. 在独立分支修改代码，保持提交聚焦。
2. 新行为先写失败测试，再实现最小行为；不要把测试改成只验证字符串存在。
3. 运行锁定还原、Debug/Release 全量测试和 `git diff --check`。
4. 修改 Windows 安装或发布脚本时，运行对应的契约测试和本机验证脚本。
5. 提交信息使用简洁的英文动词开头，例如 `feat:`、`fix:`、`test:`、`docs:`。

## 数据与隐私

不要提交 `%LOCALAPPDATA%\TrainingDeskCalendar` 下的数据库、设置、备份或日志。更新检查只允许访问 GitHub Releases 元数据，不得上传训练计划正文。

## 发布

版本由 `eng/Versions.props` 的 `VersionPrefix` 维护。Release 标签必须使用 `vMAJOR.MINOR.PATCH`，并由 GitHub Actions 生成 x64 安装器、SHA-256 校验文件和版本说明。
