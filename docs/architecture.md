# BrewAlert — Architecture

For day-to-day work most agents should read [`../AGENT.md`](../AGENT.md), not this file. Open this one only when you actually need architectural detail (adding a layer, changing DI lifetimes, touching threading).

## 1. Layers

```
┌─────────────────────────────────────────────────┐
│                  BrewAlert.UI                    │
│   Views │ ViewModels │ NavigationService │ DI    │
├─────────────────────────────────────────────────┤
│             BrewAlert.Infrastructure             │
│ Teams (Webhook/Graph) │ JSON + SQLite │ Console │
├─────────────────────────────────────────────────┤
│                 BrewAlert.Core                   │
│    Models │ Interfaces │ Services │ Events       │
│               (zero dependencies)                │
└─────────────────────────────────────────────────┘
```

Dependency direction is one-way: UI → Infrastructure → Core. Core has no project references; Infrastructure references only Core; UI references both. Never reverse.

## 2. Key design decisions

### 2.1 Interfaces live in Core
All services are consumed through interfaces under `BrewAlert.Core/Interfaces/`. That keeps the domain testable and lets us swap implementations (`TeamsWebhookNotifier` ↔ `TeamsGraphNotifier` ↔ `ConsoleNotifier`).

### 2.2 NavigationService (no service locator)
ViewModels **never** touch `App.Services` / `IServiceProvider`. All view transitions go through `INavigationService`:

```
ProfileListVM ──→ INavigationService.NavigateTo<ActiveBrewsVM>()
                     │
               NavigationService resolves VM from DI
                     │
               Fires CurrentViewChanged event
                     │
               MainWindowVM updates CurrentView
                     │
               ContentControl renders the new view
```

Why: all dependencies visible in the constructor, VMs unit-testable with a mocked `INavigationService`, no hidden coupling.

Each `NavigateTo<T>()` resolves the ViewModel in a new DI scope; the next navigation disposes the previous ViewModel and its scope. Transient `IDisposable` ViewModels resolved from the root provider would be tracked by the root until shutdown, leaking one instance per navigation on a kiosk that never restarts.

### 2.3 Event-driven timer
`BrewTimerService` supports **any number of concurrent sessions** — each call to `Start(profile)` spins up its own `PeriodicTimer` (1 s) in a background task. It raises `TimerTick` / `BrewStarted` / `BrewCompleted` / `BrewCancelled`; `TimerTick` carries a `BrewTimerTickEvent` (session id + remaining) so listeners can filter by session. The UI layer (`ActiveBrewsViewModel` + per-row `BrewItemViewModel`) marshals those onto the Avalonia UI thread via `Dispatcher.UIThread`.

**Time:** each session keeps a deadline on the monotonic clock of the injected `TimeProvider`. Every tick recomputes `Remaining` from that deadline (rounded to whole seconds) instead of subtracting one second, because `PeriodicTimer` coalesces missed ticks: a 4 s stall used to make a 10 s brew take 12 s. Pause freezes `Remaining`; resume sets a new deadline and moves `EndsAtUtc`. The monotonic clock also ignores wall-clock jumps (NTP on a Pi without RTC).

**Snapshots:** `Start`, `GetActiveSessions` and every event hand out copies of `BrewSession`; the live objects are only touched under the lock.

**Thread-safety rules (all enforced in code today):**
- Session state mutations are inside a single `lock (_lock)` block.
- Events are fired **outside** that lock (re-entrant handlers used to deadlock).
- Any VM or service that subscribes to these events **or to another singleton's event** (`ILocalizationService.LanguageChanged`, `IBrewHistoryService.HistoryUpdated`, `IAppearanceService.PropertyChanged`, `IUpdateService.UpdateAvailable`) implements `IDisposable` and unsubscribes in `Dispose()`.

### 2.4 DI lifetimes

Registered in `src/BrewAlert.UI/App.axaml.cs → ConfigureServices`.

| Registration | Lifetime | Reason |
|---|---|---|
| `MainWindowViewModel` | Singleton | Lives for the whole window; idle check, update toast |
| `AmbientOverlayViewModel` | Singleton | Owned by `MainWindowViewModel` for the idle screen |
| `INavigationService` | Singleton | Shared navigation state |
| `IBrewTimerService` | Singleton | Manages all concurrent brew sessions |
| `BrewProfileService` | Singleton | Stateless helper |
| `IProfileRepository` | Singleton | Stateless I/O facade over `profiles.json` |
| `IBrewHistoryRepository` | Singleton | SQLite `history.db` access |
| `IBrewHistoryService` | Singleton | Records every `BrewCompleted`; fires `HistoryUpdated`. Resolved eagerly at startup so it subscribes before the first brew |
| `INotificationService` | Singleton | Routing facade → Teams (Graph or Webhook) or Console |
| `IBrewCompletionNotificationService` | Singleton | Sends one notification per completed session (dedupe), outlives any timer view |
| `IPreferencesService` | Singleton | Persists user preferences to `preferences.json` |
| `ILocalizationService` | Singleton | Runtime EN/TR string table; fires `LanguageChanged` |
| `IAppearanceService` | Singleton | Current appearance, persisted fixed/weekly selection, idle-home calendar changes |
| `IUpdateService` | Singleton | Velopack check/download; applies only on user request, never while a brew is running |
| `INetworkInfoService` | Singleton | IPv4 addresses and Wi-Fi name for Settings; raises `Changed` on address changes |
| `TimeProvider` | Singleton | `TimeProvider.System`; `AppearanceServiceTests` injects its own clock |
| `ActiveBrewsViewModel` | Transient | Hosts a live list of `BrewItemViewModel`s; fresh per navigation |
| `ProfileListViewModel` | Transient | Re-fetches profiles on each visit |
| `SettingsViewModel` | Transient | Re-reads config on each visit |

### 2.5 Notifier selection

`INotificationService` is resolved by `RoutingNotificationService` (singleton), which reads `IOptionsMonitor<NotificationProviderOptions>` on every call. This means changing `BrewAlert:Notifications:Provider` in `preferences.json` takes effect immediately without a restart.

Available providers:

| `Provider` value | Notifier used |
|---|---|
| `"Graph"` | `TeamsGraphNotifier` — OAuth2 client credentials, posts to Graph `chats/{id}/messages` |
| `"Webhook"` | `TeamsWebhookNotifier` — HTTP POST to Teams Incoming Webhook URL |
| `"Console"` (default) | `ConsoleNotifier` — logs to stdout; useful for development |

Set the active provider in `%AppData%\BrewAlert\preferences.json`:
```json
{
  "BrewAlert": {
    "Notifications": {
      "Provider": "Webhook"
    }
  }
}
```
Or via environment variable: `BREWALERT__BrewAlert__Notifications__Provider=Webhook`

**Retries:** notifiers mark a failure transient (`NotificationResult.IsTransient`) for network errors, timeouts, 408, 429 and 5xx (`HttpFailure`). `BrewCompletionNotificationService` retries those after 10 s, 30 s, 1 min, 2 min and 5 min, then reports the last result; configuration errors and other 4xx are reported at once. There is no persistent queue: retries stop when the app exits.

### 2.6 Configuration precedence

```
appsettings.json                       (committed, defaults only)
  ↓ overridden by
appsettings.{DOTNET_ENVIRONMENT}.json  (optional, gitignored in practice)
  ↓ overridden by
%AppData%\BrewAlert\preferences.json   (user preferences, written by Settings screen)
  ↓ overridden by
Environment variables prefixed BREWALERT__
```

`AddEnvironmentVariables("BREWALERT__")` strips the `BREWALERT__` prefix and converts `__` to `:`. The `BrewAlert:` config root segment must therefore still be present in the variable name after the prefix:

| Config key | Environment variable |
|---|---|
| `BrewAlert:Notifications:Provider` | `BREWALERT__BrewAlert__Notifications__Provider` |
| `BrewAlert:Notifications:Teams:WebhookUrl` | `BREWALERT__BrewAlert__Notifications__Teams__WebhookUrl` |
| `BrewAlert:Notifications:TeamsGraph:TenantId` | `BREWALERT__BrewAlert__Notifications__TeamsGraph__TenantId` |
| `BrewAlert:Notifications:TeamsGraph:ClientId` | `BREWALERT__BrewAlert__Notifications__TeamsGraph__ClientId` |
| `BrewAlert:Notifications:TeamsGraph:ClientSecret` | `BREWALERT__BrewAlert__Notifications__TeamsGraph__ClientSecret` |
| `BrewAlert:Notifications:TeamsGraph:ChatId` | `BREWALERT__BrewAlert__Notifications__TeamsGraph__ChatId` |

### 2.7 Security posture
- No secrets in source.
- Webhook URLs and Client Secrets supplied via env vars (or gitignored env-specific `appsettings`).
- `git diff --cached` is the last line of defence — use it.

### 2.8 Appearance and kiosk rendering

`AppearanceService` reads `BrewAlert:Appearance` and uses `TimeProvider.GetLocalNow()`.
The default schedule alternates Obsidian/Porcelain from Monday, with each weekday editable.
A fixed choice applies immediately, including during an active brew. Calendar changes are checked
on the existing 30-second idle tick and on navigation; they wait for the profile screen with no active sessions.
Changing appearance does not navigate or recreate the timer service.

`App` selects the Avalonia Light/Dark theme variant; DynamicResource brushes and the shell's
`porcelain` class update colors and layout together. `PreferencesService` merges individual JSON
settings atomically, so saving language/provider/webhook does not discard the weekly schedule or unknown fields.

UI text uses the bundled Inter font, and icons use Avalonia Geometry paths rather than emoji
or icon fonts. Ambient colors are cached neutral brushes for both appearances; DVD edge feedback
changes opacity. All existing motion modes remain, with no added blur, shaders, browser rendering,
or per-frame brush creation. Orbit sizing protects the clock/date on narrow screens.

Avalonia does not pause style animations on hidden controls, and the overlay stays in the tree.
Every ambient animation selector is therefore prefixed with `Panel.ambient-live :is(Control).ambient-layer[IsVisible=True]`:
the root panel carries `ambient-live` only while the idle screen is shown, and each mode/backdrop panel
carries `ambient-layer`, so only the visible mode animates and nothing animates during normal use.

### 2.9 Persistence and failure handling

- `profiles.json` is written to a `.tmp` file and moved into place. A corrupt file is copied to `.bak`, logged,
  and treated as empty so the defaults are re-seeded; the home screen shows load errors instead of staying empty.
- `history.db` is versioned with `PRAGMA user_version`; `SqliteBrewHistoryRepository.Migrations[n]` upgrades
  version n to n + 1 inside a transaction. Append steps, never edit a shipped one.
- `App` logs unhandled UI exceptions and marks them handled, so one bad handler does not end the process and
  drop the in-memory timers. Unobserved task exceptions are logged and observed.
- Running sessions are not persisted: a crash, power cut or update still loses them.

## 3. Data flow — happy path

```
User taps profile card
  │
  ▼
ProfileListVM.SelectProfile()
  │
  ▼
IBrewTimerService.Start(profile)   →   BrewStarted event   →   MainWindowVM updates Title (via dispatcher)
  │
  ▼
INavigationService.NavigateTo<ActiveBrewsVM>()   →   MainWindowVM.CurrentView updates
  │
  ▼
ActiveBrewsVM hydrates a BrewItemViewModel for the new session (and any others already running)
  │
  ▼
Per-session PeriodicTimer loop (1 s)
  │
  ├─ TimerTick (with session id)  →   BrewItemViewModel filters & updates its countdown
  │
  └─ BrewCompleted ─┬─→ BrewCompletionNotificationService dedupes & sends one notification
                    │                                         │
                    │                                         ▼
                    │                              [Teams Graph | Teams Webhook | Console]
                    │                              (Notification sent to chosen provider)
                    │
                    └─→ BrewHistoryService writes the session to history.db → HistoryUpdated
                                  (home screen recent brews, ambient freshness scenes)
```

## 4. Raspberry Pi deployment

Low-end target: Raspberry Pi 3. Headless Skia rendering covers the 800 × 480 kiosk layout and
320 px narrow windows. These checks validate rendering and interaction, not frame rate on physical hardware.
The UI project targets both `linux-arm64` and `linux-arm`; the installed OS determines the appropriate RID.
The existing release workflow packages `linux-arm64` only.

Releases are built by CI on `v*` tags and published to GitHub Releases as `brewalert-<version>-linux-arm64.tar.gz`. The archive includes `install.sh` which handles system dependencies, app installation to `~/brewalert/`, and systemd service setup.

```bash
wget https://github.com/dogukanozen/BrewAlert/releases/latest/download/brewalert-<version>-linux-arm64.tar.gz
mkdir -p ~/brewalert && tar -xzf brewalert-*.tar.gz -C ~/brewalert/
bash ~/brewalert/install.sh
```

The app runs with `--drm` (KMS/DRM direct rendering, no X11 required). The systemd service is created by `install.sh` at `/etc/systemd/system/brewalert.service` with `Restart=always`.

System dependencies installed by `install.sh`: `libdrm2`, `libgbm1`, `libfontconfig1`, `libfreetype6`, `libinput10` (touch/keyboard input in DRM mode), `libfuse2` (AppImage runtime).

Updates: `MainWindowViewModel` calls `IUpdateService.CheckForUpdatesAsync()` at startup and every hour. Velopack downloads a new release from GitHub Releases in the background, then the UI shows an install/dismiss toast. Nothing is applied until the user taps install, and the tap is refused while a brew is running: `ApplyUpdatesAndExit` replaces `~/brewalert/BrewAlert.AppImage` in place and exits, and systemd restarts the service with the updated binary.
