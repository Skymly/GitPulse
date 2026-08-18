# GitPulse 路线图

功能与技术 backlog 的滚动清单。里程碑编号 **M0–M18**；完成项移入「已完成（归档）」章节。

- **文档标准**：[DOCUMENTATION.md](DOCUMENTATION.md)
- **Agent 上下文**：[../AGENTS.md](../AGENTS.md)
- **变更日志**：[../CHANGELOG.md](../CHANGELOG.md)

## 策略说明（2026-07-06 修订）

Android 适配延后，**Windows 优先**深化 GitHub API 覆盖。Windows 原生增强（Mica/Acrylic、系统托盘、Toast）穿插在各里程碑中，而非堆在末尾。全部功能完成后发布 **v0.1.0**，不设中间预发布 tag。

---

## 进行中

（暂无）

## 排期

（暂无）

---

## 候选

| 项 | 说明 |
|----|------|
| Microsoft Store / Google Play / Authenticode / AAB | ADR-012/013/014 明确延后 |
| GitHub App OAuth | 当前仅 PAT（ADR-004） |
| 真机抽查 / IME 自动化 | ADR-014：非 cut 必过 |

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
| **M18** ✅ | v0.6.0 repo commit history：Repo detail → 分页 Commits 列表并打开 GitHub（[#167](https://github.com/Skymly/GitPulse/issues/167)；[#168](https://github.com/Skymly/GitPulse/issues/168)–[#173](https://github.com/Skymly/GitPulse/issues/173)） | RestAPI |
| **M17** ✅ | v0.5.0 Starred repos：Repos 页 My repos / Starred 切换，分页 `GET /user/starred`（[#150](https://github.com/Skymly/GitPulse/issues/150)；[#151](https://github.com/Skymly/GitPulse/issues/151)–[#155](https://github.com/Skymly/GitPulse/issues/155)） | RestAPI |
| **M16** ✅ | v0.4.0 PR head Gate Rollup：PR detail Conversation 列出最新 Check Run + combined Commit Status，并显示客户端汇总（[#132](https://github.com/Skymly/GitPulse/issues/132)；[#133](https://github.com/Skymly/GitPulse/issues/133)–[#138](https://github.com/Skymly/GitPulse/issues/138)） | RestAPI |
| **M15** ✅ | v0.3.0 Submit Pull Request Review：PR detail Conversation 列出已提交 Review 并立即提交 COMMENT / APPROVE / REQUEST_CHANGES（[#114](https://github.com/Skymly/GitPulse/issues/114)；[#115](https://github.com/Skymly/GitPulse/issues/115)–[#120](https://github.com/Skymly/GitPulse/issues/120)） | RestAPI |
| **M14** ✅ | v0.2.0 create/manage PRs：同仓 Create PR（含可选 create-time Draft PR）+ PR detail 编辑 title/body（[#93](https://github.com/Skymly/GitPulse/issues/93)；[#94](https://github.com/Skymly/GitPulse/issues/94)–[#100](https://github.com/Skymly/GitPulse/issues/100)） | RestAPI |
| **M13** ✅ | Android Emulator UI Smoke + v0.1.1 双产物 GitHub Release（Win zip + 签名 APK；ADR-014；[#67](https://github.com/Skymly/GitPulse/issues/67) / [#71](https://github.com/Skymly/GitPulse/issues/71)） | full pipeline |
| **M12** ✅ | Release v0.1.0（GitHub Releases；Win publish zip；ADR-013；[#54](https://github.com/Skymly/GitPulse/issues/54) / [#58](https://github.com/Skymly/GitPulse/issues/58)） | full pipeline |
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
| **M11** ✅ | Android 日用可用（竖屏手机就地 XAML；软键盘不阻断发送；`CiAndroid`；ADR-011 / [#30](https://github.com/Skymly/GitPulse/issues/30)） | platform abstraction |
