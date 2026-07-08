# Plan: Milestone M7 + M8 收尾

> **状态**：Active
> **创建**：2026-07-08
> **更新**：2026-07-08
> **关联 RFC**：无（引入文档体系前已启动；后续 M9+ 须 RFC）
> **关联 Issue**：（待建）
> **关联 Roadmap**：M7、M8

## 目标

完成仓库详情页（M7）与 PR Diff 查看器（M8）的实现、测试、文档与提交，使 `CiLib` + Windows App 编译通过。

## 非目标

- M9 Search
- Android 专项适配
- v0.1.0 发版

## 里程碑拆解

| 阶段 | 内容 | 模块 | 状态 | PR |
|------|------|------|------|-----|
| P1 | Core 模型（Branch、Release、DiffEntry、PullRequestHead 等） | Core | [x] | — |
| P2 | `IGitHubReposApi` M7/M8 端点 | GitHubApi | [x] | — |
| P3 | RepoDetailViewModel、PrDiffViewModel | ViewModels | [x] | — |
| P4 | RepoDetailPage、DiffView、PR Files 标签 | App | [x] | — |
| P5 | MauiProgram DI、Mica、修复损坏 XAML | App | [x] | — |
| P6 | 单元测试（RepoDetail、PrDiff） | Tests | [x] | — |
| P7 | 文档驱动体系（docs/）+ AGENTS 整理 | Docs | [x] | — |
| P8 | 分模块提交 + CHANGELOG 定稿 | Repo | [ ] | — |

## 验收标准

- [x] `CiLib` Release 190 测试通过
- [x] `net10.0-windows10.0.19041.0` App Release 编译通过
- [ ] M7/M8 变更已提交（建议分 2–3 个 PR：Core+Api+VM / App / Docs）
- [ ] `CHANGELOG.md` M8 条目补全
- [ ] `docs/ROADMAP.md` M7/M8 移入已完成

## 风险与依赖

- Observables 404 异常形态不统一 → `TryGetReadmeAsync` 已做兼容
- 大 PR diff WebView 性能未压测

## 变更记录

| 日期 | 调整 | 原因 |
|------|------|------|
| 2026-07-08 | 增加 P7 文档体系 | 引入 DesignPatterns 文档驱动开发 |
