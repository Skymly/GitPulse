# ADR-004: PAT 认证与平台凭据存储

| 字段 | 值 |
|------|-----|
| **状态** | Accepted |
| **日期** | 2026-03-01 |
| **关联 Issue** | 无 |

## 背景

需要安全存储 GitHub 凭据，且 Core 层不能依赖 MAUI 或 Windows DPAPI。

## 决策

- 仅支持 **Personal Access Token**（GitHub App OAuth 推迟）。
- Core 定义 `ICredentialStore` 抽象。
- 实现放在 **App** 项目：`WindowsCredentialStore`（DPAPI）、`AndroidCredentialStore`（SecureStorage）。
- `GitHubClientFactory`（Services）读取 token 并设置 `Authorization: Bearer`。

## 后果

- **正面**：Core/Services 可测试；平台 API 隔离在 App。
- **负面**：新增平台须新增 `ICredentialStore` 实现。

## 参考

- [design/Architecture.md](../design/Architecture.md)
