# ADR-001: 分层解决方案架构

| 字段 | 值 |
|------|-----|
| **状态** | Accepted |
| **日期** | 2026-03-01 |
| **关联 RFC** | 无 — 项目骨架阶段直接决策 |

## 背景

GitPulse 需要同时满足：可测试的 ViewModel、声明式 GitHub API 展示、MAUI 多平台 UI，且避免「上帝项目」。

## 决策

采用五项目分层：

- **GitPulse.Core** — 模型与抽象，无 UI/IO
- **GitPulse.GitHubApi** — `IGitHubReposApi` 等声明式接口
- **GitPulse.Services** — `GitHubClientFactory`、通知轮询
- **GitPulse.ViewModels** — R3 状态与命令，不引用 MAUI
- **GitPulse.App** — XAML、DI、平台实现（DPAPI / SecureStorage）

依赖方向：App → ViewModels → (Services, GitHubApi) → Core。

## 后果

- **正面**：ViewModel 可在 `net10.0` 上单元测试；Observables 展示与 UI 解耦。
- **负面**：跨层变更需多 PR 或明确 Plan；平台代码留在 App 层。

## 参考

- [spec/Architecture.md](../spec/Architecture.md)
- [design/Architecture.md](../design/Architecture.md)
