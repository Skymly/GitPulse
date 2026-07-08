# Spec: Events

> **版本**：Unreleased
> **关联 Design Doc**：[design/Events.md](../design/Events.md)
> **关联 ADR**：[ADR-003](../adr/ADR-003-r3-bindable-reactive-property.md)、[ADR-007](../adr/ADR-007-manual-searchbar-event-bridge.md)

## 范围

MAUI UI 事件与 R3 响应式管道的集成契约。

## 管道约定

| 场景 | 管道 | 位置 |
|------|------|------|
| 搜索防抖 | TextChanged → Debounce(300ms) → DistinctUntilChanged → VM | `ReposPage` |
| 通知轮询 | `Observable.Interval` → REST → VM | `NotificationPoller` / `NotificationsViewModel` |

## 状态绑定

- ViewModel：`BindableReactiveProperty<T>`，XAML 绑定 `{Binding Prop.Value}`
- 命令：`[RelayCommand]` 生成 `*Command` / `*Command` async

## 不变量

1. 事件订阅在 Page `OnDisappearing` 或 ViewModel `Dispose` 中释放。
2. UI 线程更新经 `ObserveOn` 或 MAUI 调度器。
3. 若 Observables `.Events()` 因 MAUI internal API 不可用，须用手动 `Subject` 桥接并文档化（ADR-007）。

## 不在范围内

- 源生成 MAUI 全控件 Events 覆盖（依赖上游 Observables）

## 兼容基线

- R3 1.3.0+、`R3Extensions.Maui`
- Observables.Events.R3 0.1.5（SearchBar 生成器已知问题）
