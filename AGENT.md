# AGENT.md — Shared brief for every AI agent

Tool-agnostic entry point. Whether you are Claude, Gemini, Codex, Copilot, Cursor, or anything else, **read this file first**. The per-tool files (`CLAUDE.md`, `.gemini/GEMINI.md`, …) are thin pointers back here; this is the single source of truth.

> **Rule #1: do not re-discover the repo.** Route the request via §3, open only the listed files, answer. No speculative reads, no whole-repo greps, no drive-by refactors.

---

## 1. Read order

1. **This file.**
2. [`docs/commit-style.md`](docs/commit-style.md) — mandatory commit format.
3. Only if the task is architectural: [`docs/architecture.md`](docs/architecture.md).
4. Only if opening a PR: [`.github/pull_request_template.md`](.github/pull_request_template.md) auto-loads — just fill it.

Everything else is opened on-demand from §3.

## 2. Project in one paragraph

BrewAlert is a .NET 10 + Avalonia MVVM brew timer that sends a Microsoft Teams webhook when the brew completes. Runs on Windows x64 and Raspberry Pi linux-arm64. Three layers: **Core** (pure domain, zero deps) ← **Infrastructure** (Teams, JSON) ← **UI** (Avalonia + `CommunityToolkit.Mvvm`). Dependency direction is one-way; never reverse it.

## 3. Request → files (low-token lookup)

| Request keyword | Open only these |
|---|---|
| timer, pause, resume, tick, elapsed, complete | `src/BrewAlert.Core/Services/BrewTimerService.cs`, `Interfaces/IBrewTimerService.cs`, `Events/*` |
| profile, brew type, defaults | `src/BrewAlert.Core/Services/BrewProfileService.cs`, `Models/BrewProfile.cs`, `Models/BrewType.cs` |
| session state | `src/BrewAlert.Core/Models/BrewSession.cs` |
| load/save profile, JSON file | `src/BrewAlert.Infrastructure/Persistence/JsonProfileRepository.cs` |
| teams, webhook, adaptive card | `src/BrewAlert.Infrastructure/Notifications/TeamsWebhookNotifier.cs`, `TeamsMessageBuilder.cs`, `Configuration/TeamsNotificationOptions.cs` |
| console notifier / fallback | `src/BrewAlert.Infrastructure/Notifications/ConsoleNotifier.cs` |
| view, xaml, style, theme | `src/BrewAlert.UI/Views/`, `src/BrewAlert.UI/Themes/` |
| viewmodel, binding, command | `src/BrewAlert.UI/ViewModels/` |
| navigation, switch view | `src/BrewAlert.UI/Services/INavigationService.cs`, `NavigationService.cs` |
| DI, startup, configuration | `src/BrewAlert.UI/App.axaml.cs`, `Program.cs`, `appsettings.json` |
| CI, build, release | `.github/workflows/ci.yml` |
| commit message format | `docs/commit-style.md` |
| PR template | `.github/pull_request_template.md` |
| tests | `tests/BrewAlert.Core.Tests/`, `tests/BrewAlert.Infrastructure.Tests/` |

If the request maps to nothing here, **ask** the user instead of scanning the whole repo.

## 4. Invariants (do NOT break — each one earned by a past bug)

All of the following are verified against the current code. If you see a change that violates one, reject it in review.

1. **No service locator.** ViewModels never call `App.Services`, `IServiceProvider`, or `GetRequiredService<T>()`. Use `INavigationService` for view transitions and constructor injection for everything else. (see `BrewTimerViewModel.cs`, `App.axaml.cs`)
2. **Events fire outside `lock` blocks.** In `BrewTimerService` every `TimerTick` / `BrewStarted` / `BrewCompleted` / `BrewCancelled` invocation happens after the lock is released. Re-entrant handlers used to deadlock. (see `BrewTimerService.cs:53, 73, 143`)
3. **Timer subscribers implement `IDisposable`** and unsubscribe in `Dispose()`. Required for any VM holding `IBrewTimerService` events. (see `BrewTimerViewModel.cs:130`)
4. **DI lifetimes** (in `App.axaml.cs:ConfigureServices`):
   - **Singleton**: `MainWindowViewModel`, `INavigationService`, `IBrewTimerService`, `BrewProfileService`, `IProfileRepository`, `INotificationService`.
   - **Transient**: `BrewTimerViewModel`, `ProfileListViewModel`, `SettingsViewModel` — fresh instance per navigation so state never goes stale.
5. **Dependency direction** UI → Infrastructure → Core. Never add a reverse reference.
6. **No secrets in repo.** Webhook URL via env var `BREWALERT__NOTIFICATIONS__TEAMS__WEBHOOKURL` or gitignored `appsettings.Development.json`. `git diff --cached` before committing.
7. **No new CI artifact uploads** without user approval — repo is on free-tier GitHub. `ci.yml` currently uploads only on `v*` tags.
8. **No direct pushes to `main`.** Everything goes through a feature branch + PR.

## 5. Working discipline

- One feature branch per concern. Branch names: `feat/core-*`, `feat/infra-*`, `feat/ui-*`, `fix/*`, `chore/*`, `test/*`, `docs/*`, `ci/*`, `refactor/*`.
- Run locally before pushing:
  ```bash
  dotnet build --configuration Release
  dotnet test  --configuration Release
  ```
- **Every commit MUST follow** [`docs/commit-style.md`](docs/commit-style.md). No `minor.` / `Update stuff` / `wip`.
- GitHub auto-loads [`.github/pull_request_template.md`](.github/pull_request_template.md) — just fill it.
- **Never** force-push `main`, **never** amend a pushed commit without user OK, **never** use `--no-verify`.

## 6. Token discipline (how to stay cheap)

1. Route via §3, then open only the listed files.
2. Use symbol search (`Grep`) before reading a whole file.
3. Skip `bin/`, `obj/`, generated `.g.cs`, and `docs/architecture.md` unless the task is architectural.
4. One PR = one change. No drive-by refactors, no speculative features, no unsolicited documentation.
5. Reply terse. Prefer `file:line` references over prose.
6. If you have already read this file in the current session, **do not re-read it**.

## 7. Common commands

```bash
dotnet run --project src/BrewAlert.UI           # run (Windows / Linux desktop)
dotnet test                                     # all tests
dotnet publish src/BrewAlert.UI -c Release \
  -r linux-arm64 --self-contained               # Raspberry Pi build
```

## 8. When the user gives you a change request

1. Classify → layer (§3).
2. Open only the routed files.
3. Minimal edit; respect every invariant in §4.
4. Add or update a test in the matching test project.
5. Commit on a feature branch using [`docs/commit-style.md`](docs/commit-style.md).
6. Open a PR — GitHub auto-loads the template. Fill and stop.
