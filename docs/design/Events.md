# Design Doc: Events

> **关联 Spec**：[spec/Events.md](../spec/Events.md)
> **关联 ADR**：[ADR-007](../adr/ADR-007-manual-searchbar-event-bridge.md)

## 概述

两类「事件」展示：(1) MAUI 控件 → R3；(2) 定时器 → REST 轮询。

## 实现概览

### ReposPage 搜索（手动桥接）

```csharp
// ReposPage.xaml.cs — 意图与源生成 .Events() 相同
_searchSubject
    .Debounce(TimeSpan.FromMilliseconds(300), TimeProvider.System)
    .DistinctUntilChanged()
    .ObserveOn(_scheduler)
    .Subscribe(text => _viewModel.SearchText.Value = text);
```

`SearchBar.TextChanged` 写入 `_searchSubject`。

### 通知轮询（M4）

- `NotificationPoller`：`Observable.Interval` + `IGitHubReposApi.ListNotifications`
- `NotificationsViewModel` 订阅 poller 输出

## 设计权衡

- 选手动 Subject 而非 fork Observables：阻塞项为 MAUI internal API，非管道设计问题。

## 已知局限

- 其他控件 `.Events()` 未全面验证；遇 CS0122 复用 ADR-007 模式。
- 轮询非 WebSocket；展示「伪实时」足够，非生产级推送。

## 参考

- `src/GitPulse.App/Views/ReposPage.xaml.cs`
- `src/GitPulse.Services/NotificationPoller.cs`
