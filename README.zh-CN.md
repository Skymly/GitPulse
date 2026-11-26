# GitPulse

[English](README.md) | **简体中文** | [繁體中文](README.zh-TW.md) | [日本語](README.ja.md)

[![CI](https://github.com/Skymly/GitPulse/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/Skymly/GitPulse/actions/workflows/build-and-test.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4.svg)](https://dotnet.microsoft.com/)
[![MAUI](https://img.shields.io/badge/MAUI-10-512BD4.svg)](https://learn.microsoft.com/dotnet/maui/)
[![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Android-blue.svg)](#quick-start)

基于 .NET MAUI 构建的跨平台 GitHub 客户端，同时作为 [Observables](https://github.com/Skymly/Observables) 源生成器库的真实世界展示应用（面向 R3 的声明式反应式 HTTP / 事件桥接）。

这不是玩具 demo，而是作者日常使用的实际工具，并会持续迭代打磨。

## 快速开始

### 环境要求

- .NET 10 SDK（`global.json` 固定 `10.0.302`，`rollForward: latestPatch`）
- .NET MAUI 工作负载（`dotnet workload install maui`）
- Windows（主目标）或 Android（次目标）

### 构建与运行

```powershell
git clone https://github.com/Skymly/GitPulse.git
cd GitPulse

# 使用 Nuke（与 CI 一致：格式检查 + Windows 编译 + Android 编译 + 库测试）
./build.ps1 --target CiAll --configuration Release

# 或使用传统 dotnet 命令
dotnet build GitPulse.slnx -c Release
dotnet run --project src/GitPulse.App/GitPulse.App.csproj -c Debug -f net10.0-windows10.0.19041.0
```

### 配置 GitHub 访问

首次启动后打开 **Settings** 粘贴 GitHub Personal Access Token（PAT）。凭据会在本地加密存储：

- Windows：DPAPI（CurrentUser 作用域）
- Android：SecureStorage

## 功能特性

应用仍在持续开发中，规划路线见 [里程碑路线图](./docs/ROADMAP.md)。

维护者文档（ADR 与各子系统设计文档）位于 [`docs/`](./docs/README.md)。

### 展示亮点：Observables 集成

GitPulse 当前展示了两个 Observables 域，更多域将在后续接入：

| 域 | 用法 |
|----|------|
| `Observables.RestAPI.R3` | 声明式 GitHub REST API 接口 —— 编写接口即可生成 `Observable<T>` 代理 |
| `Observables.Events.R3` | MAUI 控件事件 → `Observable<T>` 数据流（防抖搜索、滚动加载等） |

声明式 API 示例：

```csharp
public interface IGitHubReposApi
{
    [Get("/repos/{owner}/{repo}")]
    Observable<Repo> GetRepo(string owner, string repo);
}

var api = RestService.For<IGitHubReposApi>(httpClient);
using var d = api.GetRepo("Skymly", "Observables")
    .Subscribe(repo => Console.WriteLine(repo.FullName));
```

## 技术栈

- .NET 10 + .NET MAUI
- R3 + R3Extensions.Maui（反应式核心）
- Observables.RestAPI.R3 + Observables.Events.R3（源生成的反应式桥接）
- CommunityToolkit.Mvvm
- MVVM 架构，多项目解决方案（App / ViewModels / Core / GitHubApi / Services）
- Nuke 构建编排
- MinVer（基于 Git Tag 的版本推导）

## 项目结构

```
src/
  GitPulse.App/         — MAUI UI、DI 与平台入口
  GitPulse.ViewModels/  — ViewModels（R3，不引用 MAUI）
  GitPulse.Core/        — 领域模型与抽象接口（不引用 MAUI）
  GitPulse.GitHubApi/   — Observables.RestAPI 声明式接口与 DTO
  GitPulse.Services/    — 认证、轮询、GitHub 客户端工厂
tests/
  GitPulse.Tests/       — 单元测试
build/                  — Nuke 构建脚本
docs/                   — ADR、子系统设计文档与路线图
.nuke/                  — Nuke 参数与 schema
```

## 构建与 CI（Nuke）

| Nuke 目标 | 说明 |
|-----------|------|
| **Ci** | `Clean` → `Restore` → `Compile` → `UnitTest`（`Compile` 包含 Android） |
| **CiLib** | 仅跨平台库测试（不含 App 项目） |
| **CiAndroid** | 仅 Android App 编译检查（`net10.0-android`；不生成 APK/AAB） |
| **CiAll** | `Format` + `Ci`（完整本地/CI 验证，含 Android 编译） |
| **Test** | `UnitTest` 别名 |
| **Format** | `dotnet format --verify-no-changes` |
| **FormatFix** | `dotnet format`（就地格式化） |
| **Publish** | 自包含 Windows exe → `artifacts/publish/{Runtime}/` |
| **PublishVerify** | `Publish` + 校验入口产物存在 |
| **Release** | `CiAll` + `PublishVerify` + `PublishAndroidVerify`（Windows zip + 签名 APK） |

```powershell
./build.ps1 --target CiAll --configuration Release
./build.ps1 --target CiAndroid --configuration Release
./build.ps1 --target Publish --configuration Release --runtime win-x64
```

## 版本管理

版本号由 [MinVer](https://github.com/adamralph/minver) 根据 Git Tag 自动推导。推送 `v*` 标签即可触发 CI 发布流程并设定版本。

```powershell
git tag v0.1.0
git push origin v0.1.0
```

## 许可证

MIT — 见 [LICENSE](./LICENSE)。
