# GitPulse — AI Agent Notes

GitPulse 是 [Observables](https://github.com/Skymly/Observables)（声明式 RestAPI + R3 事件桥）的真实世界展示应用：作者日常使用的 .NET 10 MAUI GitHub 客户端，个人项目。Windows 优先，Android 次要，iOS / MacCatalyst 暂缓（[ADR-005](docs/adr/ADR-005-windows-first-platform-strategy.md)）。

修改代码前先读 [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md)（环境、构建、测试、发版）。当前阶段与 backlog 只看 [docs/ROADMAP.md](docs/ROADMAP.md)，本文件不复述。

## 架构与边界

- 分层单向：App → ViewModels → Services / GitHubApi → Core。ViewModels 不引用 MAUI；PAT 凭据经 `ICredentialStore`，平台实现只在 App `Platforms/`（[ADR-004](docs/adr/ADR-004-pat-auth-platform-credential-store.md)）。项目职责、不变量与模块路径见 [docs/design/Architecture.md](docs/design/Architecture.md)。
- **每个 PR 只改一个模块**：App / ViewModels / Core / GitHubApi / Services / Tests / Docs-Repo（`docs/`、根目录 md、`build/`、`.github/`）。
- 技术栈：R3 `BindableReactiveProperty<T>` + Observables.RestAPI.R3 / Events.R3 + CommunityToolkit.Mvvm `[RelayCommand]`；版本以 `Directory.Packages.props` 为准。Design Doc：[RestApi.md](docs/design/RestApi.md)、[Events.md](docs/design/Events.md)。
- 已知上游限制：分页走 `GitHubQueryHandler`（[ADR-006](docs/adr/ADR-006-github-query-handler-pagination.md)）；SearchBar `.Events()` 走公开事件适配器（[ADR-015](docs/adr/ADR-015-searchbar-public-event-adapter.md)）。
- 编码：file-scoped namespaces；nullable / LangVersion / CPM 以 `Directory.Build.props` 为准。Release 下 `TreatWarningsAsErrors`——Debug 编过不代表 CI 过。

## 验证

```powershell
./build.ps1 --target CiLib --configuration Release       # 库测试，每个 PR 必跑
./build.ps1 --target CiAndroid --configuration Release   # 改 App / Android
./build.ps1 --target CiAll --configuration Release       # 改 App 或格式：Format + Windows + Android
```

## 文档同步

规则见 [docs/DOCUMENTATION.md](docs/DOCUMENTATION.md)。随代码 PR 一并更新：

| 变更 | 动作 |
|------|------|
| API / 模型 / 实现 | 对应 Design Doc |
| 破坏性 API、跨层架构、推翻既有决策 | 新 ADR（Accepted 后只能 Supersede）+ Design Doc |
| 用户可见 | `CHANGELOG.md` `[Unreleased]` |
| 里程碑完成 | ROADMAP「已完成（归档）」 |

维护者文档只放 `docs/`（根目录 `AGENTS.md`、`README.md`、`CONTRIBUTING.md`、`CHANGELOG.md` 除外）。

## Git / PR / Commit

- Issue / PR / Commit **一律英语**；与用户对话默认**简体中文**；代码标识符与对外文档英语。
- 分支 `feature/<short-description>` / `fix/<short-description>`；提交信息祈使句、说明 **why**；PR 正文按 [`.github/pull_request_template.md`](.github/pull_request_template.md)。

## 与用户协作

- 最小 diff，匹配现有风格；中文解释权衡。
- 表述不清时先问：聚焦问题 + 2–4 个具体选项，不基于猜测动手。
- 要求与 ADR / 本文件冲突时（ViewModel 引用 MAUI、Core 依赖 App、在 App 之外存 PAT、未立 ADR 改 `IGitHubReposApi`、跳过 `CiLib`、单 PR 混多模块、过度设计等），先说明为什么不合理并给替代方案，等用户确认再动手。ADR 变更走 Supersede。

## Agent skills

### Issue tracker

GitHub Issues on Skymly/GitPulse (`gh`). See `docs/agents/issue-tracker.md`.

### Triage labels

Canonical strings: `needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: glossary at `docs/CONTEXT.md`, ADRs at `docs/adr/` (GitPulse `ADR-NNN` template). See `docs/agents/domain.md`.
