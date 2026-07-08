# 文档体系标准

> **权威源**。本文档定义 GitPulse 的**文档驱动开发（Documentation-Driven Development）**体系。人类开发者与 AI Agent 均须遵守。`AGENTS.md`「文档体系」章节为本文档的精简摘要。
>
> - **核心原则**：**先文档后代码**——非琐碎变更先满足文档前置条件再实现（决策表见 [§11](#11-文档驱动开发流程)）。
> - **语言**：内部维护者文档以**中文**为主；Issue / PR / Commit / 代码标识符以**英语**为主。
> - **冲突优先级**：`AGENTS.md` > `docs/DOCUMENTATION.md` > 其他文档。

本体系源自 [DesignPatterns](https://github.com/Skymly/DesignPatterns) 仓库的文档标准，并按 GitPulse（MAUI 应用 + Observables 展示）做了适配。

---

## 1. 文档类型总览

| 类型 | 目录 | 用途 | 稳定性 | 变更门槛 |
|------|------|------|--------|----------|
| **RFC** | `docs/rfc/` | 设计提案与讨论 | 提案阶段 | 自由修改（Review 前） |
| **ADR** | `docs/adr/` | 架构决策记录 | 已决策，仅 Supersede | 不修改原文 |
| **Spec** | `docs/spec/` | 稳定契约（API、模型、不变量） | 版本化稳定 | 需 RFC + ADR |
| **Design Doc** | `docs/design/` | 实现细节、权衡、局限 | 随实现演进 | 随代码 PR 更新 |
| **Roadmap** | `docs/ROADMAP.md` | 里程碑 backlog | 滚动维护 | 维护者评审 |
| **Plan** | `docs/plans/` / GitHub Issue | 任务计划 | 短生命周期 | 计划内自由更新 |
| **Review** | `docs/review/` | 评审记录 | Final 后不可变 | 仅勾选行动项 |

### 1.1 不作为独立文档类型

| 内容 | 载体 |
|------|------|
| Agent 上下文、跨模块边界、编码约定 | `AGENTS.md` |
| 环境、构建、测试 | `docs/DEVELOPMENT.md` |
| 变更日志 | `CHANGELOG.md` |
| 贡献流程 | `CONTRIBUTING.md` |
| 用户向简介 | `README.md` |

---

## 2. RFC — 何时需要

| 场景 | 需要 RFC？ |
|------|-----------|
| 新里程碑 / 新 GitHub API 域（如 Search、Actions） | ✅ 必须 |
| `IGitHubReposApi` 破坏性变更 | ✅ 必须 |
| Core 模型破坏性变更（序列化契约） | ✅ 必须 |
| 跨层架构变更（新项目、依赖方向调整） | ✅ 必须 |
| 单模块内 bug fix | ❌ Issue + PR |
| 单模块内非破坏性 API 新增 | ❌ Issue + PR（更新 Design Doc） |
| 文档 / 测试 / 重构（无行为变更） | ❌ Issue + PR |

命名：`docs/rfc/<PascalCaseName>.md`。模板：[`rfc/_template.md`](rfc/_template.md)。

生命周期：`Draft → Review → Accepted → Implemented → archive/`（或 `Rejected`）。

---

## 3. ADR — 架构决策

记录**最终**架构决策；讨论在 RFC 中完成。命名：`docs/adr/ADR-<NNN>-<kebab-case>.md`。模板：[`adr/_template.md`](adr/_template.md)。

**不可变**：Accepted 后正文不修改；推翻时新建 ADR 并将旧 ADR 标为 `Superseded by ADR-XXX`。

---

## 4. Spec — 子系统契约

GitPulse 的 Spec 按**子系统**组织（非 DesignPatterns 的「模式」）：

| Spec | 说明 |
|------|------|
| [Architecture](spec/Architecture.md) | 解决方案分层、项目依赖、模块边界 |
| [RestApi](spec/RestApi.md) | `IGitHubReposApi`、分页、`ApiResponse<T>`、CRUD 契约 |
| [Events](spec/Events.md) | MAUI 事件 → R3 管道、手动桥接约定 |

命名：`docs/spec/<SubsystemName>.md`。模板：[`spec/_template.md`](spec/_template.md)。

---

## 5. Design Doc — 实现细节

与 Spec 同名，位于 `docs/design/`。描述 **how** 与 **why**，随实现 PR 同步更新。

---

## 6. Plan — 大型任务

| 规模 | 载体 |
|------|------|
| 小型（单 PR） | GitHub Issue |
| 大型（跨多 PR / 整里程碑） | `docs/plans/<PascalCaseName>.md` + 主 Issue |

模板：[`plans/_template.md`](plans/_template.md)。完成移入 `docs/plans/archive/`。

---

## 7. Review — 评审记录

RFC 设计评审、里程碑实现回顾、发版前审查使用。模板：[`review/_template.md`](review/_template.md)。

---

## 8. 目录结构

```
docs/
├── DOCUMENTATION.md      # 本文件
├── README.md             # 索引
├── DEVELOPMENT.md        # 开发手册
├── ROADMAP.md            # 里程碑路线图
├── rfc/                  # 设计提案
├── adr/                  # 架构决策
├── spec/                 # 稳定契约
├── design/               # 实现设计
├── plans/                # 任务计划
└── review/               # 评审记录
```

---

## 9. 文档驱动开发流程

### 9.1 变更类型 → 文档前置条件

| 变更类型 | RFC | ADR | Plan | 实现 PR 须同步 |
|----------|-----|-----|------|----------------|
| 新里程碑（新 GitHub API 域） | ✅ Accepted | ✅ | ✅ `docs/plans/` | Spec + Design + CHANGELOG |
| `IGitHubReposApi` 破坏性变更 | ✅ | ✅ | 视规模 | Spec + CHANGELOG `Breaking` |
| 非破坏性 API 新增（单模块） | ❌ | ❌ | ❌ | Design Doc + CHANGELOG |
| Bug fix | ❌ | ❌ | ❌ | CHANGELOG（用户可见时） |
| 重构（无行为变更） | ❌ | 视影响 | ❌ | Design Doc（结构变化时） |
| v0.1.0 发版 | ❌ | ❌ | ❌ | Review + CHANGELOG 版本化 |

### 9.2 新里程碑完整流程

```
1. ROADMAP 排期
2. RFC（Draft → Review → Accepted）→ ADR
3. Plan（docs/plans/）+ 主 Issue
4. 按 Plan 分模块 PR 实现 → 同步 Spec / Design Doc
5. RFC Implemented → archive/；Plan Done → archive/
6. CHANGELOG + ROADMAP 归档
```

### 9.3 Agent 工作流约定

| 场景 | Agent 行为 |
|------|-----------|
| 新里程碑 / 新 API 域 | 确认 RFC + Plan；无则先建文档（经用户确认） |
| 修改 `IGitHubReposApi` 公共面 | 确认 RFC；更新 Spec |
| 跨多 PR 任务 | 确认 `docs/plans/` 有 Plan |
| 创建 RFC / ADR / Plan / Review | 使用对应 `_template.md` |
| 文档归档 | 移入 `archive/` + 更新 README 索引，同一 PR |
| CHANGELOG | `[Unreleased]` 添加条目 |
| 文档位置 | 不在 `docs/` 外新建维护者文档（`AGENTS.md` 等根目录文件除外） |

---

## 10. PR 与 Git

| 文档类型 | 提交方式 |
|----------|----------|
| RFC / ADR | 独立 PR 或随实现 PR |
| Spec / Design Doc | 与代码同一 PR |
| Plan / Review | 独立 PR 或随里程碑 PR |
| ROADMAP / CHANGELOG | 随实现 PR |

PR 模板含文档 checklist：[`.github/pull_request_template.md`](../.github/pull_request_template.md)。

---

## 11. 文档质量检查清单

- [ ] 文件位置与命名符合规范
- [ ] Frontmatter 完整（`YYYY-MM-DD`）
- [ ] 交叉链接有效（RFC ↔ ADR ↔ Spec ↔ Design ↔ Plan）
- [ ] 对应目录 `README.md` 索引已更新
- [ ] 无 AI/LLM 工具名称；无私有工作区路径

---

## 12. 参考

- [DesignPatterns DOCUMENTATION.md](https://github.com/Skymly/DesignPatterns/blob/main/docs/DOCUMENTATION.md) — 本体系来源
- [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
- [ADR (Michael Nygard)](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions)
