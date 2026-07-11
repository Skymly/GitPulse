# ADR-003: R3 BindableReactiveProperty 状态管理

| 字段 | 值 |
|------|-----|
| **状态** | Accepted |
| **日期** | 2026-03-01 |
| **关联 Issue** | 无 |

## 背景

需要 MAUI 可绑定的响应式状态，同时保持 ViewModel 可单测、与 Observables/R3 生态一致。

## 决策

- ViewModel 状态使用 R3 `BindableReactiveProperty<T>`（`R3Extensions.Maui`）。
- 命令使用 CommunityToolkit `[RelayCommand]`。
- 集合类 UI 状态使用 `ObservableCollection<T>`（非 reactive property）。
- ViewModel 实现 `IDisposable`，在 `Dispose()` 中释放 reactive properties。

## 后果

- **正面**：与 `UseR3()` MAUI 集成一致；属性变更可绑定 `.Value`。
- **负面**：XAML 绑定需写 `PropertyName.Value`；需手动 Dispose。

## 参考

- [design/Events.md](../design/Events.md)
