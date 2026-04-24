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
| `INotificationService` | Singleton | Teams (Graph or Webhook) or Console |
| `BrewTimerViewModel` | Transient | Fresh instance per navigation (prevents stale state) |
| `ProfileListViewModel` | Transient | Re-fetches profiles on each visit |
| `SettingsViewModel` | Transient | Re-reads config on each visit |

### 2.5 Notifier selection
`INotificationService` is resolved via a factory pattern in `App.axaml.cs`. Selection priority:
1. **Teams Graph API** (`TeamsGraphNotifier`): Selected if `BrewAlert:Notifications:TeamsGraph:Enabled=true` and all OAuth credentials (Tenant/Client/Secret) + ChatId are provided. Requires `Chat.ReadWrite.All` application permission **and** Resource-Specific Consent (RSC) on the target chat; without RSC the API returns 403.
2. **Teams Webhook** (`TeamsWebhookNotifier`): Selected if Graph is disabled/unconfigured and `BrewAlert:Notifications:Teams:Enabled=true` + `WebhookUrl` is non-empty. Sends an Adaptive Card JSON body to a Power Automate HTTP trigger; the flow posts it via a "Post a card in a chat or channel" action. **Recommended path** — no Azure AD app registration or RSC required.
3. **Console** (`ConsoleNotifier`): Fallback if no remote notifications are configured.

### 2.6 Configuration precedence

```
appsettings.json                     (committed, defaults only)
  ↓ overridden by
appsettings.{DOTNET_ENVIRONMENT}.json  (optional, gitignored in practice)
  ↓ overridden by
Environment variables prefixed BREWALERT__
```

Note: Environment variables map to the configuration hierarchy. For example, `BREWALERT__BrewAlert__Notifications__Teams__Enabled` maps to the Teams notification toggle.

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
                                                   [Teams Graph | Teams Webhook | Console]
                                                   (Notification sent to chosen provider)
```

## 4. Raspberry Pi deployment

Target: Raspberry Pi 4 / 5 with a 7" touchscreen (800 × 480).

```bash
dotnet publish src/BrewAlert.UI -c Release -r linux-arm64 --self-contained
```

The Pi runs the published output as a systemd service for auto-start on boot (service unit lives on the Pi, not in this repo).
