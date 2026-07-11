# 文档约定

本文档定义 GitPulse 的内部文档类型与维护规则。

## 文档载体

| 载体 | 位置 | 用途 |
|------|------|------|
| **ADR** | `docs/adr/` | 架构决策（不可变卡片） |
| **Design Doc** | `docs/design/` | 每个子系统的 API、模型、不变量、实现与权衡 |
| **Roadmap** | `docs/ROADMAP.md` | 宏观规划与 backlog 排序 |
| **Issue** | GitHub Issues | 需求、Bug、任务追踪 |
| **PR** | GitHub Pull Requests | 变更审查 |
| **Release** | GitHub Releases + `CHANGELOG.md` | 版本历史 |

## ADR

- 命名：`docs/adr/ADR-<NNN>-<kebab-case>.md`；编号不复用。
- Accepted 后正文不修改；推翻时创建新 ADR，并将旧 ADR 标为 `Superseded by ADR-XXX`。
- 模板：[`adr/_template.md`](adr/_template.md)。

破坏性 API 或模型变更、跨层架构变更，以及改变既有 ADR 决策时，创建 ADR。

## Design Doc

每个子系统一份 `docs/design/<Subsystem>.md`，记录 API、模型、不变量、实现概览、设计权衡与已知局限。公开 API、行为或实现权衡变更时，随同一 PR 更新对应文档。

模板：[`design/_template.md`](design/_template.md)；索引：[`design/README.md`](design/README.md)。

## Roadmap

`docs/ROADMAP.md` 维护宏观排序与探索候选；具体任务与验收使用 GitHub Issue 跟踪。

## 目录结构

```
docs/
├── DOCUMENTATION.md
├── README.md
├── DEVELOPMENT.md
├── ROADMAP.md
├── adr/
└── design/
```

## 质量检查

- [ ] ADR 编号未复用；被取代的 ADR 标记了替代者。
- [ ] API、模型或实现变更已更新对应 Design Doc。
- [ ] 用户可见变更已按需更新 `CHANGELOG.md`、README 或用户文档。
