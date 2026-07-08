# Design Doc: Architecture

> **关联 Spec**：[spec/Architecture.md](../spec/Architecture.md)
> **关联 ADR**：[ADR-001](../adr/ADR-001-layered-solution-architecture.md)、[ADR-004](../adr/ADR-004-pat-auth-platform-credential-store.md)

## 概述

五项目 MAUI 解决方案；ViewModel 与 UI 分离以支持 `CiLib` 无头测试。

## 实现概览

### DI（`MauiProgram.cs`）

- Singleton：`ICredentialStore`、`IGitHubClientFactory`、`IBrowserLauncher`、`INotificationPoller`
- Transient：各 ViewModel 与 Page（Shell `GoToAsync` 解析）

### 导航

- Shell TabBar：Repos、Notifications、Settings
- 详情页经 `ShellContent` + query 参数（`owner`、`repo`、`number`）

### 平台

- Windows：DPAPI、`WindowHelpers` Mica/Acrylic（`App.xaml.cs` `HandlerChanged`）
- Android：`SecureStorage`

## 设计权衡

- **凭据在 App 而非 Services**：平台 API 不可在 net10.0 类库中统一抽象。
- **单 `IGitHubReposApi` 巨石接口**：展示用简单；未来可按域拆分（Search、Actions）并走 RFC。

## 已知局限

- App 项目未纳入 `CiLib`（需 MAUI workload）；CI 分 `Ci` / `CiLib` 两条路径。

## 参考

- [DEVELOPMENT.md](../DEVELOPMENT.md)
