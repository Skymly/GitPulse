# ADR-015: SearchBar 公开事件适配器

| 字段 | 值 |
|------|-----|
| **状态** | Accepted |
| **日期** | 2026-09-14 |
| **关联 Issue** | [#511](https://github.com/Skymly/GitPulse/issues/511) |
| **取代** | [ADR-007](ADR-007-manual-searchbar-event-bridge.md) |

## 背景

ADR-007 记录用手动 R3 `Subject<T>` 桥接 `SearchBar.TextChanged`，以绕开 Observables.Events.R3 对 MAUI 内部接口 `IControlsVisualElement` 的 CS0122。代码已改为带公开 `event` 的适配器，再走源生成的 `.Events()`；`src/` 下 `Subject<` 为零。ADR-007 仍指向已删除的 `docs/spec/Events.md`。

## 决策

- MAUI `SearchBar.Events()` 仍会 CS0122 时，使用公开事件适配器（`SearchTextSource` / `LoadMoreSource`），由 Observables.Events.R3 生成 `.Events()`。
- 页面把 `SearchBar.TextChanged`（或 CollectionView remaining-items）转发到适配器；管道本身是源生成的：`Debounce` → `DistinctUntilChanged` → `ObserveOn`。
- 不要在页面里手写 `Subject<T>`。
- 权威说明在 [design/Events.md](../design/Events.md)，不再引用 `docs/spec/`。

## 后果

- **正面**：管道仍是 Observables 展示；适配器类型公开，可被源生成。
- **负面**：MAUI 控件本身仍不能直接 `.Events()`；上游修复前适配器要留下来。

## 参考

- [ADR-007](ADR-007-manual-searchbar-event-bridge.md)（Superseded）
- [design/Events.md](../design/Events.md)
