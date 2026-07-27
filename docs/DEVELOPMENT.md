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

# Android App 编译门禁（仅 net10.0-android；需 MAUI workload；apk 试编、无签名/AAB 分发）
./build.ps1 --target CiAndroid --configuration Release

# 完整 CI（Format + 库测试 + Windows/Android App 编译）
./build.ps1 --target CiAll --configuration Release

# Windows 自包含发布
./build.ps1 --target Publish --configuration Release --runtime win-x64
```

`CiAll` 经 `Compile` → `CompileAndroid` 覆盖 Android 编译门禁（ADR-011 / [#32](https://github.com/Skymly/GitPulse/issues/32)）。日常只改 Android 相关时可先跑 `CiAndroid`。**签名 APK、Win publish zip 挂 GitHub Release** 属 M12（ADR-012）；契约与 cut 冒烟见下文 [发版手册（M12 / ADR-012）](#release-m12)。

**传统 dotnet：**

```powershell
dotnet build GitPulse.slnx -c Release
dotnet test tests/GitPulse.Tests/GitPulse.Tests.csproj -c Release
dotnet run --project src/GitPulse.App/GitPulse.App.csproj -c Debug -f net10.0-windows10.0.19041.0

# Android 编译（与 CiAndroid 等价意图）
dotnet build src/GitPulse.App/GitPulse.App.csproj -c Release -f net10.0-android
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
docs/                   — ADR、设计文档与路线图（见 DOCUMENTATION.md）
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
| Live Search（可选） | `SearchLiveIntegrationTests` | 真实 GitHub Search API |

`CiLib` 在 Release 下运行库单元测试；未设置 PAT 时，带 `Category=Integration` 的实网测试会被 **Skip**，不失败。

### 可选：实网 Search 集成测试

使用**专用测试账号**的 PAT（classic 或 fine-grained）。**不要**把 token 写入仓库、聊天或提交信息。

```powershell
# 从本机密文文件加载（路径自定；切勿提交该文件）
$env:GITPULSE_TEST_PAT = (Get-Content 'path\to\pat.txt' -Raw).Trim()

dotnet test tests/GitPulse.Tests/GitPulse.Tests.csproj -c Release --filter Category=Integration

Remove-Item Env:GITPULSE_TEST_PAT
```

也可在 CI 中配置同名 Secret，经 `workflow_dispatch` 或独立 job 注入后运行上述 filter。实网测试覆盖 API 级 M9 门禁；Windows UI 导航（结果进详情页等）仍需手工验收，见 [design/RestApi.md](design/RestApi.md)。

## 提交与分支

- 默认分支：`main`
- 功能分支：`feature/<short-description>`
- 修复分支：`fix/<short-description>`
- 提交信息：**英语**，说明 **why**

CI：[`.github/workflows/build-and-test.yml`](../.github/workflows/build-and-test.yml)。

<a id="release-m12"></a>

## 发版手册（M12 / ADR-012）

v0.1.0 经 **GitHub Release** 分发（术语见 [CONTEXT.md](CONTEXT.md)）。决策见 [ADR-012](adr/ADR-012-v0.1.0-github-release-artifacts.md)；epic [#54](https://github.com/Skymly/GitPulse/issues/54)。流水线实现（Win zip / 签名 APK）见 [#56](https://github.com/Skymly/GitPulse/issues/56)、[#57](https://github.com/Skymly/GitPulse/issues/57)；首次公开发版见 [#58](https://github.com/Skymly/GitPulse/issues/58)。

### 分发与 Release Artifact

| 项 | 约定 |
|----|------|
| 渠道 | 仅 GitHub Releases；`v*` tag 触发 `release` job。不上商店。 |
| Windows **Release Artifact** | self-contained publish **目录 zip**（非整包单挂 `GitPulse.exe`）。未 Authenticode。 |
| Android **Release Artifact** | CI **签名 APK**。不做 AAB。 |
| 版本 | MinVer + `v` tag 前缀；首发 tag 预期 `v0.1.0`。 |
| Release 说明 | `CHANGELOG.md` 对应版本节为权威；`generate_release_notes` 仅可作补充。 |

交付拆两刀：**先合发布流水线**（可不打公开 tag），再单独 **cut**（冒烟 + CHANGELOG 收口 + 打 tag）。

### Android 签名 Secrets 契约

仅 Android 在 CI 签名。密钥材料**只**放在 GitHub Secrets（另保留离线备份）；**禁止**提交 keystore / 密码。缺失任一 Secret 或产物时，`release` job **必须失败**（不得发布半空 Release）。

| Secret 名 | 用途 | 映射（实现参考） |
|-----------|------|------------------|
| `ANDROID_KEYSTORE_BASE64` | Base64 编码的 `.jks` / `.keystore` 文件内容 | 解码后写入临时 keystore 路径 |
| `ANDROID_KEYSTORE_PASSWORD` | Keystore 密码 | `AndroidSigningStorePass` |
| `ANDROID_KEY_ALIAS` | 密钥别名 | `AndroidSigningKeyAlias` |
| `ANDROID_KEY_PASSWORD` | 密钥密码 | `AndroidSigningKeyPass` |

仓库 Settings → Secrets and variables → Actions 中配置上述名称。流水线应用本契约见 [#57](https://github.com/Skymly/GitPulse/issues/57)。Windows 发布保持未签名。

### Tag → GitHub Release 流程

1. （公开 cut 前）完成下方短冒烟；将 `CHANGELOG.md` 的 `[Unreleased]` 收成目标版本节（如 `[0.1.0]`）。
2. 在已合入流水线的 `main` 上打并推送 `v*` tag（例如 `v0.1.0`）。**不要** force-push tag；**不要**未经冒烟发版。
3. Workflow：`ci-lib` + `ci-windows` → `release`（`Nuke` `Release`）。
4. `release` 产出并挂到该 tag 的 **GitHub Release**：Windows publish 目录 zip + 签名 Android APK 两个 **Release Artifact**。
5. 发布前可用 `upload-artifact` / Release draft 做干跑检查（不宣布正式 cut）。

本地对照（不替代 CI 签名与挂包）：

```powershell
./build.ps1 --target Release --configuration Release
```

### Cut 短冒烟清单

在打公开 tag 前手过一遍（ADR-012）：

- [ ] **Windows**：解压 Release Artifact zip（或等价 publish 目录），运行 App，用 PAT 登录。
- [ ] **Android**：侧载签名 APK，用 PAT 登录。
- [ ] **各平台**：打开任一等公民页（Repos / Issues / PRs / Notifications / Search / Actions 等）不崩溃。
- [ ] `CHANGELOG.md` 已收口；GitHub Release 正文以该版本节为准。

非目标（勿混入 cut）：商店上架、Win Authenticode、AAB、OAuth、Android 出应用通知、新功能 / UX 精修。

## 相关文档

- [docs/README.md](README.md) — 文档索引
- [DOCUMENTATION.md](DOCUMENTATION.md) — 文档约定
- [ROADMAP.md](ROADMAP.md) — 里程碑路线图
- [CONTEXT.md](CONTEXT.md) — Release Artifact / GitHub Release 术语
- [adr/ADR-012-v0.1.0-github-release-artifacts.md](adr/ADR-012-v0.1.0-github-release-artifacts.md) — v0.1.0 分发决策
- [../CONTRIBUTING.md](../CONTRIBUTING.md) — 贡献流程
- [../AGENTS.md](../AGENTS.md) — AI Agent 上下文
