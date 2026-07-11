# ADR-007: SearchBar 手动 R3 事件桥接

| 字段 | 值 |
|------|-----|
| **状态** | Accepted |
| **日期** | 2026-04-01 |
| **关联 Issue** | 无 |

## 背景

`Observables.Events.R3` 为 `SearchBar` 生成的 `.Events()` 扩展引用 MAUI 内部接口 `IControlsVisualElement`，导致 CS0122。GitPulse 仍需展示「事件 → Observable → Debounce」管道。

## 决策

- **ReposPage** 使用手动 R3 `Subject<T>` 桥接 `SearchBar.TextChanged`。
- 管道保持与源生成形式一致：`Debounce` → `DistinctUntilChanged` → `ObserveOn` → 更新 ViewModel。
- 上游修复前，其他 MAUI 控件若遇同类问题，采用相同手动桥接模式并记入 Design Doc。

## 后果

- **正面**：可编译、可运行；管道语义与 Observables 设计一致。
- **负面**：非源生成，展示完整性打折扣；需在文档中明确标注。

## 参考

- [spec/Events.md](../spec/Events.md)
- [design/Events.md](../design/Events.md)
