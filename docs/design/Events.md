# Design Doc: Events

> **版本**：0.33.0（公开 git tag 仍为 v0.32.0）
> **关联 ADR**：[ADR-007](../adr/ADR-007-manual-searchbar-event-bridge.md)、[ADR-015](../adr/ADR-015-searchbar-public-event-adapter.md)、[ADR-010](../adr/ADR-010-windows-tray-presence-and-toast.md)、[ADR-011](../adr/ADR-011-android-m11-daily-usable-phone.md)

## 概述

两类「事件」展示：(1) MAUI 控件 → R3；(2) 定时器 → REST 轮询（含 Tray Presence 下的 Toast 协调）。

## 范围

MAUI UI 事件与 R3 响应式管道的集成约定；通知轮询的进程级生命周期（ADR-010）。

## 管道约定

| 场景 | 管道 | 位置 |
|------|------|------|
| 仓库过滤防抖 | SearchBar → ADR-015 adapter → `.Events().TextChanged` → Debounce(300ms) → DistinctUntilChanged → VM | `ReposPage` / `UiEventPipelines` |
| GitHub Search 输入 | SearchBar → ADR-015 adapter → `.Events().TextChanged` → Debounce(300ms) → DistinctUntilChanged → 查询状态 | `SearchPage` / `UiEventPipelines` |
| Repos 加载更多 | CollectionView remaining-items → adapter → `.Events().Requested` → `LoadMoreCommand` | `ReposPage` / `UiEventPipelines` |
| Search / inbox 加载更多 | CollectionView remaining-items → adapter → `.Events().Requested` → `LoadMoreCommand` | `SearchPage` (typed Search + Review / Assigned / Mentions) / `UiEventPipelines` |
| 仓库内列表加载更多 | CollectionView remaining-items → adapter → `.Events().Requested` → `LoadMoreCommand` | `IssuesPage` / `PullRequestsPage` / `CommitsPage` / `WorkflowRunsPage` / `UiEventPipelines` |
| 通知轮询 | `Observable.Interval` → REST（`ApiResponse` + `Link`，≤10 页）→ event | `NotificationPoller` |
| 轮询 → UI | poller event → R3 绑定 | `NotificationsViewModel` |
| 轮询 → Toast | poller event → id 差集 → `IToastNotifier`（仅主窗隐藏） | `NotificationToastHost` / `NotificationToastCoordinator` |
| 列表过期 | CommunityToolkit `WeakReferenceMessenger` (`RepoListStaleMessage`) | Create issue/PR、File editor save/delete → Issues / PRs / File Browser `OnAppearing` 再加载 |

## 状态绑定

- ViewModel：`BindableReactiveProperty<T>`，XAML 绑定 `{Binding Prop.Value}`
- 命令：`[RelayCommand]` 生成 `*Command` / `*Command` async

## 不变量

1. 事件订阅在 Page `OnDisappearing` 或 ViewModel `Dispose` 中释放（ViewModel 在 Shell Tab 复用期间不因 disappear 而 Dispose）。
2. UI 线程更新经 `ObserveOn` 或 MAUI 调度器。
3. 若 Observables `.Events()` 因 MAUI internal API 不可用，须用公开 event 的 adapter（ADR-015）并文档化；管道走 `.Events()`，不要在页面里手写 Subject。
4. `INotificationPoller` 由 App 层 `NotificationToastHost` 在进程启动时 `Start`，仅在 Exit（host `Dispose`）时 `Stop`；`NotificationsPage` 不再在 disappear 时停轮询（ADR-010）。`Stop()` 始终发布空快照（`UnreadCount = 0`），清 PAT 后角标归零。
5. `INotificationPoller.LastError` / `LastErrorChanged` 是轮询失败的契约面。`NotificationsViewModel` 映射为 Page Error；401 / 缺 PAT 给 Settings 动作。
6. 空通知快照不进入 Toast 已知 id 集：`NotificationToastCoordinator` 对其 `ResetBaseline`。下一份非空列表是安静的首次基线。

## 实现概览

### ReposPage / SearchPage 搜索（Observables.Events + ADR-015 adapter）

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

### 列表过期（Create / File write）

Shell `GoToAsync("..")` 不会给下层页面带 query。Create issue/PR 成功 pop 后，以及 File editor save/delete pop 到 File Browser 时，App 发 `RepoListStaleMessage`（owner/repo + list kind）。Issues / PRs / File Browser 在 `OnAppearing` 匹配后重新加载，而不是依赖 `refresh=`。

### 通知轮询与 Tray Toast（M4 + M10）

- `ListNotifications` 返回 `Observable<ApiResponse<Notification[]>>`，以便读取 `Link`。
- `NotificationPoller`：`Observable.Interval`（`Prepend` 立即首轮）+ paged session 跟随 `rel="next"`，上限 10 页；后页失败不发布部分快照。每页 30s 超时。
- `RefreshAsync` 与 `Stop` / `Dispose` 共用 run cancellation token；busy 时排队到本轮结束再拉一次。取消不当成 timeout `LastError`。完成后若已 dispose / token 已取消则不发布。
- `Stop()` 取消 in-flight 并发布 `NotificationsUpdated([], 0)`。
- HTTP 失败设 `LastError` 并按 `Retry-After` 或指数退避跳过后续 tick；成功或未认证 stop 清错误。401 走 backoff，不 `Stop`。
- `NotificationsViewModel` 订阅 poller 输出与 `LastErrorChanged`（页面 appear 时幂等 `Start`）。标记已读后调用 `RefreshAsync`，角标以 poller 快照为准。
- `NotificationToastHost`（App）：进程级订阅 poller → `NotificationToastCoordinator`（快照更新有锁）；进入 Tray Presence 时 `ResetBaseline`。空快照也 `ResetBaseline`。
- Windows：`AppWindow.Closing` 取消关闭并隐藏到托盘；汇总 Toast 经 `AppNotificationManager`；Android 为空操作（M11 / ADR-011：v0.1.0 前不做 Android 出应用通知）

## 设计权衡

- 选公开 event adapter 而非 fork Observables：阻塞项为 MAUI internal API，非管道设计问题。
- 托盘态继续轮询：用更高 Notifications API 用量换取关窗后仍能 Toast（ADR-010）。

## 已知局限

- MAUI 控件 `.Events()` 仍可能 CS0122；公开 event 的 adapter 已验证（SearchTextSource / LoadMoreSource）。遇 CS0122 复用 ADR-015 adapter，而不是页面里手写 Subject 管道。
- 轮询非 WebSocket；展示「伪实时」足够，非生产级推送。
- 本切片无托盘未读角标、无 Actions 状态 Toast。

## 不在范围内

- 源生成 MAUI 全控件 Events 覆盖（依赖上游 Observables）

## 兼容基线

- R3 1.3.0+、`R3Extensions.Maui`
- Observables.Events.R3 0.1.5（SearchBar 生成器已知问题）

## 参考

- `src/GitPulse.App/Events/UiEventPipelines.cs`
- `src/GitPulse.App/Events/RepoListStaleMessage.cs`
- `src/GitPulse.App/Views/ReposPage.xaml.cs`
- `src/GitPulse.App/Views/SearchPage.xaml.cs`
- `src/GitPulse.App/Services/NotificationToastHost.cs`
- `src/GitPulse.Services/NotificationPoller.cs`
- `src/GitPulse.Core/Abstractions/INotificationPoller.cs`
- `src/GitPulse.Core/Notifications/NotificationToastCoordinator.cs`



