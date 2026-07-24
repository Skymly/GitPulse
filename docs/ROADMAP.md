# GitPulse 路线图

功能与技术 backlog 的滚动清单。里程碑编号 **M0–M12**；完成项移入「已完成（归档）」章节。

- **文档标准**：[DOCUMENTATION.md](DOCUMENTATION.md)
- **Agent 上下文**：[../AGENTS.md](../AGENTS.md)
- **变更日志**：[../CHANGELOG.md](../CHANGELOG.md)

## 策略说明（2026-07-06 修订）

Android 适配延后，**Windows 优先**深化 GitHub API 覆盖。Windows 原生增强（Mica/Acrylic、系统托盘、Toast）穿插在各里程碑中，而非堆在末尾。全部功能完成后发布 **v0.1.0**，不设中间预发布 tag。

---

## 进行中

| 里程碑 | 内容 | Observables 域 | 状态 |
|--------|------|----------------|------|
| **M11** | Android 日用可用（竖屏手机；ADR-011） | platform abstraction | 🔄 进行中（[#30](https://github.com/Skymly/GitPulse/issues/30)） |

**M11 DoD（摘要）**：同一套 XAML 就地修补；一等公民 = 浏览 + 评论/改状态/简单 CRUD（含新建 Issue）+ 软键盘不阻断发送；CI 仅 Android **编译**门禁；签名/AAB → M12。

**M11 非目标**：Android 系统通知 / 出应用提醒；Phone 双视图（默认）；平板/横屏精修；模拟器 UI 自动化门禁。

---

## 排期

| 里程碑 | 内容 | Observables 域 |
|--------|------|----------------|
| **M12** | Release v0.1.0（完整发布流水线） | full pipeline |

---

## 候选

（暂无）

---

## 暂缓 / 明确不做

| 项 | 理由 |
|----|------|
| GitHub App OAuth | 当前仅 PAT；OAuth 推迟至 v0.1.0 之后 |
| iOS / MacCatalyst | MAUI 目标平台未稳定，暂不投入 |
| Android 系统通知 / 出应用提醒 | ADR-011：M11 与 v0.1.0 保持应用内 Notifications；Toast 仍为 Windows 语义（ADR-010） |

---

## 已完成（归档）

| 里程碑 | 内容 | Observables 域 |
|--------|------|----------------|
| **M0** ✅ | 项目骨架：solution、Nuke、CI、文档、空 MAUI 应用可编译 | — |
| **M1** ✅ | 认证 + 仓库列表浏览 | RestAPI + Events |
| **M2** ✅ | Issue/PR 列表与详情（分页、Markdown） | RestAPI + Events |
| **M3** ✅ | Issue/PR CRUD（评论、状态、标签、新建 Issue） | RestAPI |
| **M4** ✅ | 通知中心（`Observable.Interval` 轮询模拟实时） | Events |
| **M5** ✅ | 文件浏览与编辑（Contents API） | RestAPI |
| **M6** ✅ | PR review 与 merge（merge/squash/rebase） | RestAPI |
| **M7** ✅ | 仓库详情页（README Markdown、元数据、分支、Release）+ Windows Mica/Acrylic | RestAPI + platform |
| **M8** ✅ | PR Diff 查看器（Files Changed、行内 review comment） | RestAPI |
| **M9** ✅ | Search（仓库 / Issue / PR / 代码）+ 可选实网 Integration 测试 | RestAPI |
| **M10** ✅ | GitHub Actions（workflow runs、状态、重跑、日志）+ Windows 系统托盘与 Toast（ADR-010） | RestAPI + Events + platform |
