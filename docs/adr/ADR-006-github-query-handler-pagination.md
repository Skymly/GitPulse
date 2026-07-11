# ADR-006: GitHubQueryHandler 分页查询注入

| 字段 | 值 |
|------|-----|
| **状态** | Accepted |
| **日期** | 2026-04-01 |
| **关联 Issue** | 无 |

## 背景

列表 API 需要 `page` / `per_page` / `state` 等查询参数，同时 `IGitHubReposApi` 列表方法须返回 `ApiResponse<T>` 以读取 `Link` 头做 `CanLoadMore` 检测。Observables 0.1.4 曾限制 path 与 query 参数共存（OBS3004，已在 0.1.5 修复 body 共存）。

## 决策

- 在 **Core/Http** 使用 `GitHubQueryHandler`（`DelegatingHandler`）在请求发出前注入查询参数。
- 声明式接口方法仅保留 path 参数；分页状态由 ViewModel 通过 handler 或工厂配置。
- 需要 `Link` 的方法继续返回 `Observable<ApiResponse<T>>`。

## 后果

- **正面**：分页与 Link 检测无需改动生成器契约；CRUD 仍可在接口上声明 `[Body]`。
- **负面**：查询参数不在接口签名中显式可见，须读 Design Doc。

## 参考

- [design/RestApi.md](../design/RestApi.md)
- https://github.com/Skymly/Observables/issues/111
