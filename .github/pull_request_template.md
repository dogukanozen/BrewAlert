<!--
Thanks for the PR! GitHub auto-fills this template when you open a PR.
Keep it tight — reviewers (human or agent) skim this first.
-->

## Summary

<!-- 1–3 bullets. The *why*, not a diff recap. -->
-

## Scope

<!-- Tick all that apply. -->
- [ ] Core (`src/BrewAlert.Core`)
- [ ] Infrastructure (`src/BrewAlert.Infrastructure`)
- [ ] UI (`src/BrewAlert.UI`)
- [ ] Tests (`tests/**`)
- [ ] CI (`.github/**`)
- [ ] Docs (`*.md`, `docs/**`)

## Invariants respected

<!-- See AGENT.md §4. Tick only after verifying. -->
- [ ] No service locator added (ViewModels don't call `App.Services` / `GetRequiredService`)
- [ ] Events fired outside `lock` blocks (if `BrewTimerService` touched)
- [ ] Event subscriptions disposed (if a VM subscribes to `IBrewTimerService`)
- [ ] DI lifetimes unchanged (Singleton/Transient per AGENT.md §4.4)
- [ ] Dependency direction preserved (UI → Infrastructure → Core)
- [ ] No secrets in diff
- [ ] No new CI artifact uploads (free-tier quota)

## Commit style

- [ ] Every commit follows [`docs/commit-style.md`](../docs/commit-style.md) (`<type>(<scope>): <subject>`, lowercase, imperative, ≤ 72 chars)

## Test plan

- [ ] `dotnet build --configuration Release`
- [ ] `dotnet test  --configuration Release`
- [ ] Manual smoke (UI changes only): <what you did>

## Linked issues

<!-- Optional. e.g. Closes #12, Refs #7 -->
