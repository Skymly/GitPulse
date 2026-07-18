# ADR-008: 拆分 GitHub Search API 接口

| 字段 | 值 |
|------|-----|
| **状态** | Accepted |
| **日期** | 2026-07-11 |
| **关联 Issue** | 无 |

## 背景

`IGitHubReposApi` 已覆盖仓库、Issue、PR、通知、Contents 与 Review 等资源。M9 引入 GitHub Search API，其端点、响应包装、查询语义和独立限流均与仓库资源 API 不同。继续扩展现有接口会模糊契约边界，并使后续 Actions 等域继续膨胀同一接口。

## 决策

- 新建 `IGitHubSearchApi`，声明 repository、issue/PR 和 code 三类 Search 端点；保留 `IGitHubReposApi` 不变。
- 两个接口通过同一 `IGitHubClientFactory` 创建的已认证 `HttpClient` 消费，共享 GitHub 基础地址和请求头。
- 搜索表达式 `q` 使用接口 `[Query]` 参数显式声明；`page` / `per_page` 继续由 `GitHubQueryHandler` 注入。
- 搜索方法返回 `ApiResponse<SearchResult<T>>`，以同时读取响应总数、结果项和分页 `Link` 头。
- Issue 与 PR 共用 `/search/issues`，调用方分别追加 `is:issue` 与 `is:pr`；代码搜索使用 `/search/code`。

## 后果

- **正面**：Search 契约、模型和独立限流边界清晰，后续 API 域可按同一原则拆分。
- **正面**：认证、分页和 HTTP 配置继续复用现有基础设施，无需修改工厂。
- **负面**：消费方需要按资源域选择接口，不能再从单一接口发现全部端点。
- **负面**：分页参数由 handler 注入，接口签名只显式呈现搜索表达式。

## 参考

- [design/Architecture.md](../design/Architecture.md)
- [design/RestApi.md](../design/RestApi.md)
- [GitHub REST API: Search](https://docs.github.com/en/rest/search/search)
