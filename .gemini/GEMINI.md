# GEMINI.md

**Read [`../AGENT.md`](../AGENT.md) first.** It is the single source of truth for every agent. This file only exists so Gemini picks up the brief via its default entry point.

Commit messages **MUST** follow [`../docs/commit-style.md`](../docs/commit-style.md).

Gemini-specific reminders:
- Framework: .NET 10 + Avalonia MVVM, `CommunityToolkit.Mvvm` (`[ObservableProperty]`).
- C# 13 features OK; nullable reference types enabled; file-scoped namespaces; primary constructors for DI.
- Interfaces are prefixed `I` and live in `src/BrewAlert.Core/Interfaces/`.
- UI styles belong in `src/BrewAlert.UI/Themes/BrewAlertTheme.axaml`.
