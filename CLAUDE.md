# CLAUDE.md

**Read [`AGENT.md`](AGENT.md) first.** It is the single source of truth for every agent (Claude, Gemini, Codex, …). This file exists only so Claude Code picks up the brief via its default entry point.

Commit messages **MUST** follow [`docs/commit-style.md`](docs/commit-style.md). No `minor.` / `Update stuff` / bare `wip` commits.

Claude-specific notes (only when they differ from `AGENT.md`):

- When asked to review, use `/review`; for security-sensitive diffs, `/security-review`.
- Do not spawn sub-agents for single-file tasks — route via `AGENT.md` §3 and answer inline.
- Do not re-read `AGENT.md` if you have already read it in this session.
