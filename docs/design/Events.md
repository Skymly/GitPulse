# Design Doc: Events

> **版本**：0.33.0（公开 git tag 仍为 v0.32.0）
> **关联 ADR**：[ADR-007](../adr/ADR-007-manual-searchbar-event-bridge.md)、[ADR-010](../adr/ADR-010-windows-tray-presence-and-toast.md)、[ADR-011](../adr/ADR-011-android-m11-daily-usable-phone.md)

## 概述

两类「事件」展示：(1) MAUI 控件 → R3；(2) 定时器 → REST 轮询（含 Tray Presence 下的 Toast 协调）。

## 范围

MAUI UI 事件与 R3 响应式管道的集成约定；通知轮询的进程级生命周期（ADR-010）。

## 管道约定

| 场景 | 管道 | 位置 |
|------|------|------|
| 仓库过滤防抖 | SearchBar → ADR-007 adapter → `.Events().TextChanged` → Debounce(300ms) → DistinctUntilChanged → VM | `ReposPage` / `UiEventPipelines` |
| GitHub Search 输入 | SearchBar → ADR-007 adapter → `.Events().TextChanged` → Debounce(300ms) → DistinctUntilChanged → 查询状态 | `SearchPage` / `UiEventPipelines` |
| Repos 加载更多 | CollectionView remaining-items → adapter → `.Events().Requested` → `LoadMoreCommand` | `ReposPage` / `UiEventPipelines` |
| 通知轮询 | `Observable.Interval` → REST → event | `NotificationPoller` |
| 轮询 → UI | poller event → R3 绑定 | `NotificationsViewModel` |
| 轮询 → Toast | poller event → id 差集 → `IToastNotifier`（仅主窗隐藏） | `NotificationToastHost` / `NotificationToastCoordinator` |

## 状态绑定

- ViewModel：`BindableReactiveProperty<T>`，XAML 绑定 `{Binding Prop.Value}`
- 命令：`[RelayCommand]` 生成 `*Command` / `*Command` async

## 不变量

1. 事件订阅在 Page `OnDisappearing` 或 ViewModel `Dispose` 中释放（ViewModel 在 Shell Tab 复用期间不因 disappear 而 Dispose）。
2. UI 线程更新经 `ObserveOn` 或 MAUI 调度器。
3. 若 Observables `.Events()` 因 MAUI internal API 不可用，须用公开 event 的 adapter（ADR-007）并文档化；管道走 `.Events()`，不要在页面里手写 Subject。
4. `INotificationPoller` 由 App 层 `NotificationToastHost` 在进程启动时 `Start`，仅在 Exit（host `Dispose`）时 `Stop`；`NotificationsPage` 不再在 disappear 时停轮询（ADR-010）。

## 实现概览

### ReposPage / SearchPage 搜索（Observables.Events + ADR-007 adapter）

MAUI `SearchBar.Events()` 仍会 CS0122。`SearchTextSource` 是带公开 `event Action<string>? TextChanged` 的适配器，由 Observables.Events.R3 生成 `.Events()`。页面把 `SearchBar.TextChanged` 转发到 adapter，管道本身是源生成的：

```csharp
source.Events().TextChanged
    .Debounce(TimeSpan.FromMilliseconds(300), TimeProvider.System)
    .DistinctUntilChanged()
    .ObserveOnCurrentSynchronizationContext()
    .Subscribe(text => target.Value = text);
```

Repos `CollectionView` remaining-items 用同样模式：`LoadMoreSource.Events().Requested` → `LoadMoreCommand`。共享代码在 `GitPulse.App/Events/UiEventPipelines.cs`。

### SearchPage 输入与显式提交（M9）

`SearchPage` 复用 `UiEventPipelines.BindSearchText`。防抖管道只更新
`SearchViewModel.Query`，不会调用 Search API。用户按 Enter 或点击 Search 时，
页面先同步当前 `SearchBar.Text`，再执行 `SearchCommand`；短于 3 个字符的查询
在 ViewModel 中拒绝。这样既保留响应式输入状态，又避免按键事件消耗 GitHub
Search 的独立限额（普通搜索 30 次/分钟，代码搜索 10 次/分钟）。

页面消失时释放 Events 管道，返回 Search Tab 时重新建立；ViewModel
结果与所选类型继续保留。

### 通知轮询与 Tray Toast（M4 + M10）

- `NotificationPoller`：`Observable.Interval` + `IGitHubReposApi.ListNotifications`
- `NotificationsViewModel` 订阅 poller 输出（页面 appear 时幂等 `Start`）
- `NotificationToastHost`（App）：进程级订阅 poller → `NotificationToastCoordinator`；进入 Tray Presence 时 `ResetBaseline`
- Windows：`AppWindow.Closing` 取消关闭并隐藏到托盘；汇总 Toast 经 `AppNotificationManager`；Android 为空操作（M11 / ADR-011：v0.1.0 前不做 Android 出应用通知）

## 设计权衡

- 选公开 event adapter 而非 fork Observables：阻塞项为 MAUI internal API，非管道设计问题。
- 托盘态继续轮询：用更高 Notifications API 用量换取关窗后仍能 Toast（ADR-010）。

## 已知局限

- MAUI 控件 `.Events()` 仍可能 CS0122；公开 event 的 adapter 已验证（SearchTextSource / LoadMoreSource）。遇 CS0122 复用 ADR-007 adapter，而不是页面里手写 Subject 管道。
- 轮询非 WebSocket；展示「伪实时」足够，非生产级推送。
- 本切片无托盘未读角标、无 Actions 状态 Toast。

## 不在范围内

- 源生成 MAUI 全控件 Events 覆盖（依赖上游 Observables）

## 兼容基线

- R3 1.3.0+、`R3Extensions.Maui`
- Observables.Events.R3 0.1.5（SearchBar 生成器已知问题）

## 参考

- `src/GitPulse.App/Events/UiEventPipelines.cs`
- `src/GitPulse.App/Views/ReposPage.xaml.cs`
- `src/GitPulse.App/Views/SearchPage.xaml.cs`
- `src/GitPulse.App/Services/NotificationToastHost.cs`
- `src/GitPulse.Services/NotificationPoller.cs`
- `src/GitPulse.Core/Notifications/NotificationToastCoordinator.cs`



