# ADR-018: 逐请求 HttpRequestOptions 分页

| 字段 | 值 |
|------|-----|
| **状态** | Accepted |
| **日期** | 2026-09-21 |
| **关联 Issue** | [#606](https://github.com/Skymly/GitPulse/issues/606)（吸收 [#655](https://github.com/Skymly/GitPulse/issues/655)） |
| **取代** | [ADR-006](ADR-006-github-query-handler-pagination.md) |

## 背景

ADR-006 用 `GitHubQueryHandler` 实例字段注入 `page` / `per_page` / `state`，让声明式列表方法保持 path-only。那是 Observables 0.1.4 OBS3004（path 与 `[Query]` 不能共存）的绕过；0.1.5 已允许业务 query。

实例字段无锁。`PagedGitHubSession.Client` 公开，并发 `PrepareRequest` 会把分页参数串到别的请求上。`SearchAsync` 还能在飞行中 `DisposePaged()`。ADR-006 决策段仍写「接口只留 path」，与 Search `q`、check-run `filter`、contents `ref`、`sort` 等 `[Query]` 方法矛盾。

## 决策

- 业务查询参数在声明式接口上用 `[Query]`。
- 分页/列表过滤器 `page` / `per_page` / `state` 不作为 handler 实例字段，也不进入每个列表方法签名。由 `PagedGitHubSession` 在每次请求上写入 `HttpRequestOptions`；`GitHubQueryHandler` 无状态，只读 options 再注入 query。
- ViewModel 仍经 session：`Reset` / `PrepareRequest` / `ApplyLink` / `Advance` / `HasNextPage`（及可选 `State`）。Handler 不是 ViewModel 契约。
- 需要 `Link` 的方法继续返回 `Observable<ApiResponse<T>>`。
- 不删除 `CreatePagedClientAsync`（ADR-006 已列的 follow-up，仍有效）。

## 后果

- **正面**：同一 session 上重叠的 Load / Search 不会因共享 `Page` / `PerPage` / `State` 串线；ADR 与已有 `[Query]` 接口一致。
- **负面**：分页参数仍不在接口签名里，须读 Design Doc；Core 要在 RestAPI 发出的 `HttpRequestMessage` 上 stamp options。
- **后续**：Search 先取消上一轮再 dispose session（ViewModels）。删除 `CreatePagedClientAsync` 仍为 follow-up。

## 参考

- [ADR-006](ADR-006-github-query-handler-pagination.md)（Superseded）
- [design/RestApi.md](../design/RestApi.md)
- [CONTEXT.md](../CONTEXT.md)（Paged GitHub Session）
- `src/GitPulse.Core/Http/GitHubQueryHandler.cs`
- `src/GitPulse.Core/Http/PagedGitHubSession.cs`
- [#606](https://github.com/Skymly/GitPulse/issues/606)
