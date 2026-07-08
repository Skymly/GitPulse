# GitPulse — AI Agent Notes

本文件为在本仓库工作的 AI 编码助手提供上下文。**修改代码前请先阅读本文档与 [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md)。**

## 项目状态

| 项 | 说明 |
|----|------|
| **类型** | 个人项目（Skymly workspace） |
| **远程** | https://github.com/Skymly/GitPulse |
| **阶段** | M6 已发布于 `main`；**M7/M8 工作区进行中**（见 [docs/ROADMAP.md](docs/ROADMAP.md)、[docs/plans/MilestoneM7M8.md](docs/plans/MilestoneM7M8.md)） |
| **目的** | [Observables](https://github.com/Skymly/Observables) 的真实世界展示应用（声明式 RestAPI + R3）。非玩具 demo，作者日常使用的 GitHub 客户端。 |

## 技术栈

- **.NET 10** (LTS) + **.NET MAUI**
- **R3** 1.3.0+ + **R3Extensions.Maui**（`UseR3()`、`BindableReactiveProperty<T>`）
- **Observables.RestAPI.R3** + **Observables.Events.R3** 0.1.5+
- **CommunityToolkit.Mvvm**（`[RelayCommand]`）
- **Indiko.Maui.Controls.Markdown** 1.5.0
- **MinVer**、**Nuke**、**xunit.v3**

## 目标平台

- **Windows**（主）：`net10.0-windows10.0.19041.0`
- **Android**（次）：`net10.0-android`
- iOS / MacCatalyst：暂缓（见 [ADR-005](docs/adr/ADR-005-windows-first-platform-strategy.md)）

## 仓库结构

```
src/
  GitPulse.App/         — MAUI UI、DI、平台入口
  GitPulse.ViewModels/  — ViewModel（R3，无 MAUI）
  GitPulse.Core/        — 模型、抽象、Http/
  GitPulse.GitHubApi/   — IGitHubReposApi 声明
  GitPulse.Services/    — GitHubClientFactory、通知轮询
tests/GitPulse.Tests/
docs/                   — 文档驱动开发体系
build/                  — Nuke
```

分层与 PR 边界见 [docs/spec/Architecture.md](docs/spec/Architecture.md)。

## 跨模块 PR / Issue 边界

**每个 PR 只改一个模块**（或 Docs/Repo 元数据）：

| 模块 | 路径 |
|------|------|
| **App** | `src/GitPulse.App/` |
| **ViewModels** | `src/GitPulse.ViewModels/` |
| **Core** | `src/GitPulse.Core/` |
| **GitHubApi** | `src/GitPulse.GitHubApi/` |
| **Services** | `src/GitPulse.Services/` |
| **Tests** | `tests/GitPulse.Tests/` |
| **Docs / Repo** | `docs/`、`README.md`、`CONTRIBUTING.md`、`AGENTS.md`、`build/`、`.github/` |

## 构建与 CI

```powershell
./build.ps1 --target CiLib --configuration Release   # 库测试（跨平台）
./build.ps1 --target CiAll --configuration Release  # 含 App + format
```

详见 [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md)。

## Observables 展示要点

| 域 | 文档 |
|----|------|
| RestAPI（`IGitHubReposApi`） | [docs/spec/RestApi.md](docs/spec/RestApi.md)、[docs/design/RestApi.md](docs/design/RestApi.md) |
| Events / R3 UI | [docs/spec/Events.md](docs/spec/Events.md)、[docs/design/Events.md](docs/design/Events.md) |

**已知上游限制**（须在 Design Doc 中保持同步）：

1. **OBS3004** — 0.1.5 已修复 path + `[Body]`；分页仍用 `GitHubQueryHandler`（[ADR-006](docs/adr/ADR-006-github-query-handler-pagination.md)）
2. **SearchBar `.Events()` CS0122** — 手动 `Subject` 桥接（[ADR-007](docs/adr/ADR-007-manual-searchbar-event-bridge.md)）

## 认证

- PAT only；`ICredentialStore` 在 App/Platforms（[ADR-004](docs/adr/ADR-004-pat-auth-platform-credential-store.md)）
- `GitHubClientFactory` 设置 Bearer 与 GitHub API 版本头

## 路线图

里程碑 **M0–M12** 见 [docs/ROADMAP.md](docs/ROADMAP.md)。**不要在本文件重复完整表格**——以 ROADMAP 为唯一 backlog 源。

## 文档体系（文档驱动开发）

本仓库实行**文档驱动开发**，体系源自 [DesignPatterns](https://github.com/Skymly/DesignPatterns)。**先文档后代码**；完整规范见 [docs/DOCUMENTATION.md](docs/DOCUMENTATION.md)。

| 类型 | 目录 | 用途 |
|------|------|------|
| **RFC** | `docs/rfc/` | 新里程碑 / 破坏性 API 变更 |
| **ADR** | `docs/adr/` | 架构决策（不可变） |
| **Spec** | `docs/spec/` | 稳定契约 |
| **Design Doc** | `docs/design/` | 实现细节 |
| **Plan** | `docs/plans/` | 跨多 PR 任务 |
| **Review** | `docs/review/` | 评审记录 |
| **Roadmap** | `docs/ROADMAP.md` | 里程碑 backlog |

### Agent 文档工作流

| 场景 | Agent 行为 |
|------|-----------|
| 新里程碑 / 新 GitHub API 域 | 先 RFC + Plan；无则提示用户 |
| 改 `IGitHubReposApi` 公共面 | 确认 RFC；更新 Spec |
| 跨多 PR 任务 | 确认 `docs/plans/` 有 Plan |
| 实现 PR | 同步 Design Doc + `CHANGELOG.md` `[Unreleased]` |
| 新建文档 | 使用各目录 `_template.md` |
| 文档位置 | 维护者文档放在 `docs/`（根目录仅 `AGENTS.md`、`README.md` 等） |

## 约定

- C# latest；file-scoped namespaces；nullable enabled
- `async/await` 用于 I/O
- Issue / PR / Commit：**英语**；与用户对话默认**简体中文**
- 无 AI 工具名 in commits/PRs
- Central package management（App 项目除外，见 csproj 注释）
- Release 下 `TreatWarningsAsErrors`（Nuke `_build` 除外）

## Git 安全

- 不主动 `commit` / `push` 除非用户明确要求
- 不 force-push `main`
