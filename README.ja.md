# GitPulse

[English](README.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | **日本語**

[![CI](https://github.com/Skymly/GitPulse/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/Skymly/GitPulse/actions/workflows/build-and-test.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
![.NET](https://img.shields.io/badge/.NET-10-512BD4.svg)
![MAUI](https://img.shields.io/badge/MAUI-10-512BD4.svg)
![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Android-blue.svg)

.NET MAUI と R3 で構築されたクロスプラットフォーム GitHub クライアントであり、[Observables](https://github.com/Skymly/Observables) ソースジェネレーターライブラリの実用ショーケースアプリです（宣言的リアクティブ HTTP / イベント連携）。

## クイックスタート

### 前提条件

- .NET 10 SDK（`global.json` で `10.0.302`・`rollForward: latestPatch` を固定）
- .NET MAUI ワークロード（`dotnet workload install maui`）
- Windows（主要ターゲット）または Android（副次ターゲット）

### ビルドと実行

```powershell
git clone https://github.com/Skymly/GitPulse.git
cd GitPulse

# Nuke 経由（CI 同等：フォーマット + Windows ビルド + Android ビルド + ライブラリテスト）
./build.ps1 --target CiAll --configuration Release

# 従来の dotnet CLI
dotnet build GitPulse.slnx -c Release
dotnet run --project src/GitPulse.App/GitPulse.App.csproj -c Debug -f net10.0-windows10.0.19041.0
```

### GitHub アクセスの設定

初回起動時に **Settings** を開き、GitHub Personal Access Token（PAT）を貼り付けます。トークンはローカルで暗号化保存されます：

- Windows：DPAPI（CurrentUser スコープ）
- Android：SecureStorage

## 機能

- **リポジトリとコード**：個人・Star リポジトリ、ブランチ、リリース、コミット、ファイル（インライン編集対応）の閲覧に加え、Star / Watch / Fork やクローン URL コピーに対応。
- **Issue と Pull Request**：Issue / PR の一覧・作成・管理（ラベル、担当者、会話タイムライン、ファイル差分、レビュー、チェック実行、マージ／クローズ／ブランチ更新）に対応。
- **通知と検索**：未読バッジ付きの通知受信トレイ（該当 Issue / PR / Commit をアプリ内で直接表示）と、検索および Review / Assigned / Mentions ハブを提供。
- **Actions とネイティブ統合**：ワークフロー実行・ジョブの確認、`workflow_dispatch` の実行、Windows DPAPI / Android SecureStorage による PAT 暗号化保存、Windows トレイ／Toast 通知に対応。

今後の計画は[マイルストーンロードマップ](./docs/ROADMAP.md)、メンテナー向けドキュメント（ADR・設計書）は [`docs/`](./docs/README.md) を参照してください。

### ショーケース：Observables 連携

GitPulse は現在 2 つの Observables ドメインを活用しており、今後も拡張予定です：

| ドメイン | 用途 |
|----------|------|
| `Observables.RestAPI.R3` | 宣言的 GitHub REST API インターフェース — 定義を書くだけで `Observable<T>` プロキシを生成 |
| `Observables.Events.R3` | MAUI コントロールイベント → `Observable<T>` ストリーム（デバウンス検索、スクロール読込など） |

宣言的 API の例：

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

## 技術スタック

- .NET 10 + .NET MAUI
- R3 + R3Extensions.Maui（リアクティブコア）
- Observables.RestAPI.R3 + Observables.Events.R3（ソース生成リアクティブブリッジ）
- CommunityToolkit.Mvvm
- MVVM アーキテクチャ、マルチプロジェクト構成（App / ViewModels / Core / GitHubApi / Services）
- Nuke ビルドオーケストレーション
- MinVer（Git タグベースのバージョニング）

## プロジェクト構成

```
src/
  GitPulse.App/         — MAUI UI、DI、プラットフォームエントリーポイント
  GitPulse.ViewModels/  — ViewModels（R3、MAUI 非参照）
  GitPulse.Core/        — ドメインモデルと抽象定義（MAUI 非参照）
  GitPulse.GitHubApi/   — Observables.RestAPI 宣言的インターフェースと DTO
  GitPulse.Services/    — 認証、ポーリング、GitHub クライアントファクトリー
tests/
  GitPulse.Tests/       — 単体テスト
build/                  — Nuke ビルドスクリプト
docs/                   — ADR、サブシステム設計書、ロードマップ
.nuke/                  — Nuke パラメーターとスキーマ
```

## ビルドと CI（Nuke）

| Nuke ターゲット | 説明 |
|---|---|
| **Ci** | `Clean` → `Restore` → `Compile` → `UnitTest`（`Compile` は Android を含む） |
| **CiLib** | クロスプラットフォームライブラリテストのみ（App プロジェクト除外） |
| **CiAndroid** | Android App コンパイルゲートのみ（`net10.0-android`、APK/AAB は生成しない） |
| **CiAll** | `Format` + `Ci`（Android コンパイルを含む完全ローカル/CI 検証） |
| **Test** | `UnitTest` のエイリアス |
| **Format** | `dotnet format --verify-no-changes` |
| **FormatFix** | `dotnet format`（フォーマット自動適用） |
| **Publish** | 自己完結型 Windows exe → `artifacts/publish/{Runtime}/` |
| **PublishVerify** | `Publish` + エントリーポイント存在検証 |
| **Release** | `CiAll` + `PublishVerify` + `PublishAndroidVerify`（Windows zip + 署名済み APK） |

```powershell
./build.ps1 --target CiAll --configuration Release
./build.ps1 --target CiAndroid --configuration Release
./build.ps1 --target Publish --configuration Release --runtime win-x64
```

## バージョニング

バージョンは [MinVer](https://github.com/adamralph/minver) により Git タグから自動算出されます。`v*` タグをプッシュすると CI リリースジョブが起動し、バージョンが設定されます。

```powershell
git tag v0.1.0
git push origin v0.1.0
```

## ライセンス

MIT — [LICENSE](./LICENSE) を参照してください。
