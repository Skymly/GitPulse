# Design Doc: Architecture

> **版本**：Unreleased（目标 v0.1.0）
> **关联 ADR**：[ADR-001](../adr/ADR-001-layered-solution-architecture.md)、[ADR-004](../adr/ADR-004-pat-auth-platform-credential-store.md)、[ADR-008](../adr/ADR-008-split-github-search-api-interface.md)、[ADR-009](../adr/ADR-009-split-github-actions-api-interface.md)、[ADR-010](../adr/ADR-010-windows-tray-presence-and-toast.md)、[ADR-011](../adr/ADR-011-android-m11-daily-usable-phone.md)

## 概述

GitPulse 是五项目 MAUI 解决方案；ViewModel 与 UI 分离以支持 `CiLib` 无头测试。

## 范围

项目划分、依赖方向与 PR 模块边界。

## 项目与依赖

| 项目 | TFM | 职责 | 依赖 |
|------|-----|------|------|
| GitPulse.Core | net10.0 | 模型、`ICredentialStore` / `IAppPresence` / `IToastNotifier` 等抽象、`NotificationToastCoordinator`、`GitHubQueryHandler` | — |
| GitPulse.GitHubApi | net10.0 | `IGitHubReposApi`、`IGitHubSearchApi`、`IGitHubActionsApi` 声明 | Core |
| GitPulse.Services | net10.0 | `GitHubClientFactory`、`INotificationPoller` | Core, GitHubApi |
| GitPulse.ViewModels | net10.0 | ViewModel、R3 状态 | Core, GitHubApi |
| GitPulse.App | net10.0-* | MAUI UI、平台凭据、DI | 以上全部 |

## 模块 PR 边界

每个 PR **只改一个模块**（或 Solution Items：`build/`、`.github/`、`docs/`、`AGENTS.md`）：

| 模块 | 路径 |
|------|------|
| App | `src/GitPulse.App/` |
| ViewModels | `src/GitPulse.ViewModels/` |
| Core | `src/GitPulse.Core/` |
| GitHubApi | `src/GitPulse.GitHubApi/` |
| Services | `src/GitPulse.Services/` |
| Tests | `tests/GitPulse.Tests/` |
| Docs / Repo | `docs/`、`README.md`、`CONTRIBUTING.md`、`AGENTS.md`、`build/`、`.github/` |

## 不变量

1. Core 不引用 MAUI、Observables 生成产物以外的 UI 包。
2. ViewModels 不引用 `Microsoft.Maui.*`。
3. 平台凭据实现仅存在于 App 的 `Platforms/`。

## 实现概览

### DI（`MauiProgram.cs`）

- Singleton：`ICredentialStore`、`IGitHubClientFactory`、`IBrowserLauncher`、`INotificationPoller`、`IAppPresence`、`IToastNotifier`、`NotificationToastCoordinator`、`NotificationToastHost`（ADR-010）
- Transient：各 ViewModel 与 Page（Shell `GoToAsync` 解析）

### 导航

- Shell TabBar：Repos、Notifications、Search、Settings
- 详情页经 `ShellContent` + query 参数（`owner`、`repo`、`number`）
- 托盘 / Toast 激活：显示主窗并 `GoToAsync("//NotificationsPage")`

### 平台

- Windows：DPAPI、`WindowHelpers` Mica/Acrylic；Tray Presence（`AppWindow.Closing` 取消关闭后隐藏）与 `AppNotificationManager` Toast（ADR-010）
- Android（M11 / ADR-011）：`SecureStorage`；托盘/Toast / `IAppPresence` 为空操作；**无**出应用系统通知（v0.1.0 前）。竖屏手机日用可用：同一套 XAML 就地修补；CI 增加 `net10.0-android` 编译门禁；签名/AAB 属 M12。

## 设计权衡

- **凭据在 App 而非 Services**：平台 API 不可在 net10.0 类库中统一抽象。
- **按域拆分声明式接口**：仓库资源 → `IGitHubReposApi`；Search → `IGitHubSearchApi`（ADR-008）；Actions → `IGitHubActionsApi`（ADR-009）。共享认证 `HttpClient`。

## 已知局限

- App 项目未纳入 `CiLib`（需 MAUI workload）；CI 分 `Ci` / `CiLib` 两条路径。
- M11 起应有 Android 编译门禁（[#32](https://github.com/Skymly/GitPulse/issues/32)）；功能仍靠本机冒烟，无模拟器 UI 自动化门禁。

## 不在范围内

- 微服务拆分、后端 BFF
- 离线优先 / 本地 Git 存储
- Android 系统通知 / 出应用提醒（ADR-011；v0.1.0 之后再议）

## 兼容基线

- .NET 10 LTS
- Windows 10.0.17763+；Android API 21+

## 参考

- [DEVELOPMENT.md](../DEVELOPMENT.md)
