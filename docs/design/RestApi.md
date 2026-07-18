# Design Doc: RestApi

> **版本**：Unreleased
> **关联 ADR**：[ADR-002](../adr/ADR-002-observables-declarative-github-api.md)、[ADR-006](../adr/ADR-006-github-query-handler-pagination.md)、[ADR-008](../adr/ADR-008-split-github-search-api-interface.md)

## 概述

ViewModel 通过 `IGitHubClientFactory` 获取带认证的 `HttpClient`，再按资源域创建 `IGitHubReposApi` 或 `IGitHubSearchApi` 代理。

## 范围

通过声明式接口暴露的 GitHub REST 契约及消费约定。

## 接口面

- 仓库资源接口：`IGitHubReposApi`（保持现有契约不变）
- Search 接口：`IGitHubSearchApi`（M9 设计中）
- 消费：`RestService.For<IGitHubReposApi>(client)` 或 `RestService.For<IGitHubSearchApi>(client)`
- HTTP 头（由 `GitHubClientFactory` 设置）：
  - `Authorization: Bearer <PAT>`
  - `Accept: application/vnd.github+json`
  - `X-GitHub-Api-Version: 2022-11-28`
  - `User-Agent: GitPulse`

## 方法分类

| 类别 | 返回类型 | 示例 |
|------|----------|------|
| 分页列表 | `Observable<ApiResponse<T[]>>` | `ListIssuesPaged`, `ListMyReposPaged` |
| 单资源 GET | `Observable<T>` | `GetRepo`, `GetIssue` |
| 写操作 | `Observable<T>` + `[Body]` | `CreateIssue`, `MergePullRequest` |
| 无 body DELETE | `Observable<Unit>` | `MarkThreadRead` |

## 里程碑 API 面（已实现）

| 里程碑 | 端点域 |
|--------|--------|
| M1 | `/user/repos` |
| M2 | issues, pulls, comments |
| M3 | issue CRUD, labels |
| M4 | `/notifications` |
| M5 | `/contents/{path}` |
| M6 | pull merge |
| M7 | readme, branches, releases |
| M8 | pull files, pull review comments |

## M9 Search（设计中）

| 类型 | 端点 | 查询约定 |
|------|------|----------|
| 仓库 | `GET /search/repositories` | `[Query] q` |
| Issue | `GET /search/issues` | `[Query] q` + `is:issue` |
| PR | `GET /search/issues` | `[Query] q` + `is:pr` |
| 代码 | `GET /search/code` | `[Query] q` |

- 方法返回 `Observable<ApiResponse<SearchResult<T>>>`；`SearchResult<T>` 提供 `total_count`、`incomplete_results` 和 `items`。
- `q` 在 `IGitHubSearchApi` 上显式声明；`page` / `per_page` 由 `GitHubQueryHandler` 注入，并从 `Link` 头判断下一页。
- Issue/PR 项保留 `repository_url`，由消费方提取 owner/repo；代码项使用嵌套 repository 的 `full_name`，并保留 `path` 与 `sha`。
- Search 与仓库 API 共用工厂创建的认证 `HttpClient`，但遵守 GitHub Search 独立限流。

## 不变量

1. Path 占位符名与 C# 参数名一致（Observables 路径校验）。
2. 分页列表不得改为 `Observable<T[]>` 若需 `Link` 头。
3. GitHub snake_case JSON 须在 Core 模型上用 `[JsonPropertyName]` 映射。
4. Search 的 `q` 必须保留在声明式接口签名中；分页参数继续由 handler 注入。

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

- **QueryHandler vs `[Query]`**：业务查询 `q` 使用 `[Query]` 明示；通用分页继续使用 Handler 以统一 Link 检测。
- **404 处理**：README 等可选资源在 ViewModel 层吞掉 NotFound，不失败整页加载。

## 已知局限

- Observables 对非 2xx 的异常类型因版本而异；可选端点用 `IsNotFoundError` 辅助判断。
- 大 diff 用 WebView HTML 渲染，非原生控件。

## 不在范围内

- GraphQL API
- GitHub Enterprise 自建实例（未测试）

## 兼容基线

- Observables.RestAPI.R3 **0.1.5+**（path + body 共存）

## 参考

- `src/GitPulse.GitHubApi/IGitHubReposApi.cs`
- `src/GitPulse.Core/Http/GitHubQueryHandler.cs`
