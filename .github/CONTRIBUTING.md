# Contributing to BrewAlert

Thank you for your interest in contributing! This document provides guidelines for both human and AI agent contributors.

## Architecture Overview

BrewAlert uses a **3-layer architecture** designed for parallel development:

| Layer | Project | Responsibility |
|-------|---------|----------------|
| **Core** | `BrewAlert.Core` | Models, interfaces, business logic. Zero external dependencies. |
| **Infrastructure** | `BrewAlert.Infrastructure` | Teams webhook, JSON persistence, hardware integration. |
| **UI** | `BrewAlert.UI` | Avalonia views, view models, navigation service, DI composition root. |

### Dependency Rule
```
UI → Infrastructure → Core
     (never the reverse)
```

## Development Setup

```bash
# Clone
git clone https://github.com/dogukanozen/BrewAlert.git
cd BrewAlert

# Build
dotnet build

# Test
dotnet test

# Run
dotnet run --project src/BrewAlert.UI
```

## Branch Strategy

```
main        ← production-ready, protected
├── develop ← integration branch
│   ├── feat/core-*      ← Core layer changes
│   ├── feat/infra-*     ← Infrastructure changes
│   ├── feat/ui-*        ← UI changes
│   └── fix/*            ← Bug fixes
```

## File Ownership (for AI Agents)

To minimize merge conflicts when multiple agents work in parallel:

| Task Area | Work In | Do NOT Touch |
|-----------|---------|--------------|
| Timer logic | `src/BrewAlert.Core/Services/` | UI, Infrastructure |
| Notifications | `src/BrewAlert.Infrastructure/Notifications/` | UI, Core services |
| Persistence | `src/BrewAlert.Infrastructure/Persistence/` | UI, Core services |
| UI/UX | `src/BrewAlert.UI/Views/`, `ViewModels/` | Core, Infrastructure |
| Navigation | `src/BrewAlert.UI/Services/` | Core, Infrastructure |
| Models | `src/BrewAlert.Core/Models/` | ⚠️ Careful — affects all layers |
| Tests | `tests/` | Source code (read-only reference) |

## Commit Convention

Use [Conventional Commits](https://www.conventionalcommits.org/):

```
feat(core): add brew timer pause/resume support
fix(infra): handle Teams webhook timeout gracefully
feat(ui): implement circular timer animation
test(core): add edge case tests for BrewTimerService
docs: update architecture diagram
chore: bump Avalonia to 11.4.0
```

## Code Standards

- **C# 13** / **.NET 10** features encouraged
- **Nullable reference types** enabled — no `null` without explicit `?`
- **File-scoped namespaces** preferred
- **Primary constructors** for DI injection
- All public APIs must have XML doc comments
- All services accessed via **interfaces** (defined in `BrewAlert.Core/Interfaces/`)

## Architectural Rules

> ⚠️ **These rules exist to prevent bugs we've already fixed. Do NOT violate them.**

1. **No Service Locator** — ViewModels must **never** call `App.Services`, `IServiceProvider`, or `GetRequiredService<T>()` directly. Use `INavigationService` for view transitions and constructor injection for everything else.
2. **Events outside locks** — In `BrewTimerService`, always fire events (`TimerTick`, `BrewCompleted`, etc.) **outside** `lock` blocks to prevent deadlocks.
3. **Dispose event subscriptions** — Any ViewModel that subscribes to `IBrewTimerService` events must implement `IDisposable` and unsubscribe in `Dispose()`.
4. **MainWindowViewModel is Singleton** — Never change its DI lifetime to Transient. It must be the same instance attached to the Window.
5. **Child ViewModels are Transient** — `BrewTimerViewModel`, `ProfileListViewModel`, `SettingsViewModel` get fresh instances per navigation to avoid stale state.

## Security Rules

> ⚠️ **This is a public repository. NEVER commit secrets.**

- Webhook URLs → environment variables or `appsettings.Development.json` (gitignored)
- Use `BREWALERT__` prefix for all environment variables
- Run `git diff --cached` before committing to verify no secrets leak

## Testing

- Write tests for all Core and Infrastructure services
- Use **xUnit** + **NSubstitute** for mocking
- Tests must pass before merging: `dotnet test --configuration Release`
