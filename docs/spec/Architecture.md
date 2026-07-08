# Spec: Architecture

> **版本**：Unreleased（目标 v0.1.0）
> **关联 Design Doc**：[design/Architecture.md](../design/Architecture.md)
> **关联 ADR**：[ADR-001](../adr/ADR-001-layered-solution-architecture.md)、[ADR-004](../adr/ADR-004-pat-auth-platform-credential-store.md)

## 范围

GitPulse 解决方案的项目划分、依赖方向与 PR 模块边界。

## 项目与依赖

| 项目 | TFM | 职责 | 依赖 |
|------|-----|------|------|
| GitPulse.Core | net10.0 | 模型、`ICredentialStore` 等抽象、`GitHubQueryHandler` | — |
| GitPulse.GitHubApi | net10.0 | `IGitHubReposApi` 声明 | Core |
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

## 不在范围内

- 微服务拆分、后端 BFF
- 离线优先 / 本地 Git 存储

## 兼容基线

- .NET 10 LTS
- Windows 10.0.17763+；Android API 21+
