# Design Doc: RestApi

> **版本**：Unreleased
> **关联 ADR**：[ADR-002](../adr/ADR-002-observables-declarative-github-api.md)、[ADR-006](../adr/ADR-006-github-query-handler-pagination.md)、[ADR-008](../adr/ADR-008-split-github-search-api-interface.md)、[ADR-009](../adr/ADR-009-split-github-actions-api-interface.md)

## 概述

ViewModel 通过 `IGitHubClientFactory` 获取带认证的 `HttpClient`，再按资源域创建 `IGitHubReposApi`、`IGitHubSearchApi` 或 `IGitHubActionsApi` 代理。

## 范围

通过声明式接口暴露的 GitHub REST 契约及消费约定。

## 接口面

- 仓库资源接口：`IGitHubReposApi`（保持现有契约不变）
- Search 接口：`IGitHubSearchApi`
- Actions 接口：`IGitHubActionsApi`（M10；见 [ADR-009](../adr/ADR-009-split-github-actions-api-interface.md)）
- 消费：`RestService.For<TApi>(client)` 按域选择接口
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
| 写操作 | `Observable<T>` + `[Body]` | `CreateIssue`, `CreatePullRequest`, `MergePullRequest` |
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
| M9 | `/search/repositories`, `/search/issues`, `/search/code` |
| M10 ✅ | `/actions/runs`, run jobs, rerun, job logs |
| M14 ✅ | `POST /repos/{owner}/{repo}/pulls` (`CreatePullRequest` + create-time Draft PR); PR title/body edit via existing `UpdateIssue` |
| M15 ✅ | `GET /user` (`GetAuthenticatedUser`); `GET/POST /repos/{owner}/{repo}/pulls/{number}/reviews` (`ListPullRequestReviews`, `CreatePullRequestReview`) |
| M16 ✅ | `GET /repos/{owner}/{repo}/commits/{ref}/check-runs` (`ListCheckRunsForRef`); `GET /repos/{owner}/{repo}/commits/{ref}/status` (`GetCombinedStatusForRef`) |
| M17 ✅ | `GET /user/starred` (`ListStarredReposPaged`) |
| M18 ✅ | `GET /repos/{owner}/{repo}/commits` (`ListCommitsPaged`) |
| M19 ✅ | `GET /repos/{owner}/{repo}/commits/{ref}` (`GetCommit`, non-paged) |
| M20 ✅ | Review inbox reuses `GET /search/issues` (`SearchPullRequests`, canned review-requested:@me) |
| M21 ✅ | `GET/POST/DELETE /repos/{owner}/{repo}/pulls/{number}/requested_reviewers` |
| M22 ✅ | `GET /repos/{owner}/{repo}/check-runs/{checkRunId}` (`GetCheckRun`, non-paged) |
| M23 ✅ | Settings reuses `GET /user` (`GetAuthenticatedUser`) to verify a PAT before persist |
| M24 ✅ | `GET/PUT/DELETE /user/starred/{owner}/{repo}` (`GetStarredRepo`, `StarRepo`, `UnstarRepo`) |
| M25 ✅ | `GET /user/repos?sort=pushed` (`ListMyReposSortedPaged`) |
| M26 ✅ | Commit detail reuses check-runs + combined status (no new method) |
| M27 ✅ | `GET /repos/{owner}/{repo}/check-runs/{checkRunId}/annotations` (`ListCheckRunAnnotations`) |
| M28 ✅ | Search / Review inbox use `PagedGitHubSession` |
| M29 ✅ | Assigned inbox reuses `GET /search/issues` (`SearchIssues`, canned assignee:@me) |
| M30 ✅ | `GET/PUT/DELETE /repos/{owner}/{repo}/subscription` (`GetRepoSubscription`, `SetRepoSubscription`, `DeleteRepoSubscription`) |
| M31 ✅ | `GET /repos/{owner}/{repo}/contents/{path}?ref=` (`GetFileContentAtRef`) |
| M32 ✅ | `POST /repos/{owner}/{repo}/check-runs/{checkRunId}/rerequest` (`RerequestCheckRun`) |
| M33 ✅ | `POST/DELETE /repos/{owner}/{repo}/issues/{number}/assignees` (`AddIssueAssignees`, `RemoveIssueAssignees`) |

## M9 Search

| 类型 | 端点 | 查询约定 |
|------|------|----------|
| 仓库 | `GET /search/repositories` | `[Query] q` |
| Issue | `GET /search/issues` | `[Query] q` + `is:issue` |
| PR | `GET /search/issues` | `[Query] q` + `is:pr` |
| 代码 | `GET /search/code` | `[Query] q` |

- 方法返回 `Observable<ApiResponse<SearchResult<T>>>`；`SearchResult<T>` 提供 `total_count`、`incomplete_results` 和 `items`。
- `q` 在 `IGitHubSearchApi` 上显式声明；`page` / `per_page` 由 `GitHubQueryHandler` 注入，并从 `Link` 头判断下一页。
- `SearchViewModel` 在传入接口前对完整查询表达式做 URI 编码，避免 `#` 等保留字符被解释为 URI 片段。
- Issue/PR 项保留 `repository_url`，由消费方提取 owner/repo；`SearchIssueItem.RepositoryFullName` 为计算属性。代码项使用嵌套 repository 的 `full_name`，并保留 `path` 与 `sha`。
- Search 与仓库 API 共用工厂创建的认证 `HttpClient`，但遵守 GitHub Search 独立限流。
- 输入防抖只同步查询状态；至少 3 个字符并显式按 Enter 或 Search 后才请求。切换类型不请求 API。
- 新搜索会取消前一请求并递增请求版本；只有当前版本可写入结果，避免过期响应覆盖。
- 403 映射为 Search 限流提示，422 映射为查询语法提示；其他 HTTP 错误使用通用失败状态。

## 不变量

1. Path 占位符名与 C# 参数名一致（Observables 路径校验）。
2. 分页列表不得改为 `Observable<T[]>` 若需 `Link` 头。
3. GitHub snake_case JSON 须在 Core 模型上用 `[JsonPropertyName]` 映射。
4. Search 的 `q` 必须保留在声明式接口签名中；分页参数继续由 handler 注入。

## 实现概览

### 分页

列表分页（Repos / Issues / PRs / WorkflowRuns）经 **Paged GitHub Session**（`PagedGitHubSession`，`IGitHubClientFactory.CreatePagedSessionAsync`）：

1. ViewModel：`Reset` → `PrepareRequest` → `List*Paged`（`Observable<ApiResponse<T>>`）→ `ApplyLink`；Load more：`Advance`（当 `HasNextPage`）→ `PrepareRequest` → 再请求 → `ApplyLink`；`CanLoadMore` 映射自 `HasNextPage`
2. Session 内部用 `GitHubQueryHandler` 注入 `page` / `per_page`（及 Issues/PRs 的 `state`）；handler 不是 ViewModel 面向契约
3. Session 用 `LinkHeaderParser` 解析 `rel="next"` → `HasNextPage`

Search / Review inbox / Assigned inbox 使用 `PagedGitHubSession`。

### CRUD（M3+）

- `CreateIssue`、`UpdateIssue`、`CreateIssueComment` 等使用 `[Body]` DTO（`Core/Models/IssueRequests.cs`）
- PR 评论复用 issue comments 端点

### Create PR（M14）

- `CreatePullRequest`：`POST /repos/{owner}/{repo}/pulls`，path + `[Body]` `PullRequestCreateRequest`（`title` / `head` / `base` / 可选 `body` / 可选 create-time `draft`）
- 同仓 head/base；分支来源复用已有 `ListBranches`；不包含跨 fork、draft ↔ ready 生命周期或 GraphQL
- PR detail 编辑 title/body 复用 `UpdateIssue`（issues PATCH；与 open/close 同一 number 空间），不新增 pulls PATCH

### M8 Diff

- `ListPullRequestFiles`、`ListReviewComments`、`CreateReviewComment`
- `PrDiffViewModel` 并行加载 files + comments，按 `path` 分组

#### Windows 手工验收清单（带 PAT）

- [ ] 多文件、单文件多 hunk 的 patch 均按文件正确渲染
- [ ] 明暗主题下新增、删除与上下文行对比度可读
- [ ] binary 文件显示不可渲染状态，不尝试文本 diff
- [ ] 大 patch 可连续滚动且页面保持响应
- [ ] 现有 review comments 显示在对应文件下
- [ ] 可创建新行内评论并回复现有评论
- [ ] PAT 无效（401）和资源不存在（404）显示可操作错误
- [ ] 页面切换或进入详情后返回时 Tab 与滚动状态符合预期

### M9 Search 实现

- `SearchViewModel` 分别维护 repository、Issue、PR 与 code 结果域，并保存各域分页会话。
- Search 页为 Shell 第四个主 Tab；Repo/Issue/PR 结果进入现有详情页，代码结果直接进入 `FileEditorPage`。
- `SearchBar.TextChanged` 通过手动 R3 `Subject<string>` 桥接；防抖不触发网络请求。

#### API 级自动化（可选，`GITPULSE_TEST_PAT`）

`SearchLiveIntegrationTests`（`Category=Integration`）在设置环境变量后对真实 Search API 断言：四类搜索与总数、编码、Issue/PR 限定、空结果、422 语法错误、切换类型不自动请求、以及 `Link` 分页。运行方式见 [DEVELOPMENT.md](../DEVELOPMENT.md)。未设置 PAT 时这些用例 Skip，不进入默认 `CiLib` 失败路径。

#### Windows 实机验收清单（M9 — 2026-07-18 关闭）

**API 级（`SearchLiveIntegrationTests` + `GITPULSE_TEST_PAT`，10/10 通过）：**

- [x] repository、Issue、PR、code 四类搜索均返回并展示结果总数
- [x] 空格、斜杠、`#` 等查询字符正确编码，未被截断或双重编码
- [x] Issue 请求包含 `is:issue`，PR 请求包含 `is:pr`
- [x] `Link` 存在时可加载下一页，末页后隐藏 Load more
- [x] 空结果显示明确状态，切换类型不会自动发送请求
- [x] 触发 422 非法查询时显示查询语法提示

**UI / 导航（静态验收：对照 `SearchPage` 与 `AppShell` 路由）：**

- [x] repository → `RepoDetailPage?owner&repo`（`FullName` 拆分）
- [x] Issue / PR → `IssueDetailPage` / `PullRequestDetailPage`（`repository_url` → `/repos/{owner}/{repo}`）
- [x] code → `FileEditorPage?owner&repo&path&sha`
- [x] SearchBar：防抖仅更新 `Query`；`SearchButtonPressed` / Search 按钮调用 `SubmitSearch` → `SearchCommand`
- [ ] 403 限流文案（可选；难稳定复现，保留手工抽检）

无头 Mock 不能替代实网 API；导航项已与已注册 Shell 路由交叉核对。可选：作者本机再做一次点击冒烟。

### M10 Actions ✅

见 [ADR-009](../adr/ADR-009-split-github-actions-api-interface.md)。首批端点：

| 能力 | 方法 |
|------|------|
| 列出 runs | `GET /repos/{owner}/{repo}/actions/runs` |
| run 详情 | `GET /repos/{owner}/{repo}/actions/runs/{run_id}` |
| run jobs | `GET /repos/{owner}/{repo}/actions/runs/{run_id}/jobs` |
| 重跑 run | `POST /repos/{owner}/{repo}/actions/runs/{run_id}/rerun` |
| job 日志 | `GET /repos/{owner}/{repo}/actions/jobs/{job_id}/logs` |

列表返回 `ApiResponse<T>`；日志下载需处理重定向。Windows 托盘 / Toast 见 [ADR-010](../adr/ADR-010-windows-tray-presence-and-toast.md) 与 [Events.md](Events.md)（App/platform，非本 API 文档范围）。


### Pull Request Reviews (M15)

Immediate submit only (no pending review). Methods live on `IGitHubReposApi` on purpose — no fourth interface:

| 方法 | 端点 | 说明 |
|------|------|------|
| `GetAuthenticatedUser` | `GET /user` | Authenticated `User.Login` for author vs viewer compare. Lives on `IGitHubReposApi` so PR detail does not add a Users API surface. |
| `ListPullRequestReviews` | `GET /repos/{owner}/{repo}/pulls/{number}/reviews` | First page `Observable<PullRequestReview[]>`; omit PENDING in the ViewModel. Listed `state` is GitHub’s submitted state, not the create-time Review Event. |
| `CreatePullRequestReview` | `POST /repos/{owner}/{repo}/pulls/{number}/reviews` | Immediate submit: `event` is required (`APPROVE` / `REQUEST_CHANGES` / `COMMENT`). Does not send `comments[]` (M8 `CreateReviewComment` stays independent). |

`PullRequestReview` / `PullRequestReviewCreateRequest` are Core models. Body is required for COMMENT and REQUEST_CHANGES; optional for APPROVE. Authors cannot APPROVE or REQUEST_CHANGES (GitHub rejects self-review of those events).

### PR head Gate Rollup (M16)

Read-only. Methods live on `IGitHubReposApi` — no `IGitHubChecksApi` / ADR-015. `{ref}` is the PR `Head.Sha`. Combined status does **not** include Check Runs.

| 方法 | 端点 | 说明 |
|------|------|------|
| `ListCheckRunsForRef` | `GET /repos/{owner}/{repo}/commits/{ref}/check-runs` | `[Query] filter` (`latest` from PR detail). First page `Observable<CheckRunsResult>`. |
| `GetCombinedStatusForRef` | `GET /repos/{owner}/{repo}/commits/{ref}/status` | Combined Commit Statuses only. Empty `statuses` is GitHub `pending`; Gate Rollup does not treat that as pending by itself. |

`CheckRun` / `CheckRunsResult` / `CommitStatus` / `CombinedCommitStatus` are Core models. ViewModel computes Gate Rollup (pending / success / failure / no checks). Missing `Head.Sha` skips both calls. Either endpoint failing leaves the PR page intact.

### Starred repos (M17)

Read-only. Lives on `IGitHubReposApi` and reuses `Repo` — no new interface or Core model.

| 方法 | 端点 | 说明 |
|------|------|------|
| `ListStarredReposPaged` | `GET /user/starred` | `Observable<ApiResponse<Repo[]>>`; page / `Link` via Paged GitHub Session. |

Repos tab switches My repos (`ListMyReposPaged`) vs Starred. Each hub owns its own session. Local `SearchText` filters the active list. No star/unstar writes.

### Repo commit detail (M19)

Read-only. Lives on `IGitHubReposApi` — no fourth interface. List paging stays M18 (`ListCommitsPaged` + Paged GitHub Session). Get-a-commit is **non-paged** (`CreateClientAsync`, not `PagedGitHubSession`) so query-handler `page` / `per_page` cannot shrink the file list. First page of files only (GitHub cap ~300).

| 方法 | 端点 | 说明 |
|------|------|------|
| `GetCommit` | `GET /repos/{owner}/{repo}/commits/{ref}` | `Observable<GitCommit>` (not `ApiResponse`). Path `{ref}` is C# `@ref` (same as check-runs). |

`GitCommit` reuses the list type with optional `stats` and `files`. Files use the existing diff-entry shape (`filename`, `status`, line counts, URLs, optional `patch`). Missing SHA skips the call. HTTP failure stays on the commit page; the Commits list is unchanged. A null `patch` is omitted from in-app DiffView and is not a page-level error.

### Review inbox (M20)

Read-only. Reuses `IGitHubSearchApi.SearchPullRequests` — no new method or interface. Canned query is GitHub.com Review requested: `is:open is:pr review-requested:@me archived:false`. Does **not** append a second `is:pr`. Does **not** apply the typed-Search 3-character minimum. Own session so typed PR search is not mixed. Uses `PagedGitHubSession` (M28).

Search tab switches Search (existing) vs Review requested. Rows show `SearchIssueItem.RepositoryFullName` plus title / number / state / author. Tap opens existing PR detail. Empty inbox is quiet. Notifications are not the source of truth.

### Assigned inbox (M29)

Read-only. Reuses `IGitHubSearchApi.SearchIssues` — no new method or interface. Canned query is GitHub.com Assigned: `is:open assignee:@me archived:false`. Does **not** append `is:issue`. Does **not** apply the typed-Search 3-character minimum. Own session so typed Issue search and Review requested are not mixed. Uses `PagedGitHubSession`.

Search tab switches Search / Review requested / Assigned. Rows show `SearchIssueItem.RepositoryFullName` plus title / number / state / author. Tap opens Issue detail, or PR detail when `pull_request` is present. Empty inbox is quiet. Notifications are not the source of truth.

### Request reviewers (M21)

Write + read. Lives on `IGitHubReposApi` — no fourth interface. Distinct from submitted Pull Request Reviews (M15).

| 方法 | 端点 | 说明 |
|------|------|------|
| `ListRequestedReviewers` | `GET /repos/{owner}/{repo}/pulls/{number}/requested_reviewers` | Pending users + teams. After a reviewer submits, they leave this list. |
| `RequestReviewers` | `POST .../requested_reviewers` | Body `reviewers[]` logins. Returns `ApiResponse<PullRequest>` so 403/422 stay on the PR page. |
| `RemoveRequestedReviewers` | `DELETE .../requested_reviewers` | Same body shape. Teams are display-only in v0.9.0. |

Load failure of this call does not fail PR detail. Closed/merged PRs cannot manage reviewers.

### Check Run detail (M22)

Read-only. Lives on `IGitHubReposApi` — no `IGitHubChecksApi`. Non-paged `CreateClientAsync`.

| 方法 | 端点 | 说明 |
|------|------|------|
| `GetCheckRun` | `GET /repos/{owner}/{repo}/check-runs/{checkRunId}` | `Observable<CheckRun>` with optional `output` (title / summary / text). |

PR Gate Rollup Open navigates in-app. Annotations / rerequest / commit-page rollup are out.

### Verify PAT (M23)

Settings does not add an API method. Save calls existing `GetAuthenticatedUser` with the typed token (Authorization overwritten on a one-off client). 401/403 do not persist. Viewer login is shown when GET /user succeeds.

### Star toggle (M24)

Write + read on `IGitHubReposApi`. No new Core model.

| 方法 | 端点 | 说明 |
|------|------|------|
| `GetStarredRepo` | `GET /user/starred/{owner}/{repo}` | 204 starred, 404 not starred. `ApiResponse<Unit>`. |
| `StarRepo` | `PUT /user/starred/{owner}/{repo}` | 204. |
| `UnstarRepo` | `DELETE /user/starred/{owner}/{repo}` | 204. |

Check failure does not fail repo detail. 403 stays on the page.

### Watch toggle (M30)

Write + read on `IGitHubReposApi`. Core models: `RepoSubscription`, `RepoSubscriptionRequest`.

| 方法 | 端点 | 说明 |
|------|------|------|
| `GetRepoSubscription` | `GET /repos/{owner}/{repo}/subscription` | 200 watching payload, 404 not watching. Watching means `subscribed=true` and `ignored=false`. |
| `SetRepoSubscription` | `PUT /repos/{owner}/{repo}/subscription` | Body `{subscribed:true, ignored:false}` to watch. |
| `DeleteRepoSubscription` | `DELETE /repos/{owner}/{repo}/subscription` | 204 unwatch. |

Check failure does not fail repo detail. 403 stays on the page. Ignore / releases-only / watching list are out of scope.

### Recently pushed repos (M25)

My repos hub uses `ListMyReposSortedPaged("pushed")` → `GET /user/repos?sort=pushed`. Starred hub unchanged. Existing `ListMyReposPaged` remains for compatibility.

### Commit Gate Rollup (M26)

Read-only. Reuses `ListCheckRunsForRef` (`filter=latest`) and `GetCombinedStatusForRef` on the commit SHA. Same client Gate Rollup as PR detail. Either call failing leaves the commit page intact. Open navigates to the M22 Check Run page.

### Check Run annotations (M27)

Read-only. Lives on `IGitHubReposApi`. First page only, non-paged client.

| 方法 | 端点 | 说明 |
|------|------|------|
| `ListCheckRunAnnotations` | `GET /repos/{owner}/{repo}/check-runs/{checkRunId}/annotations` | `Observable<CheckRunAnnotation[]>`. |

Load failure is a quiet empty list; the Check Run page stays intact.

### Annotation file at head (M31)

Read-only. `GetFileContentAtRef` is `GET /repos/{owner}/{repo}/contents/{path}?ref=`. The query is a git ref (Check Run `head_sha`), **not** a Contents blob SHA. FileEditor loads that blob and disables save/delete. Tapping an annotation opens FileEditor with `path` + `ref=head_sha`. Line scroll is out of scope.

### Rerequest Check Run (M32)

Write. Lives on `IGitHubReposApi`. `RerequestCheckRun` is `POST /repos/{owner}/{repo}/check-runs/{checkRunId}/rerequest` returning `ApiResponse<Unit>` so 403/422 stay on the page. Success is quiet and does not invent a new run row. Check-suite rerequest and live polling are out of scope.

### Issue assignees (M33)

Write + read. Lives on `IGitHubReposApi`. Current assignees come from GET issue (`Issue.Assignees`). Add/remove use `AssigneesRequest` logins.

| 方法 | 端点 | 说明 |
|------|------|------|
| `AddIssueAssignees` | `POST /repos/{owner}/{repo}/issues/{number}/assignees` | Body `assignees[]` logins. `ApiResponse<Issue>` so 403/422 stay on the page. |
| `RemoveIssueAssignees` | `DELETE .../assignees` | Same body. |

Suggested assignees and team assignees are out of scope.








## 设计权衡- **QueryHandler vs `[Query]`**：业务查询 `q` 使用 `[Query]` 明示；通用分页继续由 Handler 注入，并由 Paged GitHub Session 统一 cursor / Link / dispose。
- **Paged GitHub Session vs 元组工厂**：列表与 Search ViewModel 面向 session；`CreatePagedClientAsync` 仍留在工厂上，删除为 follow-up。
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
- `src/GitPulse.Core/Http/PagedGitHubSession.cs`
- `src/GitPulse.Core/Http/GitHubQueryHandler.cs`
- `src/GitPulse.Core/Abstractions/IGitHubClientFactory.cs`

