# ADR-017: BindableReactiveProperty 属于 R3 核心包

| 字段 | 值 |
|------|-----|
| **状态** | Accepted |
| **日期** | 2026-09-14 |
| **关联 Issue** | [#511](https://github.com/Skymly/GitPulse/issues/511) |
| **取代** | [ADR-003](ADR-003-r3-bindable-reactive-property.md) 中「类型来自 `R3Extensions.Maui`」的表述 |

## 背景

ADR-003 把 ViewModel 状态定为 `BindableReactiveProperty<T>`，并写它属于 `R3Extensions.Maui`。该类型在 **R3 核心包**；`GitPulse.ViewModels` 只引用 `R3` 即可编译。`R3Extensions.Maui` 仍由 App 用于 `UseR3()` 等主机集成，不进入 ViewModels 项目。

ADR-003 其余决策（`[RelayCommand]`、`ObservableCollection<T>`、ViewModel `IDisposable`）仍然有效。

## 决策

- ViewModel 状态使用 R3 **核心包**的 `BindableReactiveProperty<T>`。
- ViewModels 项目不引用 `R3Extensions.Maui`。
- 命令仍用 CommunityToolkit `[RelayCommand]`；集合仍用 `ObservableCollection<T>`；ViewModel 仍 `IDisposable`。

## 后果

- **正面**：ViewModels 保持 `net10.0`、无 MAUI 引用。
- **负面**：XAML 仍绑定 `PropertyName.Value`；须手动 Dispose。

## 参考

- [ADR-003](ADR-003-r3-bindable-reactive-property.md)（包归属被取代）
- [design/Events.md](../design/Events.md)
