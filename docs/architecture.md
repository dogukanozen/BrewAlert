# BrewAlert — Architecture

For day-to-day work most agents should read [`../AGENT.md`](../AGENT.md), not this file. Open this one only when you actually need architectural detail (adding a layer, changing DI lifetimes, touching threading).

## 1. Layers

```
┌─────────────────────────────────────────────────┐
│                  BrewAlert.UI                    │
│   Views │ ViewModels │ NavigationService │ DI    │
├─────────────────────────────────────────────────┤
│             BrewAlert.Infrastructure             │
│   Teams webhook │ JSON persistence │ Console     │
├─────────────────────────────────────────────────┤
│                 BrewAlert.Core                   │
│    Models │ Interfaces │ Services │ Events       │
│               (zero dependencies)                │
└─────────────────────────────────────────────────┘
```

Dependency direction is one-way: UI → Infrastructure → Core. Core has no project references; Infrastructure references only Core; UI references both. Never reverse.

## 2. Key design decisions

### 2.1 Interfaces live in Core
All services are consumed through interfaces under `BrewAlert.Core/Interfaces/`. That keeps the domain testable and lets us swap implementations (`TeamsWebhookNotifier` ↔ `ConsoleNotifier` is the live example — see §2.5).

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
- Any VM that subscribes to these events implements `IDisposable` and unsubscribes in `Dispose()`.

### 2.4 DI lifetimes

Registered in `src/BrewAlert.UI/App.axaml.cs → ConfigureServices`.

| Registration | Lifetime | Reason |
|---|---|---|
| `MainWindowViewModel` | Singleton | Lives for the whole window |
| `INavigationService` | Singleton | Shared navigation state |
| `IBrewTimerService` | Singleton | One active brew session at a time |
| `BrewProfileService` | Singleton | Stateless helper |
| `IProfileRepository` | Singleton | Stateless I/O facade |
| `INotificationService` | Singleton | Either Teams or Console (resolved at startup) |
| `BrewTimerViewModel` | Transient | Fresh instance per navigation (prevents stale state) |
| `ProfileListViewModel` | Transient | Re-fetches profiles on each visit |
| `SettingsViewModel` | Transient | Re-reads config on each visit |

### 2.5 Notifier selection
`INotificationService` is registered via a factory. If `Notifications:Teams:Enabled=true` **and** `WebhookUrl` is non-empty, we resolve `TeamsWebhookNotifier` (an `HttpClient`-based adaptive-card poster). Otherwise we fall back to `ConsoleNotifier` — which is what local dev and CI use.

### 2.6 Configuration precedence

```
appsettings.json                     (committed, defaults only)
  ↓ overridden by
appsettings.{DOTNET_ENVIRONMENT}.json  (optional, gitignored in practice)
  ↓ overridden by
Environment variables prefixed BREWALERT__
```

### 2.7 Security posture
- No secrets in source.
- Webhook URL supplied via env var (or gitignored env-specific `appsettings`).
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
IBrewTimerService.Start(profile)   →   BrewStarted event
  │
  ▼
PeriodicTimer loop (1 s)
  │
  ├─ TimerTick        →   BrewTimerVM (UI thread)   →   countdown display
  │
  └─ BrewCompleted    →   BrewTimerVM                →   INotificationService.SendBrewCompletedAsync()
                                                              │
                                                              ▼
                                                   HTTP POST → Teams webhook (adaptive card)
                                                   (or console log if Teams disabled)
```

## 4. Raspberry Pi deployment

Target: Raspberry Pi 4 / 5 with a 7" touchscreen (800 × 480).

```bash
dotnet publish src/BrewAlert.UI -c Release -r linux-arm64 --self-contained
```

The Pi runs the published output as a systemd service for auto-start on boot (service unit lives on the Pi, not in this repo).
