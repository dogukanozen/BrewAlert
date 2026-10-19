# BrewAlert — Architecture

For day-to-day work most agents should read [`../AGENT.md`](../AGENT.md), not this file. Open this one only when you actually need architectural detail (adding a layer, changing DI lifetimes, touching threading).

## 1. Layers

```
┌─────────────────────────────────────────────────┐
│                  BrewAlert.UI                    │
│   Views │ ViewModels │ NavigationService │ DI    │
├─────────────────────────────────────────────────┤
│             BrewAlert.Infrastructure             │
│ Teams (Webhook/Graph) │ JSON persistence │ Console │
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

### 2.3 Event-driven timer
`BrewTimerService` supports **any number of concurrent sessions** — each call to `Start(profile)` spins up its own `PeriodicTimer` (1 s) in a background task. It raises `TimerTick` / `BrewStarted` / `BrewCompleted` / `BrewCancelled`; `TimerTick` carries a `BrewTimerTickEvent` (session id + remaining) so listeners can filter by session. The UI layer (`ActiveBrewsViewModel` + per-row `BrewItemViewModel`) marshals those onto the Avalonia UI thread via `Dispatcher.UIThread`.

**Thread-safety rules (all enforced in code today):**
- Session state mutations are inside a single `lock (_lock)` block.
- Events are fired **outside** that lock (re-entrant handlers used to deadlock).
- Any VM that subscribes to these events **or to `ILocalizationService.LanguageChanged`** implements `IDisposable` and unsubscribes in `Dispose()`.

### 2.4 DI lifetimes

Registered in `src/BrewAlert.UI/App.axaml.cs → ConfigureServices`.

| Registration | Lifetime | Reason |
|---|---|---|
| `MainWindowViewModel` | Singleton | Lives for the whole window |
| `INavigationService` | Singleton | Shared navigation state |
| `IBrewTimerService` | Singleton | Manages all concurrent brew sessions |
| `BrewProfileService` | Singleton | Stateless helper |
| `IProfileRepository` | Singleton | Stateless I/O facade |
| `INotificationService` | Singleton | Routing facade → Teams (Graph or Webhook) or Console |
| `IPreferencesService` | Singleton | Persists user preferences to `preferences.json` |
| `ILocalizationService` | Singleton | Runtime EN/TR string table; fires `LanguageChanged` |
| `IAppearanceService` | Singleton | Current appearance, persisted fixed/weekly selection, idle-home calendar changes |
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
  └─ BrewCompleted               →   BrewCompletionNotificationService dedupes & sends one notification
                                                              │
                                                              ▼
                                                   [Teams Graph | Teams Webhook | Console]
                                                   (Notification sent to chosen provider)
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

Auto-updates: Velopack downloads the new `.nupkg` from GitHub Releases, replaces `~/brewalert/BrewAlert.AppImage` in place, and exits. Systemd restarts the service automatically with the updated binary.
