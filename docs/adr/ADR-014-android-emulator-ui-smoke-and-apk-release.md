# ADR-014: Android 模拟器 UI 冒烟解锁签名 APK 公开发版

| 字段 | 值 |
|------|-----|
| **状态** | Accepted |
| **日期** | 2026-07-31 |
| **关联 Issue** | [#67](https://github.com/Skymly/GitPulse/issues/67) |
| **取代** | [ADR-013](ADR-013-v0.1.0-windows-only-github-release.md)（就「后续版本何时可挂 Android APK / 冒烟形态」而言；**v0.1.0 仍为仅 Windows**） |

## 背景

ADR-013 将 v0.1.0 定为仅 Windows publish zip，并把 Android 签名 APK 推迟到「有侧载冒烟条件」之后；当时默认需要真机。v0.1.0 已按该契约发布。维护者仍无合适真机，但可用 Android 模拟器做可重复 UI 自动化。若继续把真机当作唯一门槛，则公开发 APK 会无限期卡住，尽管 `PublishAndroid*` 与 Secrets 契约已就绪。

Windows 侧已有本机 FlaUI 短冒烟（cut 清单必过、不进 `CiLib`）。Android 需要对齐同一角色的冒烟，而不是先做 CI 硬门禁。

## 决策

- **取代 ADR-013 中「后续版本须真机侧载才可挂 APK」的表述**：自本决策起，**Android Emulator UI Smoke**（见 [CONTEXT.md](../CONTEXT.md)）视为合格的 Android cut 门槛。
- **目的**：解锁公开发 **签名 APK** Release Artifact；不是先把 Appium 做成 `CiLib` / `release` job 硬失败。
- **运行环境**：**模拟器为主**（默认一台 **API 34+ 竖屏手机 AVD**）。真机抽查可选，不挡发版。
- **与流水线关系**：与 Windows FlaUI 相同——**cut 清单必过**；默认 **不**把 UI 冒烟绑进 tag `release` 硬门禁。
- **场景范围**：对齐 Windows 短冒烟（启动、主 Tab、存 PAT、一等公民页可开不崩）。**不含** IME / Diff / merge 精修自动化。
- **技术栈**：Appium 2 + UiAutomator2 + NUnit；新建 `tests/GitPulse.AndroidUITests`（不进 `CiLib`）；复用 `GITPULSE_UI_TEST_HOST` / `UiTestHostPage`；`AutomationId` 与 Windows 对齐。
- **被测包**：日常可用 debug；**cut / 挂 APK 前必须**对 **签名 APK**（`PublishAndroid` 产物）再跑一遍。
- **下一公开 cut**：**`v0.1.1`** — GitHub Release 同时挂 **Windows publish zip + 签名 Android APK**（仍仅 GitHub Releases；无商店 / Authenticode / AAB）。
- **明确不做（本切片）**：真机必过、平板矩阵、IME 自动化、商店上架、AAB、OAuth、Android 出应用通知、产品新功能、UI 冒烟进 `CiLib`。

## 后果

- **正面**：无真机也可完成 Android 公开发版；与已有 Windows cut 习惯一致。
- **正面**：`PublishAndroid*` / Secrets 契约可被真正用上。
- **负面**：模拟器不等于厂商 ROM / 全面屏手势；真机问题可能晚发现。
- **文档**：ROADMAP 开 M13；DEVELOPMENT 增补 Android UI 自动化与 v0.1.1 cut 清单；Release Artifact 用语不再要求「真机侧载」。

## 参考

- [ADR-005](ADR-005-windows-first-platform-strategy.md)
- [ADR-011](ADR-011-android-m11-daily-usable-phone.md)
- [ADR-013](ADR-013-v0.1.0-windows-only-github-release.md)（Superseded for post-v0.1.0 Android attach）
- [CONTEXT.md](../CONTEXT.md)
- [ROADMAP.md](../ROADMAP.md)
- [DEVELOPMENT.md](../DEVELOPMENT.md)
