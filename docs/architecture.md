# BrewAlert Architecture

## Overview

BrewAlert is a brew timer & notification application built with a clean 3-layer architecture.

## Layer Diagram

```
┌─────────────────────────────────────────────────┐
│                  BrewAlert.UI                    │
│    Views │ ViewModels │ Services │ Themes        │
│            Composition Root (DI)                 │
├─────────────────────────────────────────────────┤
│             BrewAlert.Infrastructure             │
│    Teams Webhook │ JSON Persistence │ Hardware   │
├─────────────────────────────────────────────────┤
│                 BrewAlert.Core                   │
│      Models │ Interfaces │ Services │ Events     │
│              (Zero Dependencies)                 │
└─────────────────────────────────────────────────┘
```

## Key Design Decisions

### 1. Interface-Based Services
All services are accessed through interfaces defined in `BrewAlert.Core/Interfaces/`.
This allows:
- Easy mocking in tests
- Swapping implementations (e.g., Teams → Slack)
- Parallel development by multiple agents

### 2. NavigationService Pattern
ViewModels **never** access the DI container directly (no Service Locator anti-pattern).
Instead, an `INavigationService` abstraction handles all view transitions:

```
ProfileListVM ──→ INavigationService.NavigateTo<BrewTimerVM>()
                         │
                  NavigationService resolves VM from DI
                         │
                  Fires CurrentViewChanged event
                         │
                  MainWindowVM updates CurrentView property
                         │
                  ContentControl renders the new view
```

**Why?**
- All ViewModel dependencies are visible in the constructor
- ViewModels are independently unit-testable (mock `INavigationService`)
- No hidden coupling to `App.Services` or `IServiceProvider`

### 3. Event-Driven Timer
`IBrewTimerService` uses C# events (`TimerTick`, `BrewCompleted`) rather than polling.
The UI subscribes to these events and marshals updates to the Avalonia UI thread.

**Thread Safety Rules:**
- State mutations happen inside `lock` blocks
- Events are fired **outside** lock blocks to prevent deadlocks from re-entrant handlers
- `BrewTimerViewModel` implements `IDisposable` to unsubscribe from events and prevent memory leaks

### 4. DI Lifetime Strategy

| Registration | Lifetime | Reason |
|---|---|---|
| `MainWindowViewModel` | **Singleton** | Lives as long as the window — one instance for the entire app lifecycle |
| `INavigationService` | **Singleton** | Shared navigation state across all ViewModels |
| `IBrewTimerService` | **Singleton** | Single timer instance manages the active brew session |
| `BrewTimerViewModel` | **Transient** | Fresh instance per navigation — prevents stale state |
| `ProfileListViewModel` | **Transient** | Re-fetches profiles on each visit |
| `SettingsViewModel` | **Transient** | Re-reads config on each visit |

### 5. Configuration Hierarchy
```
appsettings.json (committed, defaults only)
  ↓ overridden by
appsettings.{Environment}.json (gitignored)
  ↓ overridden by
Environment variables (BREWALERT__*)
```

### 6. Security
- No secrets in source code
- Webhook URLs via environment variables
- `appsettings.Development.json` in `.gitignore`

## Data Flow

```
User taps profile card
  │
  ▼
ProfileListVM.SelectProfile()
  │
  ▼
INavigationService.NavigateTo<BrewTimerVM>()  →  MainWindowVM.CurrentView updates
  │
  ▼
BrewTimerVM.StartBrew(profile)
  │
  ▼
IBrewTimerService.Start(profile)  →  BrewStarted event
  │
  ▼
Timer loop (PeriodicTimer, 1s interval)
  │
  ├─ TimerTick event  →  BrewTimerVM (UI thread)  →  countdown display
  │
  └─ BrewCompleted event  →  BrewTimerVM  →  INotificationService.SendBrewCompletedAsync()
                                                      │
                                                      ▼
                                                HTTP POST → Teams Webhook (Adaptive Card)
```

## Raspberry Pi Deployment

Target: Raspberry Pi 4/5 with 7" touchscreen (800x480)

```bash
dotnet publish src/BrewAlert.UI -c Release -r linux-arm64 --self-contained
```

The app runs as a systemd service for auto-start on boot.
