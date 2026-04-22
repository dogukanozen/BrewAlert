# Commit Style — BrewAlert

All commits **MUST** follow this format. No exceptions. This applies to human and agent contributors alike. Anything else gets rejected in review.

## 1. Format

```
<type>(<scope>): <subject>

<body>          # optional, blank line above, wrap at ~72 cols
<footer>        # optional, blank line above
```

- **Lowercase** type and scope.
- Subject ≤ 72 chars, **imperative mood** ("add", not "added" / "adds"), **no trailing period**.
- One logical change per commit. If you need "and" in the subject, split it.

## 2. Allowed `<type>` values

| type | Use for |
|---|---|
| `feat` | A new user-visible capability |
| `fix` | A bug fix |
| `refactor` | Internal change, no behaviour change, no new feature |
| `perf` | A change whose sole purpose is performance |
| `test` | Add/adjust tests only |
| `docs` | Documentation only (README, `docs/`, XML comments) |
| `chore` | Build tooling, dependency bumps, non-code housekeeping |
| `ci` | `.github/workflows/*` or other CI config |
| `style` | Formatting / whitespace / renames with no logic change |
| `revert` | Revert a previous commit (subject = reverted subject) |

No other types. Do not invent `update`, `misc`, `minor`, `wip`.

## 3. Allowed `<scope>` values

Pick the **narrowest** one that fits.

| scope | Maps to |
|---|---|
| `core` | `src/BrewAlert.Core/**` |
| `infra` | `src/BrewAlert.Infrastructure/**` |
| `ui` | `src/BrewAlert.UI/**` |
| `tests` | `tests/**` (only when the commit is tests-only; otherwise use the code scope) |
| `ci` | `.github/workflows/**` (pair with `type: ci`) |
| `docs` | `docs/**`, `README.md`, `AGENT.md`, `CLAUDE.md`, `.gemini/**`, `.github/pull_request_template.md` |
| `deps` | Dependency / SDK bumps (pair with `type: chore`) |
| `repo` | Top-level repo config: `.gitignore`, `.gitattributes`, `.editorconfig`, `*.slnx` |

If a commit spans multiple scopes, you are almost certainly doing two things — split the commit. Real cross-cutting changes (rare) may omit the scope: `refactor: rename IBrewTimerService.Tick to OnTick`.

## 4. Breaking changes

Add `!` after the scope **and** a `BREAKING CHANGE:` footer explaining the migration.

```
feat(core)!: rename IBrewTimerService.Tick to OnTick

BREAKING CHANGE: subscribers must rename Tick -> OnTick.
```

## 5. Body and footer

- **Body**: the *why*, not the *what*. The diff already shows the *what*. Skip the body if the subject is self-explanatory.
- **Footers**:
  - `Refs: #<issue>` or `Closes: #<issue>`
  - `Co-Authored-By: Name <email>`
  - `BREAKING CHANGE: <migration note>`

## 6. Examples (good)

```
feat(core): add pause/resume to BrewTimerService
fix(infra): retry Teams webhook on transient 5xx
refactor(ui): extract ProfileCard into its own view
test(core): cover BrewTimerService re-entrancy
chore(deps): bump Avalonia to 11.4.0
ci: drop test-results artifact upload
docs: add request->files routing table
perf(core): avoid allocation in tick hot path
```

## 7. Examples (reject)

| Bad | Why |
|---|---|
| `minor.` | No type, no subject. |
| `Update stuff` | No type, vague, capitalised, no scope. |
| `feat: stuff and things` | No scope, multi-purpose. |
| `feat(core): Added timer.` | Past tense, trailing period, capitalised. |
| `feat(Core): add timer` | Scope must be lowercase. |
| `feat(core): add pause and fix webhook retry` | Two changes — split. |
| `wip` | Not a type. Rebase or squash before pushing. |

## 8. Quick self-check before `git commit`

- [ ] Type is from §2.
- [ ] Scope is from §3 (or intentionally omitted for a real cross-cutting change).
- [ ] Subject is imperative, lowercase, ≤ 72 chars, no period.
- [ ] The commit does **one** thing.
- [ ] Body explains *why* if the reason is non-obvious.
- [ ] Breaking change? `!` + `BREAKING CHANGE:` footer.
