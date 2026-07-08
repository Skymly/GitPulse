# Spec: RestApi

> **版本**：Unreleased
> **关联 Design Doc**：[design/RestApi.md](../design/RestApi.md)
> **关联 ADR**：[ADR-002](../adr/ADR-002-observables-declarative-github-api.md)、[ADR-006](../adr/ADR-006-github-query-handler-pagination.md)

## 范围

通过 `IGitHubReposApi` 暴露的 GitHub REST 契约及消费约定。

## 接口面

- 单一入口接口：`IGitHubReposApi`（`GitPulse.GitHubApi`）
- 消费：`var api = RestService.For<IGitHubReposApi>(httpClient);`
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

## 不变量

1. Path 占位符名与 C# 参数名一致（Observables 路径校验）。
2. 分页列表不得改为 `Observable<T[]>` 若需 `Link` 头。
3. GitHub snake_case JSON 须在 Core 模型上用 `[JsonPropertyName]` 映射。

## 不在范围内

- GraphQL API
- GitHub Enterprise 自建实例（未测试）

## 兼容基线

- Observables.RestAPI.R3 **0.1.5+**（path + body 共存）
