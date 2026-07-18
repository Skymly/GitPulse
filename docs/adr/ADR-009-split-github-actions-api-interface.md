# ADR-009: 拆分 GitHub Actions API 接口

| 字段 | 值 |
|------|-----|
| **状态** | Accepted |
| **日期** | 2026-07-18 |
| **关联 Issue** | 无 |

## 背景

M10 引入 GitHub Actions（workflow runs、jobs、重跑、日志）。这些端点属于独立 REST 资源域，响应包装（`workflow_runs` / `jobs` 列表）、写操作（rerun）与日志下载（重定向）语义均不同于 `IGitHubReposApi` 与 `IGitHubSearchApi`。继续扩展现有接口会重复 ADR-008 已避免的契约膨胀。

## 决策

- 新建 `IGitHubActionsApi`，与 `IGitHubReposApi`、`IGitHubSearchApi` 并列；三者共享 `IGitHubClientFactory` 创建的已认证 `HttpClient`。
- M10 首批声明：
  - `GET /repos/{owner}/{repo}/actions/runs` — 列出仓库 workflow runs
  - `GET /repos/{owner}/{repo}/actions/runs/{run_id}` — run 详情
  - `GET /repos/{owner}/{repo}/actions/runs/{run_id}/jobs` — run 的 jobs
  - `POST /repos/{owner}/{repo}/actions/runs/{run_id}/rerun` — 重跑整个 run
  - `GET /repos/{owner}/{repo}/actions/jobs/{job_id}/logs` — job 日志（跟随/处理重定向）
- 分页：`page` / `per_page` 继续由 `GitHubQueryHandler` 注入；列表方法返回 `ApiResponse<T>` 以便读取 `Link`。
- Windows 系统托盘与 Toast 属于 **App/platform** 切片，不进入此 API 接口决策；可与 Actions UI 分 PR 交付。

## 后果

- **正面**：Actions 契约与限流边界清晰，后续可按域扩展（artifacts、cancel 等）而不污染仓库资源接口。
- **正面**：认证与分页基础设施复用不变。
- **负面**：消费方须按域选择接口；日志下载需显式处理 HTTP 重定向与短时 URL。

## 参考

- [design/Architecture.md](../design/Architecture.md)
- [design/RestApi.md](../design/RestApi.md)
- [GitHub REST API: Workflow runs](https://docs.github.com/en/rest/actions/workflow-runs)
- [GitHub REST API: Workflow jobs](https://docs.github.com/en/rest/actions/workflow-jobs)
- [ADR-008](ADR-008-split-github-search-api-interface.md)
