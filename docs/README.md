# 内部文档索引

本目录面向**维护者与贡献者**（中文为主）。用户向简介见根目录 [README.md](../README.md)。

> **文档体系标准**：[DOCUMENTATION.md](DOCUMENTATION.md) — 类型、生命周期、模板、工作流。源自 [DesignPatterns](https://github.com/Skymly/DesignPatterns) 文档驱动开发体系。

## 入门

| 文档 | 说明 |
|------|------|
| [DOCUMENTATION.md](DOCUMENTATION.md) | **文档体系标准**（先文档后代码） |
| [DEVELOPMENT.md](DEVELOPMENT.md) | 环境、构建、测试、仓库布局 |
| [ROADMAP.md](ROADMAP.md) | 里程碑 M0–M12 |
| [../CONTRIBUTING.md](../CONTRIBUTING.md) | 贡献流程 |
| [../AGENTS.md](../AGENTS.md) | AI Agent 上下文 |
| [../CHANGELOG.md](../CHANGELOG.md) | 变更日志 |

## 设计提案与决策

| 目录 | 说明 |
|------|------|
| [rfc/](rfc/README.md) | RFC — 设计提案（[状态板](rfc/README.md)） |
| [adr/](adr/README.md) | ADR — 架构决策（[索引](adr/README.md)） |

## 计划与评审

| 目录 | 说明 |
|------|------|
| [plans/](plans/README.md) | Plan — 大型任务（[状态板](plans/README.md)） |
| [review/](review/README.md) | Review — 评审记录（[索引](review/README.md)） |

## 规范与设计

| 目录 | 说明 |
|------|------|
| [spec/](spec/README.md) | Spec — 稳定契约（API、模型、不变量） |
| [design/](design/README.md) | Design Doc — 实现细节与权衡 |

### 子系统索引

| 子系统 | Spec | Design Doc |
|--------|------|------------|
| 解决方案架构 | [spec/Architecture.md](spec/Architecture.md) | [design/Architecture.md](design/Architecture.md) |
| Observables RestAPI | [spec/RestApi.md](spec/RestApi.md) | [design/RestApi.md](design/RestApi.md) |
| Observables Events / R3 UI | [spec/Events.md](spec/Events.md) | [design/Events.md](design/Events.md) |

## 受众分工

| 受众 | 位置 | 语言 |
|------|------|------|
| 终端用户 / 克隆者 | `README.md` | 英语 |
| 维护者 / 深度设计 | `docs/` | 中文 |
| AI Agent | `AGENTS.md` + `docs/` | 中文 |
