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

### Windows UI 自动化（本机）

`tests/GitPulse.UITests` 使用 **FlaUI.UIA3 + NUnit**（直接启动 unpackaged `GitPulse.App.exe`），默认不进 `CiLib`。

| 依赖 | 说明 |
|------|------|
| Windows + 开发人员模式 | UIA 自动化通常需要开启 |
| `GITPULSE_UI_TEST_PAT` | User 环境变量；备用账号 PAT（勿提交） |
| 被测 App | `artifacts/publish/win-x64/GitPulse.App.exe`，或设 `GITPULSE_UI_APP_PATH` |

```powershell
# Publish unpackaged Windows app (TargetFrameworks workaround for NU1102)
dotnet restore GitPulse.slnx
dotnet publish src/GitPulse.App/GitPulse.App.csproj `
  -c Release -f net10.0-windows10.0.19041.0 -r win-x64 --self-contained true --no-restore `
  -o artifacts/publish/win-x64 `
  -p:TargetFrameworks=net10.0-windows10.0.19041.0 `
  -p:WindowsPackageType=None

$env:GITPULSE_UI_TEST_PAT = [Environment]::GetEnvironmentVariable('GITPULSE_UI_TEST_PAT','User')
dotnet test tests/GitPulse.UITests/GitPulse.UITests.csproj -c Release
```

`FlaUISetup` 会设置 `GITPULSE_UI_TEST_HOST=1`。在该模式下 App 用 `UiTestHostPage`（`TabbedPage` + `NavigationPage`）代替 `AppShell`，因为 **Shell + NavigationView** 下 UIA 看不到 `ContentPage` 正文。页面在 `Window` 首帧 `Appearing` 后再构造（`CreateWindow` 期间 inflate `SettingsPage` 会 WinUI 崩溃）。深度导航走 `AppNavigation`（有 Shell 时仍用 `Shell.GoToAsync`）。诊断输出：`artifacts/uitest-diagnostics/`。

### Android UI 自动化（本机模拟器）

`tests/GitPulse.AndroidUITests` 使用 **Appium 2 + UiAutomator2 + NUnit**，默认不进 `CiLib`。决策见 [ADR-014](adr/ADR-014-android-emulator-ui-smoke-and-apk-release.md)；术语 **Android Emulator UI Smoke** 见 [CONTEXT.md](CONTEXT.md)。

| 依赖 | 说明 |
|------|------|
| Android SDK + 模拟器 | 默认 AVD 名 **`GitPulse_API34_Phone`**（API 34+ 竖屏手机）；可用 `GITPULSE_ANDROID_AVD` 覆盖 |
| JDK 17+ | 构建/模拟器常用；本机可设 `JAVA_HOME` |
| Appium 2 + UiAutomator2 driver | `npm i -g appium` 后 `appium driver install uiautomator2`；不进默认 CI |
| `GITPULSE_UI_TEST_PAT` | 与 Windows UITests 相同（User 环境变量；勿提交） |
| UI Test Host | Appium 经 `optionalIntentArguments --es GITPULSE_UI_TEST_HOST 1` 启用（Android 进程读不到宿主机环境变量；见 App `MainActivity`） |
| 被测包 | 日常可用 debug/Release APK；**cut / 挂 APK 前**须对 **签名 APK**（`PublishAndroid` → `artifacts/GitPulse-android.apk`）再跑；可用 `GITPULSE_ANDROID_APK` 覆盖路径 |

创建默认 AVD（一次性）：**

```powershell
$env:ANDROID_HOME = "$env:LOCALAPPDATA\Android\Sdk"   # 或本机 SDK 路径
$env:ANDROID_SDK_ROOT = $env:ANDROID_HOME
$env:Path = "$env:ANDROID_HOME\emulator;$env:ANDROID_HOME\platform-tools;$env:ANDROID_HOME\cmdline-tools\latest\bin;$env:Path"

# 若尚未安装：emulator + platforms;android-34 + system-images;android-34;google_apis;x86_64
avdmanager create avd -n GitPulse_API34_Phone -k "system-images;android-34;google_apis;x86_64" -d pixel_6 --force
```

**构建被测 APK 并跑短冒烟：**

```powershell
$env:JAVA_HOME = 'C:\Program Files\Android\openjdk\jdk-21.0.8'  # 按本机 JDK 调整
$env:ANDROID_HOME = "$env:LOCALAPPDATA\Android\Sdk"
$env:ANDROID_SDK_ROOT = $env:ANDROID_HOME
$env:Path = "$env:JAVA_HOME\bin;$env:ANDROID_HOME\emulator;$env:ANDROID_HOME\platform-tools;$env:Path"

# 日常：Release APK（EmbedAssemblies 避免 Fast Deployment 与 Appium 冲突）
dotnet build src/GitPulse.App/GitPulse.App.csproj -c Release -f net10.0-android `
  -p:EmbedAssembliesIntoApk=true -p:AndroidPackageFormats=apk

# 可选：先手动启动模拟器（否则 Appium 会按 GITPULSE_ANDROID_AVD / 默认名拉起）
# emulator -avd GitPulse_API34_Phone -no-snapshot-load

$env:GITPULSE_UI_TEST_PAT = [Environment]::GetEnvironmentVariable('GITPULSE_UI_TEST_PAT','User')
# 可选：$env:GITPULSE_ANDROID_APK = 'artifacts/GitPulse-android.apk'
dotnet test tests/GitPulse.AndroidUITests/GitPulse.AndroidUITests.csproj -c Release
```

失败时诊断输出写入 `artifacts/uitest-diagnostics/`（截图 + page source）。`AppiumSetup` 会在端口 `4723` 无服务时自启本地 Appium；也可另开终端手动 `appium`。


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

# Windows 自包含发布（目录 + Release Artifact zip：artifacts/GitPulse-win-x64.zip）
./build.ps1 --target Publish --configuration Release --runtime win-x64
./build.ps1 --target PublishVerify --configuration Release --runtime win-x64

# Android 签名 APK 发布（Release Artifact：artifacts/GitPulse-android.apk）
# 需先设置四个 ANDROID_* 环境变量（见下文「Android 签名 Secrets 契约」），缺一即失败
./build.ps1 --target PublishAndroidVerify --configuration Release

# 双产物 tag 形状（Win zip + 签名 APK；ADR-014 / v0.1.1+；缺 Secret/APK 即失败）
./build.ps1 --target Release --configuration Release
```

`CiAll` 经 `Compile` → `CompileAndroid` 覆盖 Android 编译门禁（ADR-011 / [#32](https://github.com/Skymly/GitPulse/issues/32)）。日常只改 Android 相关时可先跑 `CiAndroid`。**v0.1.0** 仅 Win zip（ADR-013）；**v0.1.1+** 在 Android Emulator UI Smoke 通过后可挂签名 APK（ADR-014）。契约与 cut 冒烟见下文 [发版手册（M12 / ADR-013 → M13 / ADR-014）](#release-m12)。

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

## 发版手册（M12 / ADR-013 → M13 / ADR-014）

v0.1.0 经 **GitHub Release** 分发且 **仅 Windows zip**（[ADR-013](adr/ADR-013-v0.1.0-windows-only-github-release.md)）。自 **v0.1.1** 起，cut 在通过 **Android Emulator UI Smoke** 后可同时挂签名 APK（[ADR-014](adr/ADR-014-android-emulator-ui-smoke-and-apk-release.md)）。术语见 [CONTEXT.md](CONTEXT.md)。Epic：M12 [\#54](https://github.com/Skymly/GitPulse/issues/54)；M13 [\#67](https://github.com/Skymly/GitPulse/issues/67)。

### 分发与 Release Artifact

| 项 | 约定 |
|----|------|
| 渠道 | 仅 GitHub Releases；`v*` tag 触发 `release` job。不上商店。 |
| Windows **Release Artifact** | self-contained publish **目录 zip**（`artifacts/GitPulse-{RID}.zip`）。未 Authenticode。入口为 `GitPulse.App.exe`。 |
| Android **Release Artifact** | CI **签名 APK**（`artifacts/GitPulse-android.apk`）。**v0.1.0 未挂**；**v0.1.1+** 在 Android Emulator UI Smoke（cut 清单）通过后挂。不做 AAB。 |
| 版本 | MinVer + `v` tag 前缀；最近公开 tag **`v0.32.0`**。 |
| Release 说明 | `CHANGELOG.md` 对应版本节为权威；`generate_release_notes` 仅可作补充。 |

交付仍拆两刀：**先合能力**（自动化 / 流水线），再单独 **cut**（冒烟 + CHANGELOG 收口 + 打 tag）。

### Android 签名 Secrets 契约

跑 `PublishAndroid*` 或（自 **v0.1.1** 起）tag `release` 挂 APK 时需要。密钥材料**只**放在 GitHub Secrets（另保留离线备份）；**禁止**提交 keystore / 密码。**v0.1.0** 的 Release 历史上不要求这些 Secret（ADR-013）；**v0.1.1+** 双产物 cut 缺任一 Secret 或 APK 即失败（勿发半空 Release）。

| Secret 名 | 用途 | 映射（实现参考） |
|-----------|------|------------------|
| `ANDROID_KEYSTORE_BASE64` | Base64 编码的 `.jks` / `.keystore` 文件内容 | 解码后写入临时 keystore 路径 |
| `ANDROID_KEYSTORE_PASSWORD` | Keystore 密码 | `AndroidSigningStorePass` |
| `ANDROID_KEY_ALIAS` | 密钥别名 | `AndroidSigningKeyAlias` |
| `ANDROID_KEY_PASSWORD` | 密钥密码 | `AndroidSigningKeyPass` |

仓库 Settings → Secrets and variables → Actions 中可预先配置。本地跑 `PublishAndroid*` 需自行设置同名环境变量（取自离线备份），缺一即失败。Windows 发布保持未签名。

### Tag → GitHub Release 流程

1. （公开 cut 前）完成下方短冒烟；将 `CHANGELOG.md` 的 `[Unreleased]` 收成目标版本节（如 `[0.1.1]`）。
2. 在已合入能力的 `main` 上打并推送 `v*` tag（例如 `v0.1.1`）。**不要** force-push tag；**不要**未经冒烟发版。
3. Workflow：`ci-lib` + `ci-windows` → `release`（Nuke `Release`）。
4. **v0.1.0**：Release **仅** Windows publish zip（ADR-013，已发布；历史事实不变）。
5. **v0.1.1+**（ADR-014）：`release` job 挂 **Windows zip + 签名 Android APK**；缺 `ANDROID_*` / APK **fail closed**。Android Emulator UI Smoke 在 cut 清单本机完成，**不**作为本 job 硬步骤。
6. 发布前可用 `upload-artifact` / Release draft 做干跑检查。

本地对照：

```powershell
# Win zip only
./build.ps1 --target PublishVerify --configuration Release
# 双产物（需 ANDROID_*）：Win zip + 签名 APK；缺 Secret/APK 即失败
./build.ps1 --target Release --configuration Release
# 仅签名 APK（需 ANDROID_*）；cut 前 Android Emulator UI Smoke 应对此产物再跑
./build.ps1 --target PublishAndroidVerify --configuration Release
```

### Cut 短冒烟清单

#### v0.1.0（已完成，ADR-013）

- [x] **Windows**：解压 Release Artifact zip，运行 `GitPulse.App.exe`，用 PAT 登录。
- [x] **一等公民页**（可用 FlaUI）：Repos / Issues / PRs / Notifications / Search / Actions 等至少各开一页不崩溃。
- [x] `CHANGELOG.md` 已收口；GitHub Release 正文以该版本节为准。
- [x] **不要求** Android 侧载冒烟。

#### v0.1.1+（ADR-014）

在打公开 tag 前过一遍：

- [ ] **Windows**：同 v0.1.0（FlaUI 短冒烟可选复跑）。
- [ ] **Android Emulator UI Smoke**：默认 API 34+ 竖屏模拟器；Appium 短冒烟对齐 Windows 场景；**cut 前对签名 APK** 再跑一遍。
- [ ] `CHANGELOG.md` 已收口；GitHub Release 挂 **Win zip + 签名 APK**。
- [ ] **不要求** 真机侧载必过；UI 冒烟默认 **不**进 `CiLib` / `release` 硬门禁。

非目标（勿混入 cut）：商店上架、Win Authenticode、AAB、OAuth、Android 出应用通知、产品新功能 / UX 精修。

## 相关文档

- [docs/README.md](README.md) — 文档索引
- [DOCUMENTATION.md](DOCUMENTATION.md) — 文档约定
- [ROADMAP.md](ROADMAP.md) — 里程碑路线图
- [CONTEXT.md](CONTEXT.md) — Release Artifact / GitHub Release / Android Emulator UI Smoke 术语
- [adr/ADR-014-android-emulator-ui-smoke-and-apk-release.md](adr/ADR-014-android-emulator-ui-smoke-and-apk-release.md) — 现行：模拟器冒烟解锁 APK
- [adr/ADR-013-v0.1.0-windows-only-github-release.md](adr/ADR-013-v0.1.0-windows-only-github-release.md) — v0.1.0 Win-only（已取代）
- [adr/ADR-012-v0.1.0-github-release-distribution.md](adr/ADR-012-v0.1.0-github-release-distribution.md) — 原双产物决策（已取代）
- [../CONTRIBUTING.md](../CONTRIBUTING.md) — 贡献流程
- [../AGENTS.md](../AGENTS.md) — AI Agent 上下文
