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
ProfileListVM ──→ INavigationService.NavigateTo<BrewTimerVM>()
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
`BrewTimerService` runs a `PeriodicTimer` (1 s) in a background task and raises `TimerTick` / `BrewStarted` / `BrewCompleted` / `BrewCancelled`. The UI layer marshals those onto the Avalonia UI thread via `Dispatcher.UIThread`.

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
| `IBrewTimerService` | Singleton | One active brew session at a time |
| `BrewProfileService` | Singleton | Stateless helper |
| `IProfileRepository` | Singleton | Stateless I/O facade |
| `INotificationService` | Singleton | Routing facade → Teams (Graph or Webhook) or Console |
| `IPreferencesService` | Singleton | Persists user preferences to `preferences.json` |
| `ILocalizationService` | Singleton | Runtime EN/TR string table; fires `LanguageChanged` |
| `BrewTimerViewModel` | Transient | Fresh instance per navigation (prevents stale state) |
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
Or via environment variable: `BREWALERT__Notifications__Provider=Webhook`

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

Environment variable mapping examples (prefix `BREWALERT__` replaces the `BrewAlert:` root segment):

| Config key | Environment variable |
|---|---|
| `BrewAlert:Notifications:Provider` | `BREWALERT__Notifications__Provider` |
| `BrewAlert:Notifications:Teams:WebhookUrl` | `BREWALERT__Notifications__Teams__WebhookUrl` |
| `BrewAlert:Notifications:TeamsGraph:TenantId` | `BREWALERT__Notifications__TeamsGraph__TenantId` |
| `BrewAlert:Notifications:TeamsGraph:ClientId` | `BREWALERT__Notifications__TeamsGraph__ClientId` |
| `BrewAlert:Notifications:TeamsGraph:ClientSecret` | `BREWALERT__Notifications__TeamsGraph__ClientSecret` |
| `BrewAlert:Notifications:TeamsGraph:ChatId` | `BREWALERT__Notifications__TeamsGraph__ChatId` |

### 2.7 Security posture
- No secrets in source.
- Webhook URLs and Client Secrets supplied via env vars (or gitignored env-specific `appsettings`).
- `git diff --cached` is the last line of defence — use it.

## 3. Data flow — happy path

```
User taps profile card
  │
  ▼
ProfileListVM.SelectProfile()
  │
  ▼
INavigationService.NavigateTo<BrewTimerVM>()   →   MainWindowVM.CurrentView updates
  │
  ▼
BrewTimerVM.StartBrew(profile)
  │
  ▼
IBrewTimerService.Start(profile)   →   BrewStarted event   →   MainWindowVM updates Title
  │
  ▼
PeriodicTimer loop (1 s)
  │
  ├─ TimerTick        →   BrewTimerVM (UI thread)   →   countdown display
  │
  └─ BrewCompleted    →   BrewTimerVM                →   INotificationService.SendBrewCompletedAsync()
                                                              │
                                                              ▼
                                                   [Teams Graph | Teams Webhook | Console]
                                                   (Notification sent to chosen provider)
```

## 4. Raspberry Pi deployment

Target: Raspberry Pi 4 / 5 with a 7" touchscreen (800 × 480).

```bash
dotnet publish src/BrewAlert.UI -c Release -r linux-arm64 --self-contained
```

The Pi runs the published output as a systemd service for auto-start on boot (service unit lives on the Pi, not in this repo).
