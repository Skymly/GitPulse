# GitPulse

[English](README.md) | [简体中文](README.zh-CN.md) | **繁體中文** | [日本語](README.ja.md)

[![CI](https://github.com/Skymly/GitPulse/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/Skymly/GitPulse/actions/workflows/build-and-test.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
![.NET](https://img.shields.io/badge/.NET-10-512BD4.svg)
![MAUI](https://img.shields.io/badge/MAUI-10-512BD4.svg)
![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Android-blue.svg)

以 .NET MAUI 建置的跨平台 GitHub 用戶端，同時作為 [Observables](https://github.com/Skymly/Observables) 原始碼產生器程式庫的真實世界展示應用程式（面向 R3 的宣告式反應式 HTTP / 事件橋接）。

這並非玩具範例，而是作者日常使用的實際工具，並將持續迭代至真正實用。

## 快速開始

### 環境需求

- .NET 10 SDK（`global.json` 固定 `10.0.302`，`rollForward: latestPatch`）
- .NET MAUI 工作負載（`dotnet workload install maui`）
- Windows（主要目標）或 Android（次要目標）

### 建置與執行

```powershell
git clone https://github.com/Skymly/GitPulse.git
cd GitPulse

# 透過 Nuke（與 CI 一致：格式檢查 + Windows 編譯 + Android 編譯 + 程式庫測試）
./build.ps1 --target CiAll --configuration Release

# 或使用傳統 dotnet 命令
dotnet build GitPulse.slnx -c Release
dotnet run --project src/GitPulse.App/GitPulse.App.csproj -c Debug -f net10.0-windows10.0.19041.0
```

### 設定 GitHub 存取

首次啟動時開啟 **Settings** 並貼上 GitHub Personal Access Token（PAT）。權杖會於本機加密儲存：

- Windows：DPAPI（CurrentUser 範圍）
- Android：SecureStorage

## 功能特色

應用程式仍在積極開發中，規劃進度請參閱[里程碑路線圖](./docs/ROADMAP.md)。

維護者文件（ADR 與各子系統設計文件）位於 [`docs/`](./docs/README.md)。

### 展示亮點：Observables 整合

GitPulse 目前展示兩個 Observables 領域，後續將陸續擴充：

| 領域 | 用途 |
|------|------|
| `Observables.RestAPI.R3` | 宣告式 GitHub REST API 介面 —— 撰寫介面即可獲得 `Observable<T>` 代理 |
| `Observables.Events.R3` | MAUI 控制項事件 → `Observable<T>` 資料流（防抖搜尋、捲動載入等） |

宣告式 API 範例：

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

## 技術堆疊

- .NET 10 + .NET MAUI
- R3 + R3Extensions.Maui（反應式核心）
- Observables.RestAPI.R3 + Observables.Events.R3（原始碼產生的反應式橋接）
- CommunityToolkit.Mvvm
- MVVM 架構，多專案解決方案（App / ViewModels / Core / GitHubApi / Services）
- Nuke 建置編排
- MinVer（基於 Git Tag 的版本號推導）

## 專案配置

```
src/
  GitPulse.App/         — MAUI UI、DI 與平台進入點
  GitPulse.ViewModels/  — ViewModels（R3，不參考 MAUI）
  GitPulse.Core/        — 領域模型與抽象介面（不參考 MAUI）
  GitPulse.GitHubApi/   — Observables.RestAPI 宣告式介面與 DTO
  GitPulse.Services/    — 驗證、輪詢、GitHub 用戶端工廠
tests/
  GitPulse.Tests/       — 單元測試
build/                  — Nuke 建置指令碼
docs/                   — ADR、子系統設計文件與路線圖
.nuke/                  — Nuke 參數與 schema
```

## 建置與 CI（Nuke）

| Nuke 目標 | 說明 |
|-----------|------|
| **Ci** | `Clean` → `Restore` → `Compile` → `UnitTest`（`Compile` 包含 Android） |
| **CiLib** | 僅跨平台程式庫測試（不含 App 專案） |
| **CiAndroid** | 僅 Android App 編譯檢查（`net10.0-android`；不產出 APK/AAB） |
| **CiAll** | `Format` + `Ci`（完整本機/CI 驗證，含 Android 編譯） |
| **Test** | `UnitTest` 別名 |
| **Format** | `dotnet format --verify-no-changes` |
| **FormatFix** | `dotnet format`（就地套用格式化） |
| **Publish** | 獨立 Windows exe → `artifacts/publish/{Runtime}/` |
| **PublishVerify** | `Publish` + 驗證進入點檔案存在 |
| **Release** | `CiAll` + `PublishVerify` + `PublishAndroidVerify`（Windows zip + 簽章 APK） |

```powershell
./build.ps1 --target CiAll --configuration Release
./build.ps1 --target CiAndroid --configuration Release
./build.ps1 --target Publish --configuration Release --runtime win-x64
```

## 版本管理

版本號由 [MinVer](https://github.com/adamralph/minver) 依據 Git Tag 自動推導。推送 `v*` 標籤會觸發 CI 發布工作並設定版本。

```powershell
git tag v0.1.0
git push origin v0.1.0
```

## 授權條款

MIT — 請參閱 [LICENSE](./LICENSE)。
