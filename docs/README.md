# 内部文档索引

本目录面向**维护者与贡献者**（中文为主）。用户向简介见根目录 [README.md](../README.md)。

> **文档约定**：[DOCUMENTATION.md](DOCUMENTATION.md) — ADR、Design Doc 与同步规则。

## 入门

| 文档 | 说明 |
|------|------|
| [DOCUMENTATION.md](DOCUMENTATION.md) | **文档约定**（ADR、Design Doc 与同步规则） |
| [CONTEXT.md](CONTEXT.md) | 领域术语（Tray Presence、Toast、New Notification 等） |
| [DEVELOPMENT.md](DEVELOPMENT.md) | 环境、构建、测试、仓库布局 |
| [ROADMAP.md](ROADMAP.md) | 里程碑 M0–M12 |
| [../CONTRIBUTING.md](../CONTRIBUTING.md) | 贡献流程 |
| [../AGENTS.md](../AGENTS.md) | AI Agent 上下文 |
| [../CHANGELOG.md](../CHANGELOG.md) | 变更日志 |

## 架构决策

| 目录 | 说明 |
|------|------|
| [adr/](adr/README.md) | ADR — 架构决策（[索引](adr/README.md)） |

## 子系统设计

| 目录 | 说明 |
|------|------|
| [design/](design/README.md) | Design Doc — API、契约、实现细节与权衡 |

| 子系统 | Design Doc |
|--------|------------|
| 解决方案架构 | [design/Architecture.md](design/Architecture.md) |
| Observables RestAPI | [design/RestApi.md](design/RestApi.md) |
| Observables Events / R3 UI | [design/Events.md](design/Events.md) |

## 受众分工

| 受众 | 位置 | 语言 |
|------|------|------|
| 终端用户 / 克隆者 | `README.md` | 英语 |
| 维护者 / 深度设计 | `docs/` | 中文 |
| AI Agent | `AGENTS.md` + `docs/` | 中文 |
