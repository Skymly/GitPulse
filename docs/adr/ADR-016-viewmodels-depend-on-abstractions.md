# ADR-016: ViewModels 依赖抽象而非 Services 项目

| 字段 | 值 |
|------|-----|
| **状态** | Accepted |
| **日期** | 2026-09-14 |
| **关联 Issue** | [#511](https://github.com/Skymly/GitPulse/issues/511) |
| **取代** | [ADR-001](ADR-001-layered-solution-architecture.md) 中的依赖图与「Core 无 IO」表述 |

## 背景

ADR-001 画成 `App → ViewModels → (Services, GitHubApi) → Core`，并写 Core「无 UI/IO」。实现是依赖倒置：ViewModels 只引用 Core + GitHubApi；`IGitHubClientFactory` / `INotificationPoller` 等抽象在 Core，Services 提供实现。Core 已包含 `GitHubQueryHandler` 与 `NotificationToastCoordinator` 这类 IO 辅助，不是纯模型层。Architecture.md 的表格才是准的。

不要为了迎合 ADR-001 给 ViewModels 加 Services 项目引用。

## 决策

五项目分层保持不变，依赖方向改为：

- **GitPulse.Core** — 模型、抽象、分页/查询 HTTP 辅助（`GitHubQueryHandler`）、通知 Toast 协调。无 MAUI。
- **GitPulse.GitHubApi** — 声明式 REST 接口。
- **GitPulse.Services** — `GitHubClientFactory`、`NotificationPoller`（实现 Core 抽象）。
- **GitPulse.ViewModels** — R3 状态与命令；依赖 Core + GitHubApi；**不**引用 Services 或 MAUI。
- **GitPulse.App** — XAML、DI、平台凭据；组合以上全部。

依赖：`ViewModels → Core + GitHubApi`，`Services → Core + GitHubApi`，`App → 以上全部`。ViewModels 与 Services 不互相引用。

权威图以 [design/Architecture.md](../design/Architecture.md) 为准。

## 后果

- **正面**：ViewModels 可单测且不绑死 HTTP 实现；分层比 ADR-001 的图更干净。
- **负面**：读者若只看 ADR-001 会画错依赖；以此 ADR 与 Architecture.md 为准。

## 参考

- [ADR-001](ADR-001-layered-solution-architecture.md)（部分被取代）
- [design/Architecture.md](../design/Architecture.md)
