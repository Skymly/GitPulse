# ADR-011: M11 Android 日用可用（手机优先）范围

| 字段 | 值 |
|------|-----|
| **状态** | Accepted |
| **日期** | 2026-07-24 |
| **关联 Issue** | [#30](https://github.com/Skymly/GitPulse/issues/30)、[#31](https://github.com/Skymly/GitPulse/issues/31)、[#32](https://github.com/Skymly/GitPulse/issues/32)、[#33](https://github.com/Skymly/GitPulse/issues/33) |

## 背景

ADR-005 将 Android 定为次要平台，全面适配列为 M11。M10 完成后 Android 仍可编译并具备凭据存储，但桌面式布局与输入体验不足以支撑作者手机日用；同时「日用」容易被误解为需要 Android 系统通知或发布级 parity。需在开工前钉死 DoD、非目标与交付顺序。

## 决策

- **DoD**：竖屏手机上**日用可用**——非「仅能跑通」，亦非发布级 parity / 全页精修。
- **验收形态**：竖屏手机为唯一验收面；平板与横屏「不崩溃」即可。
- **工作流**：浏览 + 评论 / 改状态 / 简单 CRUD（含新建 Issue）为一等公民；Diff、文件编辑、merge「挤可用」即可，不进必过精修。
- **布局**：同一套 XAML **就地修补**；不默认拆 Phone 双视图。
- **出应用提醒**：M11 与 v0.1.0 **不做** Android 系统通知 / 出应用提醒；`IToastNotifier` / `IAppPresence` 继续空操作（ADR-010）。回 App 后使用应用内 Notifications。该项列入 ROADMAP「暂缓」，推迟到 v0.1.0 之后。
- **门禁**：CI / Nuke 增加 `net10.0-android` **编译**门禁；功能靠本机冒烟。签名、AAB、分发管道属 **M12**。
- **IME**：Issue / PR 详情与新建 Issue 上，软键盘不得阻断完成发送 / 提交（冒烟失败项）。
- **交付顺序**：先 Docs（本 ADR + ROADMAP / Architecture）与 CI 编译门禁，再 App 小 PR。

## 后果

- **正面**：M11 边界可验收；避免把次要平台做成第二套 Shell 或通知子系统。
- **正面**：与 ADR-005 / ADR-010 一致——Toast / Tray Presence 保持 Windows 语义。
- **负面**：Android 后台静默；作者在手机上须主动打开 App 才看到新 GitHub Notification。
- **负面**：就地修补可能在个别页达到可维护性上限，届时再个案升级为双视图，而非默认分叉。

## 参考

- [ADR-005](ADR-005-windows-first-platform-strategy.md)
- [ADR-010](ADR-010-windows-tray-presence-and-toast.md)
- [ROADMAP.md](../ROADMAP.md)
- [Architecture.md](../design/Architecture.md)
- [CONTEXT.md](../CONTEXT.md)
