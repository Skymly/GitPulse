# ADR-002: Observables 声明式 GitHub API

| 字段 | 值 |
|------|-----|
| **状态** | Accepted |
| **日期** | 2026-03-01 |
| **关联 Issue** | 无 — 项目核心目标 |

## 背景

GitPulse 的首要目的是展示 [Observables](https://github.com/Skymly/Observables) 的 RestAPI 域：用接口 + 特性描述 HTTP，编译期生成 `HttpClient` 代理，消费端得到 `Observable<T>`。

## 决策

- 所有 GitHub REST 调用通过 `IGitHubReposApi`（及未来接口）声明。
- 使用 `Observables.RestAPI.R3` + `RestService.For<T>(httpClient)`。
- 需要响应头的列表方法返回 `Observable<ApiResponse<T>>`（Link 分页）。
- 写操作使用 path + `[Body]` 参数（Observables 0.1.5+）。

## 后果

- **正面**：API 面即文档；与 R3 管道自然组合；契合展示目标。
- **负面**：上游生成器限制需在工作区记录（见 ADR-006、ADR-007）；接口变更须更新 Design Doc。

## 参考

- [design/RestApi.md](../design/RestApi.md)
- https://github.com/Skymly/Observables/issues/111
