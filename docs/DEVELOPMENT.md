# 开发手册

本文档是**操作手册**（环境、构建、测试、仓库布局）。**项目级规范权威源**为 [`../AGENTS.md`](../AGENTS.md)；功能 backlog 见 [`ROADMAP.md`](ROADMAP.md)。冲突时以 `AGENTS.md` 为准。

## 环境要求

| 工具 | 版本建议 |
|------|----------|
| [.NET SDK](https://dotnet.microsoft.com/download) | 10.0（LTS） |
| .NET MAUI workload | `dotnet workload install maui` |
| Git | 2.x |
| IDE | Visual Studio 2022、Rider 或 VS Code + C# Dev Kit |

主目标框架：`net10.0-windows10.0.19041.0`（Windows）、`net10.0-android`（次要）。

## 克隆与构建

```powershell
git clone https://github.com/Skymly/GitPulse.git
cd GitPulse
```

**推荐（与 CI 一致，Nuke）：**

```powershell
# 完整库级 CI（跨平台，不编译 App）
./build.ps1 --target CiLib --configuration Release

# 完整 CI（含 MAUI App 编译 + 全量测试）
./build.ps1 --target CiAll --configuration Release

# Windows 自包含发布
./build.ps1 --target Publish --configuration Release --runtime win-x64
```

**传统 dotnet：**

```powershell
dotnet build GitPulse.slnx -c Release
dotnet test tests/GitPulse.Tests/GitPulse.Tests.csproj -c Release
dotnet run --project src/GitPulse.App/GitPulse.App.csproj -c Debug -f net10.0-windows10.0.19041.0
```

## 仓库布局

```
src/
  GitPulse.App/         — MAUI UI、DI、平台入口、DiffHtmlGenerator
  GitPulse.ViewModels/  — ViewModel（R3 状态，无 MAUI 依赖，可单测）
  GitPulse.Core/        — 领域模型、抽象、Http 辅助
  GitPulse.GitHubApi/   — Observables.RestAPI 声明式接口
  GitPulse.Services/   — GitHubClientFactory、通知轮询
tests/GitPulse.Tests/   — 单元测试 + TestHelpers
build/                  — Nuke 脚本
docs/                   — 文档驱动开发体系（见 DOCUMENTATION.md）
```

## 架构原则

1. **分层依赖单向**：App → ViewModels → Services/GitHubApi → Core；Core 不依赖 UI/MAUI。
2. **ViewModel 可测**：ViewModel 不引用 MAUI；通过 `IGitHubClientFactory` 等抽象注入。
3. **声明式 GitHub API**：`IGitHubReposApi` 由 Observables 源生成 HttpClient 代理。
4. **R3 响应式状态**：`BindableReactiveProperty<T>` + `[RelayCommand]`；MAUI 绑定在 View 层。
5. **async/await**：所有 I/O 异步；`CancellationToken` 用于 HTTP 超时。

## 测试

```powershell
./build.ps1 --target CiLib --configuration Release
```

| 层级 | 位置 | 覆盖 |
|------|------|------|
| ViewModel | `tests/GitPulse.Tests/*ViewModelTests.cs` | 业务逻辑、Mock HTTP |
| Core | `GitHubQueryHandlerTests`、`LinkHeaderParserTests` 等 | HTTP 辅助、模型 |
| Services | `GitHubClientFactoryTests`、`NotificationPollerTests` | 工厂、轮询 |

`CiLib` 在 Release 下运行 **190** 个单元测试（截至 M7/M8 工作区）。

## 提交与分支

- 默认分支：`main`
- 功能分支：`feature/<short-description>`
- 修复分支：`fix/<short-description>`
- 提交信息：**英语**，说明 **why**

CI：[`.github/workflows/build-and-test.yml`](../.github/workflows/build-and-test.yml)。

## 相关文档

- [docs/README.md](README.md) — 文档索引
- [DOCUMENTATION.md](DOCUMENTATION.md) — 文档驱动开发标准
- [ROADMAP.md](ROADMAP.md) — 里程碑路线图
- [../CONTRIBUTING.md](../CONTRIBUTING.md) — 贡献流程
- [../AGENTS.md](../AGENTS.md) — AI Agent 上下文
