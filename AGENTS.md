# GitPulse — AI Agent Notes

本文件为在本仓库工作的 AI 编码助手提供上下文。**修改代码前请先阅读本文档与 [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md)。**

## 项目状态

| 项 | 说明 |
|----|------|
| **类型** | 个人项目（Skymly workspace） |
| **远程** | https://github.com/Skymly/GitPulse |
| **阶段** | **M10 已合并于 `main`**；**M11 进行中**（ADR-011，见 [docs/ROADMAP.md](docs/ROADMAP.md)、[#30](https://github.com/Skymly/GitPulse/issues/30)） |
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
docs/                   — ADR、设计文档与路线图
build/                  — Nuke
```

分层与 PR 边界见 [docs/design/Architecture.md](docs/design/Architecture.md)。

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
./build.ps1 --target CiAndroid --configuration Release  # Android App 编译门禁
./build.ps1 --target CiAll --configuration Release  # 含 App（Windows+Android）+ format
```

详见 [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md)。

## Observables 展示要点

| 域 | 文档 |
|----|------|
| RestAPI（`IGitHubReposApi`） | [docs/design/RestApi.md](docs/design/RestApi.md) |
| Events / R3 UI | [docs/design/Events.md](docs/design/Events.md) |

**已知上游限制**（须在 Design Doc 中保持同步）：

1. **OBS3004** — 0.1.5 已修复 path + `[Body]`；分页仍用 `GitHubQueryHandler`（[ADR-006](docs/adr/ADR-006-github-query-handler-pagination.md)）
2. **SearchBar `.Events()` CS0122** — 手动 `Subject` 桥接（[ADR-007](docs/adr/ADR-007-manual-searchbar-event-bridge.md)）

## 认证

- PAT only；`ICredentialStore` 在 App/Platforms（[ADR-004](docs/adr/ADR-004-pat-auth-platform-credential-store.md)）
- `GitHubClientFactory` 设置 Bearer 与 GitHub API 版本头

## 路线图

里程碑 **M0–M12** 见 [docs/ROADMAP.md](docs/ROADMAP.md)。**不要在本文件重复完整表格**——以 ROADMAP 为唯一 backlog 源。

## 文档体系

文档约定见 [docs/DOCUMENTATION.md](docs/DOCUMENTATION.md)。

| 载体 | 位置 | 用途 |
|------|------|------|
| **ADR** | `docs/adr/` | 不应随讨论漂移的架构决策；Accepted 后仅可 Supersede |
| **Design Doc** | `docs/design/` | 子系统的 API、模型、不变量、实现与权衡 |
| **Context / glossary** | `docs/CONTEXT.md` | 领域术语（ubiquitous language） |
| **Roadmap** | `docs/ROADMAP.md` | 宏观规划与 backlog 排序 |
| **Issue / PR / Release** | GitHub | 任务追踪、审查与版本历史 |

### Agent 文档工作流

| 场景 | Agent 行为 |
|------|-----------|
| 破坏性 API / 模型或跨层架构变更 | 创建或更新 ADR，并更新对应 Design Doc |
| API、模型或实现变更 | 随代码 PR 更新对应 Design Doc |
| ROADMAP 变更 | 完成项移入「已完成（归档）」；新增项放入对应章节 |
| CHANGELOG | 用户可见变更在 `[Unreleased]` 下添加条目 |
| 文档目录 | 不在 `docs/` 之外创建维护者文档（根目录 `AGENTS.md`、`README.md`、`CONTRIBUTING.md`、`CHANGELOG.md` 除外） |

### 子系统文档

| 子系统 | Design Doc |
|--------|------------|
| 解决方案架构 | [Architecture.md](docs/design/Architecture.md) |
| Observables RestAPI | [RestApi.md](docs/design/RestApi.md) |
| Events / R3 UI | [Events.md](docs/design/Events.md) |

---

## Git / Issue / PR / Commit

- **语言（权威）**：Issue / PR / Commit **一律英语**；与用户对话默认**简体中文**。本条为权威表述，**覆盖** `docs/DEVELOPMENT.md`、`CONTRIBUTING.md` 中任何「中英文均可」的旧措辞。
- 分支：功能 `feature/<short-description>`、修复 `fix/<short-description>`；提交信息祈使句、说明 **why**。
- **每个 PR 只改一个模块**（边界见上文「跨模块 PR / Issue 边界」）。
- PR 模板：[`.github/pull_request_template.md`](.github/pull_request_template.md)。
- **禁止**在 Commit / PR 中提及 AI / Agent 工具。
- **不主动** `commit` / `push` / 发版，除非用户明确要求；不 force-push `main`。

编码约定：C# latest；file-scoped namespaces；nullable enabled；`async/await` 用于 I/O；Central package management（App 项目除外）；Release 下 `TreatWarningsAsErrors`（Nuke `_build` 除外）。

---

## 澄清与规范

Agent 行为准则——与「与用户沟通」并行生效：

1. **用户表述不清楚时，立刻询问**：不要基于猜测继续工作。用聚焦的问题（而非开放式提问）澄清意图，提供 2–4 个具体选项供用户选择。
2. **用户表述不合理时，立刻指出并给出建议**：包括但不限于——违反已有 ADR（如 ViewModel 引用 MAUI、Core 依赖 App、平台凭据实现下沉到 Services）、未记录 ADR 的破坏性修改 `IGitHubReposApi`、跳过测试（`CiLib` / 相关 ViewModel 测试）、单 PR 混合多个模块、破坏分层依赖方向、在 App 之外绕过 `ICredentialStore` 存 PAT、过度设计。指出问题时必须说明**为什么不合理**，并给出合理替代方案。
3. **不要盲目执行**：即使能「做到」用户要求的事，如果认为方向有误，应先提出异议，等待用户确认后再动手。
4. **发现矛盾时主动报告**：如果用户的新要求与已有 ADR / `AGENTS.md` 规则冲突，指出冲突点，由用户决定是否更新规则或调整需求（ADR 变更须走 Supersede 流程，见 [docs/DOCUMENTATION.md](docs/DOCUMENTATION.md)）。

## 与用户沟通

- **最小 diff**、匹配现有风格；**不主动** `commit` / `push` / 发版，除非用户明确要求。
- 中文解释权衡；代码标识符与对外文档默认英语。

## 待用户决策项（非阻塞）

- v0.1.0 发版时机与功能裁剪范围（当前路线至 M12，见 [docs/ROADMAP.md](docs/ROADMAP.md)）。
- GitHub App OAuth 是否在 v0.1.0 之后纳入（当前 ADR-004 为 PAT only）。
- Android 出应用通知是否在 v0.1.0 之后纳入（M11 / ADR-011 明确不做）。
