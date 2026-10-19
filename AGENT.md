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

BrewAlert is a .NET 10 + Avalonia MVVM brew timer that sends a Microsoft Teams Adaptive Card when the brew completes. Runs on Windows x64 and Raspberry Pi linux-arm64. Three layers: **Core** (pure domain, zero deps) ← **Infrastructure** (Teams, JSON profiles, SQLite brew history) ← **UI** (Avalonia + `CommunityToolkit.Mvvm`). Dependency direction is one-way; never reverse it. The recommended notification path is **Power Automate webhook** (`TeamsWebhookNotifier`); Graph API (`TeamsGraphNotifier`) is available but requires RSC on the target chat.

## 3. Request → files (low-token lookup)

| Request keyword | Open only these |
|---|---|
| timer, pause, resume, tick, elapsed, complete, concurrent brew | `src/BrewAlert.Core/Services/BrewTimerService.cs`, `Interfaces/IBrewTimerService.cs`, `Events/*`, `src/BrewAlert.UI/ViewModels/ActiveBrewsViewModel.cs`, `BrewItemViewModel.cs`, `Views/ActiveBrewsView.axaml` |
| profile, brew type, defaults | `src/BrewAlert.Core/Services/BrewProfileService.cs`, `Models/BrewProfile.cs`, `Models/BrewType.cs` |
| session state | `src/BrewAlert.Core/Models/BrewSession.cs` |
| ambient, idle screen, screensaver, DVD bounce, orbit mode, animation mode, freshness, bayat, stale, cooling | `src/BrewAlert.Core/Services/BrewFreshnessEvaluator.cs`, `Models/BrewFreshnessLevel.cs`, `src/BrewAlert.UI/ViewModels/AmbientOverlayViewModel.cs`, `AmbientSceneItem.cs`, `AmbientMode.cs`, `AmbientPalette.cs`, `Views/AmbientOverlayView.axaml(.cs)`, `Converters/Ambient*.cs` |
| load/save profile, JSON file | `src/BrewAlert.Infrastructure/Persistence/JsonProfileRepository.cs` |
| history, recent brews, SQLite, history.db | `src/BrewAlert.Core/Interfaces/IBrewHistoryService.cs`, `IBrewHistoryRepository.cs`, `Models/BrewHistoryEntry.cs`, `src/BrewAlert.Infrastructure/Persistence/BrewHistoryService.cs`, `SqliteBrewHistoryRepository.cs`, `src/BrewAlert.UI/ViewModels/ProfileListViewModel.cs`, `RecentBrewItem.cs` |
| completion notification, duplicate notification | `src/BrewAlert.UI/Services/IBrewCompletionNotificationService.cs`, `BrewCompletionNotificationService.cs` |
| update, velopack, update toast | `src/BrewAlert.UI/Services/IUpdateService.cs`, `UpdateService.cs`, `ViewModels/MainWindowViewModel.cs`, `Program.cs` |
| network, IP address, IPv4, SSID, Wi-Fi name, ssh | `src/BrewAlert.UI/Services/INetworkInfoService.cs`, `NetworkInfoService.cs`, `ViewModels/NetworkSettingsViewModel.cs`, `Views/SettingsView.axaml` |
| retry, notification failed, transient | `src/BrewAlert.UI/Services/BrewCompletionNotificationService.cs`, `src/BrewAlert.Infrastructure/Notifications/HttpFailure.cs`, `src/BrewAlert.Core/Models/NotificationResult.cs` |
| teams, webhook, adaptive card, power automate | `src/BrewAlert.Infrastructure/Notifications/TeamsWebhookNotifier.cs`, `TeamsMessageBuilder.cs`, `Configuration/TeamsNotificationOptions.cs` |
| teams graph api, graph notifier | `src/BrewAlert.Infrastructure/Notifications/TeamsGraphNotifier.cs`, `TeamsGraphMessageBuilder.cs`, `Configuration/TeamsGraphOptions.cs` |
| notification routing, provider selection | `src/BrewAlert.Infrastructure/Notifications/RoutingNotificationService.cs`, `Configuration/NotificationProviderOptions.cs` |
| console notifier / fallback | `src/BrewAlert.Infrastructure/Notifications/ConsoleNotifier.cs` |
| language, localization, TR, EN, Turkish, English | `src/BrewAlert.UI/Services/ILocalizationService.cs`, `LocalizationService.cs`, `AppLanguage.cs`, `src/BrewAlert.Infrastructure/Configuration/LanguageOptions.cs` |
| preferences, provider setting, settings save | `src/BrewAlert.UI/Services/IPreferencesService.cs`, `PreferencesService.cs`, `Constants/BrewAlertPaths.cs` |
| view, xaml, style, theme | `src/BrewAlert.UI/Views/`, `src/BrewAlert.UI/Themes/` |
| appearance, weekly theme, Obsidian, Porcelain, day schedule | `src/BrewAlert.UI/Services/AppearanceOptions.cs`, `IAppearanceService.cs`, `AppearanceService.cs`, `ViewModels/AppearanceSettingsViewModel.cs`, `Views/AppearanceSettingsView.axaml`, `Themes/BrewAlertTheme.axaml`, `Themes/AmbientColors.axaml` |
| viewmodel, binding, command | `src/BrewAlert.UI/ViewModels/` |
| navigation, switch view | `src/BrewAlert.UI/Services/INavigationService.cs`, `NavigationService.cs` |
| DI, startup, configuration | `src/BrewAlert.UI/App.axaml.cs`, `Program.cs`, `appsettings.json` |
| CI, build, release | `.github/workflows/ci.yml` |
| commit message format | `docs/commit-style.md` |
| PR template | `.github/pull_request_template.md` |
| tests | `tests/BrewAlert.Core.Tests/`, `tests/BrewAlert.Infrastructure.Tests/`, `tests/BrewAlert.UI.Tests/` |

If the request maps to nothing here, **ask** the user instead of scanning the whole repo.

## 4. Invariants (do NOT break — each one earned by a past bug)

All of the following are verified against the current code. If you see a change that violates one, reject it in review.

1. **No service locator.** ViewModels never call `App.Services`, `IServiceProvider`, or `GetRequiredService<T>()`. Use `INavigationService` for view transitions and constructor injection for everything else. (see `ActiveBrewsViewModel.cs`, `BrewItemViewModel.cs`, `App.axaml.cs`)
2. **Events fire outside `lock` blocks.** In `BrewTimerService` every `TimerTick` / `BrewStarted` / `BrewCompleted` / `BrewCancelled` invocation happens after the lock is released. Re-entrant handlers used to deadlock. (see the `Fire events OUTSIDE the lock` comment in `BrewTimerService.cs`)
3. **Event subscribers implement `IDisposable`** and unsubscribe in `Dispose()`. Required for any VM or service subscribing to an event on a singleton: `IBrewTimerService` (`TimerTick`, `BrewStarted`, etc.), `ILocalizationService.LanguageChanged`, `IBrewHistoryService.HistoryUpdated`, `IAppearanceService.PropertyChanged`, `IUpdateService.UpdateAvailable`. (see `BrewItemViewModel.cs`, `ActiveBrewsViewModel.cs`, `BrewHistoryService.cs`)
4. **DI lifetimes** (in `App.axaml.cs:ConfigureServices`, table in `docs/architecture.md` §2.4):
   - **Singleton**: every service (timer, profiles, history, notifications, preferences, localization, appearance, navigation, completion notifications, updates, network info, `TimeProvider`) plus `MainWindowViewModel` and `AmbientOverlayViewModel`.
   - **Transient**: `ActiveBrewsViewModel`, `ProfileListViewModel`, `SettingsViewModel` — fresh instance per navigation so state never goes stale. `NavigationService` resolves each one in its own DI scope and disposes the scope on the next navigation; resolving them from the root provider leaks every instance.
5. **Dependency direction** UI → Infrastructure → Core. Never add a reverse reference.
6. **No secrets in repo.** Webhook URL via env var `BREWALERT__BrewAlert__Notifications__Teams__WebhookUrl` (provider via `BREWALERT__BrewAlert__Notifications__Provider=Webhook`) or gitignored `appsettings.Development.json`. `git diff --cached` before committing. `preferences.json` is gitignored.
7. **No new CI artifact uploads** without user approval — repo is on free-tier GitHub. `ci.yml` currently uploads only on `v*` tags.
8. **No direct pushes to `main`.** Everything goes through a feature branch + PR.
9. **Timer time comes from the deadline, not from ticks.** `BrewTimerService` recomputes `Remaining` from a monotonic `TimeProvider` timestamp on every tick; never decrement per tick (`PeriodicTimer` drops missed ticks, so a stall stretched brews). Take `TimeProvider` for anything time-based so tests can use `FakeTimeProvider`.
10. **Ambient animations are gated.** Every `Style.Animations` selector in `AmbientOverlayView.axaml` starts with `Panel.ambient-live :is(Control).ambient-layer[IsVisible=True]`, and the element sits under a panel with the `ambient-layer` class. Avalonia keeps infinite style animations running on hidden controls, so an ungated one runs forever on the Pi after the first idle screen.

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
  -r linux-arm64 --self-contained               # Raspberry Pi build (CI handles release packaging)
```

## 8. When the user gives you a change request

1. Classify → layer (§3).
2. Open only the routed files.
3. Minimal edit; respect every invariant in §4.
4. Add or update a test in the matching test project.
5. Commit on a feature branch using [`docs/commit-style.md`](docs/commit-style.md).
6. Open a PR — GitHub auto-loads the template. Fill and stop.
