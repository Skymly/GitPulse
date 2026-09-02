# Research: what Fluent can mean on MAUI 10

| Field | Value |
|-------|-------|
| **Ticket** | [What Fluent can mean on MAUI 10](https://github.com/Skymly/GitPulse/issues/447) (part of [Product-level UX spec map](https://github.com/Skymly/GitPulse/issues/446)) |
| **Date** | 2026-09-02 |
| **Purpose** | Facts for a later product-level UX spec: what theming, chrome, typography, iconography, Acrylic/Mica, and Shell .NET MAUI 10 actually gives GitPulse on Windows (`net10.0-windows10.0.19041.0`) and Android, relative to Fluent / WinUI. **Does not** pick colors, fonts, or the visual language. **Does not** change the app. |

## Method

Primary sources only:

- GitPulse `src/GitPulse.App` (`Platforms/Windows`, `Platforms/Android`, `Resources/Styles`, `AppShell.xaml`, `MauiProgram.cs`, `GitPulse.App.csproj`), ADR-005 / ADR-010 / ADR-011, `docs/DEVELOPMENT.md`
- Official .NET MAUI 10 docs (Learn, `view=net-maui-10.0`) and `Microsoft.Maui.Controls` API
- Official WinUI 3 / Windows App SDK docs (theming, materials, typography, iconography, title bar)
- First-party MAUI source tag [10.0.20](https://github.com/dotnet/maui/tree/10.0.20) and NuGet [Microsoft.Maui.Core 10.0.20](https://www.nuget.org/packages/Microsoft.Maui.Core/10.0.20)

No blog roundups. Claims below follow the owner of the API or the GitPulse file that uses it.

---

## 1. Three things "Fluent on MAUI 10" can mean

These are distinct surfaces. This note does not choose among them.

| Sense | Owner | What it actually is |
|-------|--------|---------------------|
| **WinUI 3 / Fluent on Windows** | Windows App SDK / WinUI | Native Windows UI. WinUI 3 "brings the Fluent Design System" and runs on Windows 10 1809 (build 17763) and later, including Windows 11 ([WinUI 3](https://learn.microsoft.com/en-us/windows/apps/winui/winui3/)). Theming is `RequestedTheme` plus `{ThemeResource}` brushes and type-ramp styles ([Theming in Windows apps](https://learn.microsoft.com/en-us/windows/apps/develop/ui/theming)). Typography default is Segoe UI Variable plus a published type ramp ([Typography in Windows](https://learn.microsoft.com/en-us/windows/apps/design/signature-experiences/typography)). Icons default to Segoe Fluent Icons / `SymbolThemeFontFamily` ([Icons in Windows apps](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/icons)). Window chrome uses `Window.SystemBackdrop` (`MicaBackdrop` / `DesktopAcrylicBackdrop`) or Composition controllers ([System backdrops](https://learn.microsoft.com/en-us/windows/apps/develop/ui/system-backdrops)). |
| **MAUI 10 abstraction** | .NET MAUI | Cross-platform XAML (`http://schemas.microsoft.com/dotnet/2021/maui`) mapped by handlers to native views ([Handlers](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/handlers/?view=net-maui-10.0)). On Windows, MAUI apps use WinUI 3 ([Supported platforms](https://learn.microsoft.com/en-us/dotnet/maui/supported-platforms?view=net-maui-10.0)). MAUI XAML is **not** WinUI XAML: no `{ThemeResource}`, no WinUI accent/type-ramp resources in a MAUI `ResourceDictionary`. Default font is Open Sans on every platform ([Fonts](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/fonts?view=net-maui-10.0)). There is **no** MAUI control or `Window` property for Mica/Acrylic. |
| **GitPulse today** | this repo | Template-like `Colors.xaml` / `Styles.xaml` (Primary `#512BD4`, Magenta selected tabs, OpenSans, `dotnet_bot.png` tab icons), Shell `TabBar` of four tabs, Windows `MicaController` with `DesktopAcrylicController` fallback, tray/toast Windows-only. |

MAUI 10 can host native WinUI from `Window.Handler.PlatformView` as `Microsoft.UI.Xaml.Window` (GitPulse already does this for backdrops and tray). That is platform code, not a MAUI Fluent theme.

---

## 2. Stack GitPulse actually compiles

From [`GitPulse.App.csproj`](../../src/GitPulse.App/GitPulse.App.csproj) and the local `maui-windows` workload:

| Item | Fact |
|------|------|
| TFMs | `net10.0-android` + `net10.0-windows10.0.19041.0` |
| Windows min | `SupportedOSPlatformVersion` / `TargetPlatformMinVersion` `10.0.17763.0` (1809) |
| Packaging | `WindowsPackageType=None` (unpackaged) |
| MAUI package | `Microsoft.Maui.Controls` Version=`$(MauiVersion)` (workload-driven, not pinned in-repo) |
| Workload on this machine | `maui-windows` **10.0.20**/10.0.100 |
| WASDK pulled by MAUI 10.0.20 | `Microsoft.WindowsAppSDK >= 1.7.250909003` on `net10.0-windows10.0.19041` ([NuGet Microsoft.Maui.Core 10.0.20](https://www.nuget.org/packages/Microsoft.Maui.Core/10.0.20)) — **WASDK 1.7, not 2.0** |
| XAML | `MauiXamlInflator=SourceGen` |
| Android Material 3 | MAUI documents `UseMaterial3` as Android-only and **not enabled by default** ([Material 3](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/material-design?view=net-maui-10.0)). GitPulse does not set it. |

Windows App SDK 1.3+ recommends setting WinUI `Window.SystemBackdrop` to `MicaBackdrop` / `DesktopAcrylicBackdrop`. The Composition `MicaController` / `DesktopAcrylicController` path remains documented for extra control ([System backdrops](https://learn.microsoft.com/en-us/windows/apps/develop/ui/system-backdrops)). GitPulse uses the controller path.

`SystemBackdropElement` (Mica/Acrylic on a **region**, not the whole window) requires **Windows App SDK 2.0 or later** ([System backdrops](https://learn.microsoft.com/en-us/windows/apps/develop/ui/system-backdrops), [Materials](https://learn.microsoft.com/en-us/windows/apps/develop/ui/materials)). MAUI 10.0.20 does not ship that SDK. It is not a MAUI control.

---

## 3. Theming

### What MAUI 10 gives

- Light/Dark via `{AppThemeBinding Light=…, Dark=…}`, `Application.RequestedTheme`, `Application.UserAppTheme`, and `RequestedThemeChanged` ([Respond to system theme changes](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/system-theme-changes?view=net-maui-10.0)).
- Runtime dictionary swap via `DynamicResource` ([Theme an app](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/theming?view=net-maui-10.0)).
- Android `MainActivity` **must** include `ConfigChanges.UiMode` or the activity restarts instead of receiving the theme change. GitPulse already sets it ([`MainActivity.cs`](../../src/GitPulse.App/Platforms/Android/MainActivity.cs)).
- Per-control `Style` in a MAUI `ResourceDictionary`. Shell attached colors (`Shell.BackgroundColor`, `Shell.TabBar*`) ([Shell tabs](https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/shell/tabs?view=net-maui-10.0)).

### What WinUI Fluent theming is, and MAUI XAML does not expose

WinUI apps set `Application.RequestedTheme` and consume `{ThemeResource …}` brushes (`TextFillColorPrimaryBrush`, `SystemAccentColor`, XAML type-ramp styles such as `BodyTextBlockStyle`) ([Theming in Windows apps](https://learn.microsoft.com/en-us/windows/apps/develop/ui/theming)). Those resources live in WinUI theme dictionaries, not in a MAUI `ResourceDictionary`.

GitPulse `App.xaml` uses the MAUI XML namespace and merges `Resources/Styles/Colors.xaml` + `Styles.xaml`. [`Platforms/Windows/App.xaml`](../../src/GitPulse.App/Platforms/Windows/App.xaml) is an empty `MauiWinUIApplication` — no WinUI `RequestedTheme`, no WinUI `ResourceDictionary`.

There is **no** `{ThemeResource}` usage in GitPulse XAML. Theming is `{AppThemeBinding}` + `{StaticResource}`.

### What GitPulse already uses

- Palette in [`Colors.xaml`](../../src/GitPulse.App/Resources/Styles/Colors.xaml): `Primary=#512BD4`, Magenta, Gray ramp, plus semantic Green/Purple/Orange/Red for issue/PR-style badges. Android splash/status colors in [`values/colors.xml`](../../src/GitPulse.App/Platforms/Android/Resources/values/colors.xml) match the same purple (`colorPrimary=#512BD4`).
- Widespread `{AppThemeBinding Light=…, Dark=…}` in [`Styles.xaml`](../../src/GitPulse.App/Resources/Styles/Styles.xaml).
- **No** `Application.UserAppTheme` / `RequestedTheme` override in app code — the app follows the OS theme.
- `Page` style: opaque `White` / `OffBlack`. `ContentPage` overrides `BackgroundColor` with `OnPlatform` **WinUI = Transparent** so the system backdrop can show; the style comment states other platforms keep the opaque page background.
- Shell chrome colors: White/OffBlack backgrounds; TabBar selected Magenta (light) / White (dark).
- Android theme: `Theme=@style/Maui.SplashTheme`.

Unused MAUI theming surface: `UserAppTheme` override, `DynamicResource` theme dictionaries, WinUI `{ThemeResource}` (only via native embedding).

---

## 4. Chrome (window, title bar, tray)

### MAUI `Window` (cross-platform API)

[`Microsoft.Maui.Controls.Window`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.maui.controls.window?view=net-maui-10.0) has `Title`, `TitleBar`, `IsMinimizable`, `IsMaximizable`, size/position, `StatusBarTheme` (mobile; no-op on desktop). It does **not** have `SystemBackdrop`.

[`TitleBar`](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/controls/titlebar?view=net-maui-10.0) is **Windows and Mac Catalyst only**. Set `Window.TitleBar`. Standard height 32px, can grow. Visual states include `TitleBarTitleActive` / `TitleBarTitleInactive`. Sample trailing content uses `FontFamily="SegoeMDL2"` (Windows font). Caption/search/person patterns that grow the bar to 48px are WinUI **design** guidance, not a MAUI property ([Title bar design](https://learn.microsoft.com/en-us/windows/apps/design/basics/titlebar-design)): 32px bar, 16x16 icon, Segoe UI Variable caption, Mica recommended as default title-bar background.

GitPulse:

- Creates `new Window(root)` in [`App.xaml.cs`](../../src/GitPulse.App/App.xaml.cs) and does **not** set `Window.TitleBar`, `IsMinimizable`, or `IsMaximizable`.
- [`Styles.xaml`](../../src/GitPulse.App/Resources/Styles/Styles.xaml) contains a **commented-out** `Style TargetType="TitleBar"` whose visual states match the MAUI TitleBar docs (template remnant). It is not applied.

### Safe area / IME

.NET 10 `SafeAreaEdges`: Android `ContentPage` default is **None** (edge-to-edge). With `WindowSoftInputModeAdjust.Resize`, docs say `All` is required so content stays above the IME ([Safe area](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/safe-area?view=net-maui-10.0)). GitPulse already sets `SafeAreaEdges` Android=`All`, WinUI=`Default`, and `WindowSoftInputModeAdjust="Resize"` / Android `SoftInput.AdjustResize`.

### Tray / toast (not MAUI)

ADR-005: Mica/Acrylic, tray, Toast are Windows-native, not Android parity. ADR-010: Windows tray + Toast in `App/Platforms`; Android implementations are empty.

GitPulse Windows:

- [`WindowsAppPresence`](../../src/GitPulse.App/Platforms/Windows/WindowsAppPresence.cs) uses **H.NotifyIcon.WinUI** `TaskbarIcon` and WinUI `MenuFlyout` / `MenuFlyoutItem` (Open, Notifications, Exit) — not MAUI `Flyout`.
- Icon file: `Resources/Raw/tray.ico` (MauiAsset).
- [`WindowsToastNotifier`](../../src/GitPulse.App/Platforms/Windows/WindowsToastNotifier.cs) for OS toast.

Android:

- [`AndroidAppPresence`](../../src/GitPulse.App/Platforms/Android/AndroidAppPresence.cs): `IsMainWindowVisible => true` (no tray).
- [`AndroidToastNotifier`](../../src/GitPulse.App/Platforms/Android/AndroidToastNotifier.cs): no-op.

There is no MAUI tray or toast API in use. These stay Windows-only compositor / shell integrations.

---

## 5. Typography

### MAUI 10

- Default font is **Open Sans on every platform** ([Fonts](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/fonts?view=net-maui-10.0)).
- Apps register TTF/OTF with `ConfigureFonts` / `MauiFont`. `OnPlatform` can set different `FontFamily` per platform.
- Android **system** families MAUI documents: `monospace`, `serif`, `sans-serif` (and condensed/light/medium/black variants). Not Segoe.
- Font icons: `FontImageSource` (Glyph + FontFamily). `FontImage` markup extension is **deprecated in .NET 10** ([Image](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/controls/image?view=net-maui-10.0), [What's new in .NET 10](https://learn.microsoft.com/en-us/dotnet/maui/whats-new/dotnet-10?view=net-maui-10.0)).

### Fluent / WinUI

- System UI font: **Segoe UI Variable**; type ramp sizes 12/14/18/20/28/40/68 epx (Small / Text / Display steps) ([Typography in Windows](https://learn.microsoft.com/en-us/windows/apps/design/signature-experiences/typography)).
- WinUI common controls pick Segoe UI Variable by default. MAUI global styles that set `FontFamily=OpenSansRegular` **override** that on Windows for those controls.
- **Selawik**: documented as an open-source font metrically compatible with Segoe UI, "intended for apps on other platforms that don't want to bundle Segoe UI" ([Typography in Windows](https://learn.microsoft.com/en-us/windows/apps/design/signature-experiences/typography)). That is a Windows-docs fact about a substitute, not a MAUI default.

### What GitPulse already uses

- [`MauiProgram.ConfigureFonts`](../../src/GitPulse.App/MauiProgram.cs): `OpenSans-Regular.ttf` to `OpenSansRegular`, `OpenSans-Semibold.ttf` to `OpenSansSemibold`.
- Global control styles set `FontFamily=OpenSansRegular` (Button, Editor, SearchBar, SearchHandler, and others).
- File editor: `OpenSansRegular`.
- Commit SHA UI: `FontFamily="Consolas"` (Windows system face; **not** registered as `MauiFont`).
- Diff HTML: `'Cascadia Mono', 'Consolas', 'Courier New', monospace` ([`DiffHtmlGenerator.cs`](../../src/GitPulse.App/Services/DiffHtmlGenerator.cs)).

Unused: Segoe UI Variable, WinUI type-ramp `{ThemeResource}` styles, `OnPlatform` font split, registered icon fonts.

---

## 6. Iconography

### MAUI 10

- `MauiImage`: SVG is converted to PNG at build; XAML must reference `.png` ([Image](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/controls/image?view=net-maui-10.0)).
- `MauiIcon` / `MauiSplashScreen` for app icon and splash.
- `FontImageSource` for unicode glyphs from a registered font.

### Fluent / WinUI

- Windows 11 UI icons: **Segoe Fluent Icons**. `FontIcon` / `SymbolIcon` use `SymbolThemeFontFamily`, which is Segoe Fluent Icons on Windows 11 and falls back to Segoe MDL2 Assets on Windows 10 20H2 or earlier ([Icons](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/icons)).
- Official constraint: you can download Segoe Fluent Icons for design/development; **"you may not ship it to another platform."** On Windows 11 it ships with the OS; on Windows 10 it is not included by default ([Segoe Fluent Icons font](https://learn.microsoft.com/en-us/windows/apps/design/iconography/segoe-fluent-icons-font)).

A Fluent glyph font is a **Windows-only** asset unless GitPulse ships a **different** licensed icon font on Android. MAUI will not provide Segoe Fluent Icons on Android.

### What GitPulse already uses

- App icon: `MauiIcon` `appicon.svg` + `appiconfg.svg`, `Color="#512BD4"`.
- Splash: `splash.svg`, same purple.
- Only raster in `Resources/Images`: `dotnet_bot.png` (`Resize=True`, `BaseSize=300,185`).
- Shell tabs Repos / Notifications / Search: `Icon="dotnet_bot.png"`. **Settings has no `Icon`.**
- Tray: `tray.ico`.
- No `FontImageSource`, no Segoe Fluent Icons, no `SymbolThemeFontFamily`.

---

## 7. Acrylic / Mica

### WinUI / WASDK (Windows only)

Two supported application paths ([System backdrops](https://learn.microsoft.com/en-us/windows/apps/develop/ui/system-backdrops), [Materials](https://learn.microsoft.com/en-us/windows/apps/develop/ui/materials)):

1. **Recommended (WASDK 1.3+):** `Microsoft.UI.Xaml.Window.SystemBackdrop = MicaBackdrop` or `DesktopAcrylicBackdrop`.
2. **Controllers:** `MicaController` / `DesktopAcrylicController` plus `SystemBackdropConfiguration` plus a `DispatcherQueue`, gated by `IsSupported()`.

Facts from those pages (not GitPulse comments):

| Material | Role | API | OS |
|----------|------|-----|----|
| **Mica** | Opaque wallpaper-tinted **window foundation** (title bar / nav recommended) | `MicaBackdrop` or `MicaController`; `Kind` `Base` or `BaseAlt` | Windows 11; solid fallback on Windows 10 |
| **Desktop Acrylic** | Frosted-glass window or transient surface | `DesktopAcrylicBackdrop` or `DesktopAcrylicController` | Windows 10 build 17763+ |
| **Fluent Acrylic guidance** | Transient light-dismiss (flyouts, menus) | `SystemBackdrop` on `FlyoutBase` / `Popup` / `MenuFlyoutPresenter` | same |
| **In-app `AcrylicBrush`** | Blurs **in-window** XAML only — **no HostBackdrop** in WinUI 3 | `{ThemeResource AcrylicInAppFillColorDefaultBrush}` | WinUI 3 |
| **`SystemBackdropElement`** | Mica/Acrylic on a **region** | WASDK **2.0+** | not in MAUI 10's WASDK 1.7 |

Fallback (the APIs handle it; the app should still look correct as a solid): Remote Desktop/VM, insufficient GPU, Transparency effects off, Battery Saver (**Acrylic** only; Mica is not affected), High Contrast ([Materials](https://learn.microsoft.com/en-us/windows/apps/develop/ui/materials)).

Opaque page backgrounds cover the backdrop. WinUI structure guidance: do not paint an opaque `Background` on the window / page if Mica should show ([Structure a modern WinUI 3 desktop app](https://learn.microsoft.com/en-us/windows/apps/develop/ui/windows-app-sdk-app-structure)).

**There is no MAUI cross-platform Mica/Acrylic API.** Android has no equivalent compositor material in MAUI.

### What GitPulse already uses

[`WindowHelpers.TryMicaOrAcrylic`](../../src/GitPulse.App/Platforms/Windows/WindowHelpers.cs), called from `App.OnWindowHandlerChanged` when `PlatformView` is `Microsoft.UI.Xaml.Window`:

1. Ensure `DispatcherQueue`.
2. `SystemBackdropConfiguration` with theme from `FrameworkElement.ActualTheme`.
3. If `MicaController.IsSupported()` then `new MicaController()` (default Kind, **not** `BaseAlt`).
4. Else if `DesktopAcrylicController.IsSupported()` then `DesktopAcrylicController`.
5. Else leave solid.

GitPulse comments say Mica is preferred on Windows 11 22H2+ and Acrylic on older Windows 11; the **code** only trusts `IsSupported()`. It does **not** set WinUI `Window.SystemBackdrop`. ContentPage WinUI transparent background is required for the effect.

Android: no-op (no call).

Unused Windows surface: `MicaBackdrop` XAML, `Kind=BaseAlt`, in-app `AcrylicBrush`, flyout `SystemBackdrop`, `SystemBackdropElement` (blocked by WASDK 2.0 anyway).

---

## 8. Shell

### What MAUI documents

- `TabBar` is the **conceptual bottom tab bar** and **disables the flyout** ([Shell tabs](https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/shell/tabs?view=net-maui-10.0), [Shell overview](https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/shell/?view=net-maui-10.0)).
- More than five tabs produce a **More** overflow (Android string `overflow_tab_title`). GitPulse has **four** tabs.
- Appearance: `Shell.*` and `TabBar*` attached colors. GitPulse styles these.
- Integrated `SearchHandler` (search box in Shell chrome). GitPulse does not use it; Search is a tab with a page `SearchBar` (ADR-007).
- .NET 10 Shell chrome addition: `Shell.NavBarVisibilityAnimationEnabled` only ([What's new](https://learn.microsoft.com/en-us/dotnet/maui/whats-new/dotnet-10?view=net-maui-10.0)). Unused.

### Windows implementation (MAUI 10.0.20 source)

[`ShellItemHandler.Windows.cs`](https://github.com/dotnet/maui/blob/10.0.20/src/Controls/src/Core/Handlers/Shell/ShellItemHandler.Windows.cs):

- `CreatePlatformElement()` returns `MauiNavigationView`.
- `PaneDisplayMode = Top`.
- Back button collapsed, Settings hidden, pane toggle hidden.
- Tab icons: `bsi.Icon?.ToIconSource(MauiContext)` then WinUI `IconElement`.
- Optional `SearchHandler` maps to WinUI `AutoSuggestBox` (width 300). Unused by GitPulse.

A MAUI `TabBar` on Windows is a **top WinUI `NavigationView`**, not a bottom tab bar and not a left Fluent sidebar `NavigationView`.

### Android implementation (MAUI 10.0.20 source)

Official docs: **starting in .NET MAUI 11**, Shell on Android uses handlers (`ShellHandler` / `ShellItemHandler` / `ShellSectionHandler`) by default ([Shell overview](https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/shell/?view=net-maui-10.0)). MAUI **10** Android still uses the compatibility renderer:

[`ShellItemRenderer.cs` (tag 10.0.20)](https://github.com/dotnet/maui/blob/10.0.20/src/Controls/src/Core/Compatibility/Handlers/Shell/Android/ShellItemRenderer.cs) in `Microsoft.Maui.Controls.Platform.Compatibility` creates Material `BottomNavigationView` (`Google.Android.Material.BottomNavigation`). Overflow uses a bottom sheet (`MoreTabId = 99`).

**Layout is not the same as Windows.** Same MAUI `TabBar` XAML becomes a top `NavigationView` on Windows and bottom Material tabs on Android. That is the platform fact behind "rule parity, not layout parity."

If Material 3 were enabled, Android Shell tabs would use Material 3 `BottomNavigationView` / `TabLayout` ([Material 3](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/material-design?view=net-maui-10.0)). GitPulse does not enable it.

### What GitPulse already uses

[`AppShell.xaml`](../../src/GitPulse.App/AppShell.xaml):

- `TabBar` `MainTabBar`: Repos, Notifications, Search, Settings.
- Stacked details registered as **sibling `ShellContent`** after the TabBar (routes for `GoToAsync`), not `Routing.RegisterRoute`.
- [`AppShell.xaml.cs`](../../src/GitPulse.App/AppShell.xaml.cs): `InitializeComponent` only.

UI tests: `GITPULSE_UI_TEST_HOST=1` swaps Shell for `TabbedPage` plus `NavigationPage` because **Shell plus NavigationView hides `ContentPage` from UIA** ([`docs/DEVELOPMENT.md`](../DEVELOPMENT.md), glossary "UI Test Host").

---

## 9. Hard limits vs Fluent / WinUI

| Capability | MAUI 10 Windows (`net10.0-windows10.0.19041.0`) | MAUI 10 Android | Fluent / WinUI (native) |
|------------|--------------------------------------------------|-----------------|-------------------------|
| Native stack | WinUI 3 + WASDK **1.7** | AndroidX / Material (M3 opt-in, off) | WinUI 3 + WASDK (2.0 APIs exist but not via MAUI 10) |
| Theme markup | `AppThemeBinding` / MAUI `Style` | same XAML; `UiMode` on Activity | `{ThemeResource}`, `SystemAccentColor`, type-ramp styles |
| Default UI font | Open Sans (MAUI default; GitPulse sets it) | Open Sans; Android generic families available | Segoe UI Variable + type ramp |
| Icon font | Any **shipped** TTF via `FontImageSource` | Same; **must ship** the font | Segoe Fluent Icons on Win11; **may not ship to another platform** |
| Title bar | MAUI `TitleBar` (unused) | N/A | 32px Mica caption; custom WinUI title bar |
| Window backdrop | Native only (`MicaController` / `SystemBackdrop` on WinUI `Window`) | **None** | `MicaBackdrop` / `DesktopAcrylicBackdrop` |
| Region backdrop | Not in WASDK 1.7 (`SystemBackdropElement` needs 2.0) | **None** | WASDK 2.0 `SystemBackdropElement` |
| In-app Acrylic | WinUI `AcrylicBrush` via native embedding only | **None** | In-app only (no HostBackdrop) |
| Shell tabs | Top `MauiNavigationView` | Material `BottomNavigationView` | App-authored `NavigationView` (often left pane) |
| Tray / Toast | GitPulse WinUI + `AppNotificationManager` | Empty (ADR-010) | Desktop shell, not a WinUI page control |
| MAUI `Window.SystemBackdrop` | **Does not exist** | — | WinUI `Window.SystemBackdrop` exists |

---

## 10. GitPulse already uses vs unused MAUI 10 / WinUI surface

**Already in the app (do not treat as net-new platform work):**

- OpenSans + template purple `Styles` / `Colors`
- `AppThemeBinding` light/dark, OS theme (no `UserAppTheme`)
- Shell `TabBar` of four tabs; Magenta/White selected colors
- `dotnet_bot.png` on three tabs; Settings has no icon
- Windows Mica controller + Acrylic fallback + transparent WinUI page background
- Windows tray (`tray.ico`, WinUI `MenuFlyout`) and Toast
- Android IME/safe-area (`SafeAreaEdges=All`, `AdjustResize`, `UiMode`)
- 44px `MinimumHeightRequest` / `MinimumWidthRequest` on several control styles (Button, CheckBox, Editor, and others)

**Present in MAUI 10 / WASDK 1.7, unused by GitPulse:**

- `Window.TitleBar` (Windows-only)
- `Window.IsMinimizable` / `IsMaximizable`
- `Application.UserAppTheme`
- `DynamicResource` theme dictionaries
- `FontImageSource` (and any registered icon font)
- `OnPlatform` font families (for example Segoe UI Variable on WinUI versus Android generic / bundled TTF)
- Shell `SearchHandler` (would become a WinUI `AutoSuggestBox` on Windows)
- `Shell.NavBarVisibilityAnimationEnabled`
- WinUI `Window.SystemBackdrop` (`MicaBackdrop` / `Kind=BaseAlt`) instead of controllers
- In-app `AcrylicBrush` / flyout `SystemBackdrop`
- Android `UseMaterial3`

**Hard "not on this stack":**

- `{ThemeResource}` / WinUI type ramp inside MAUI XAML
- Shipping Segoe Fluent Icons to Android
- MAUI API for Mica/Acrylic on Android
- `SystemBackdropElement` while MAUI 10 pins WASDK 1.7
- Pixel-identical Shell chrome (top NavigationView versus bottom Material tabs)

---

## 11. Pointers

| Kind | Where |
|------|--------|
| GitPulse styles / shell | [`Colors.xaml`](../../src/GitPulse.App/Resources/Styles/Colors.xaml), [`Styles.xaml`](../../src/GitPulse.App/Resources/Styles/Styles.xaml), [`AppShell.xaml`](../../src/GitPulse.App/AppShell.xaml) |
| GitPulse Windows chrome | [`WindowHelpers.cs`](../../src/GitPulse.App/Platforms/Windows/WindowHelpers.cs), [`App.xaml.cs`](../../src/GitPulse.App/App.xaml.cs), [`WindowsAppPresence.cs`](../../src/GitPulse.App/Platforms/Windows/WindowsAppPresence.cs) |
| ADRs | [ADR-005](../adr/ADR-005-windows-first-platform-strategy.md), [ADR-010](../adr/ADR-010-windows-tray-presence-and-toast.md), [ADR-011](../adr/ADR-011-android-m11-daily-usable-phone.md), [ADR-007](../adr/ADR-007-manual-searchbar-event-bridge.md) |
| MAUI 10 | [Supported platforms](https://learn.microsoft.com/en-us/dotnet/maui/supported-platforms?view=net-maui-10.0), [Handlers](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/handlers/?view=net-maui-10.0), [Fonts](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/fonts?view=net-maui-10.0), [TitleBar](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/controls/titlebar?view=net-maui-10.0), [Shell tabs](https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/shell/tabs?view=net-maui-10.0), [Window API](https://learn.microsoft.com/en-us/dotnet/api/microsoft.maui.controls.window?view=net-maui-10.0) |
| WinUI / WASDK | [WinUI 3](https://learn.microsoft.com/en-us/windows/apps/winui/winui3/), [Theming](https://learn.microsoft.com/en-us/windows/apps/develop/ui/theming), [System backdrops](https://learn.microsoft.com/en-us/windows/apps/develop/ui/system-backdrops), [Materials](https://learn.microsoft.com/en-us/windows/apps/develop/ui/materials), [Typography](https://learn.microsoft.com/en-us/windows/apps/design/signature-experiences/typography), [Segoe Fluent Icons](https://learn.microsoft.com/en-us/windows/apps/design/iconography/segoe-fluent-icons-font) |
| MAUI 10.0.20 source | [ShellItemHandler.Windows.cs](https://github.com/dotnet/maui/blob/10.0.20/src/Controls/src/Core/Handlers/Shell/ShellItemHandler.Windows.cs), [ShellItemRenderer.cs (Android)](https://github.com/dotnet/maui/blob/10.0.20/src/Controls/src/Core/Compatibility/Handlers/Shell/Android/ShellItemRenderer.cs) |
| NuGet | [Microsoft.Maui.Core 10.0.20](https://www.nuget.org/packages/Microsoft.Maui.Core/10.0.20) (WASDK 1.7.250909003) |
