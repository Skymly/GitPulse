# Design Doc: RestApi

> **关联 Spec**：[spec/RestApi.md](../spec/RestApi.md)
> **关联 ADR**：[ADR-002](../adr/ADR-002-observables-declarative-github-api.md)、[ADR-006](../adr/ADR-006-github-query-handler-pagination.md)

## 概述

ViewModel 通过 `IGitHubClientFactory.CreateClientAsync()` 获取带认证的 `HttpClient`，再 `RestService.For<IGitHubReposApi>(client)`。

## 实现概览

### 分页

1. ViewModel 调用 `List*Paged` → `Observable<ApiResponse<T>>`
2. `GitHubQueryHandler` 注入 `page` / `per_page`（及 issues 的 `state`）
3. `LinkHeaderParser` 解析 `rel="next"` → `CanLoadMore`

### CRUD（M3+）

- `CreateIssue`、`UpdateIssue`、`CreateIssueComment` 等使用 `[Body]` DTO（`Core/Models/IssueRequests.cs`）
- PR 评论复用 issue comments 端点

### M8 Diff

- `ListPullRequestFiles`、`ListReviewComments`、`CreateReviewComment`
- `PrDiffViewModel` 并行加载 files + comments，按 `path` 分组

## 设计权衡

- **QueryHandler vs `[Query]`**：保留 Handler 以统一分页与 Link 检测，避免接口膨胀。
- **404 处理**：README 等可选资源在 ViewModel 层吞掉 NotFound，不失败整页加载。

## 已知局限

- Observables 对非 2xx 的异常类型因版本而异；可选端点用 `IsNotFoundError` 辅助判断。
- 大 diff 用 WebView HTML 渲染，非原生控件。

## 参考

- `src/GitPulse.GitHubApi/IGitHubReposApi.cs`
- `src/GitPulse.Core/Http/GitHubQueryHandler.cs`
