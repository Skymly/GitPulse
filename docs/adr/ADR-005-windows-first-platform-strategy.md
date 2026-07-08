# ADR-005: Windows 优先平台策略

| 字段 | 值 |
|------|-----|
| **状态** | Accepted |
| **日期** | 2026-07-06 |
| **关联 RFC** | 无 |

## 背景

作者主要在 Windows 上日常使用 GitPulse。在功能深度与 Android 平台 parity 之间需取舍。

## 决策

- **主平台**：Windows（`net10.0-windows10.0.19041.0`）。
- **次要平台**：Android；iOS / MacCatalyst 暂缓。
- Windows 原生体验（Mica/Acrylic、系统托盘、Toast）与功能里程碑交错交付，不批量堆在末尾。
- Android 全面适配列为 **M11**，在 Windows 功能路线（M7–M10）之后。

## 后果

- **正面**：日用小工具价值优先；Windows 特性可渐进展示。
- **负面**：Android 长期落后；双平台 PR 需额外注意。

## 参考

- [ROADMAP.md](../ROADMAP.md)
