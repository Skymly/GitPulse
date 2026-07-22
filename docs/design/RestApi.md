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
| M9 | `/search/repositories`, `/search/issues`, `/search/code` |
| M10 ✅ | `/actions/runs`, run jobs, rerun, job logs |

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
- Issue/PR 项保留 `repository_url`，由消费方提取 owner/repo；代码项使用嵌套 repository 的 `full_name`，并保留 `path` 与 `sha`。
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

1. ViewModel 调用 `List*Paged` → `Observable<ApiResponse<T>>`
2. `GitHubQueryHandler` 注入 `page` / `per_page`（及 issues 的 `state`）
3. `LinkHeaderParser` 解析 `rel="next"` → `CanLoadMore`

### CRUD（M3+）

- `CreateIssue`、`UpdateIssue`、`CreateIssueComment` 等使用 `[Body]` DTO（`Core/Models/IssueRequests.cs`）
- PR 评论复用 issue comments 端点

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
