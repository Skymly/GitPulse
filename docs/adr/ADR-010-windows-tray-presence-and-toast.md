# ADR-010: Windows 托盘驻留与 Toast 的平台抽象

| 字段 | 值 |
|------|-----|
| **状态** | Accepted |
| **日期** | 2026-07-20 |
| **关联 Issue** | [#19](https://github.com/Skymly/GitPulse/issues/19) |

## 背景

M10 剩余工作为 Windows 系统托盘与 Toast。产品决策是：托盘提供关窗后的驻留；Toast 在主窗隐藏时汇总提示**新的** GitHub Notification；托盘驻留期间通知轮询必须继续。实现上需在「全部塞进 App/Platforms」与「放入 Core 抽象」之间取舍，并与现有 `ICredentialStore` / `IBrowserLauncher` 分层一致。

## 决策

- 在 **Core** 引入薄平台抽象（例如应用驻留 / Toast 通知），**不**依赖 MAUI 或 WinUI 类型。
- **Windows** 实现位于 `App/Platforms`：托盘图标与菜单（Open、Notifications、Exit）、关窗隐藏到托盘、Toast；Android 实现为空操作。
- App 层协调：订阅 `INotificationPoller`，在主窗隐藏时用通知 Id 差集检测 New Notification，每轮最多一条汇总 Toast；点击 Toast 或菜单「Notifications」显示主窗并导航到 Notifications Tab。
- 托盘驻留期间轮询**继续**；仅进程 Exit 时停止。这显式修正「进后台即 Stop」对 Windows 托盘场景不适用的语义。
- 本切片不做托盘未读角标；不做 Actions 状态 Toast。

## 后果

- **正面**：与凭据/浏览器启动器同一分层；ViewModels 仍可无头测试；Android 不被迫实现托盘。
- **正面**：关窗后仍能收到新通知 Toast，与产品目标一致。
- **负面**：Windows 在托盘态持续消耗 Notifications API 额度；须接受相对前台-only 停轮询的更高调用量。
- **负面**：须调整现有 poller 前台/后台生命周期挂钩（至少 Windows）。

## 参考

- [CONTEXT.md](../CONTEXT.md)
- [ADR-004](ADR-004-pat-auth-platform-credential-store.md)
- [ADR-005](ADR-005-windows-first-platform-strategy.md)
- [ADR-009](ADR-009-split-github-actions-api-interface.md)
- [ROADMAP.md](../ROADMAP.md)
