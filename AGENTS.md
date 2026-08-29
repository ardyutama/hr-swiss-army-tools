# AGENTS.md

## Agent skills

### .NET architecture and coding

For ASP.NET Core, EF Core, backend testing, dependency injection, domain modeling, or
performance work, read `docs/agents/dotnet.md` after `CONTEXT.md` and any relevant ADRs.
Use `.agents/skills/dotnet-skills/` as conditional playbooks for the matching task; the
repository-specific rules in `docs/agents/dotnet.md` take precedence when they differ.

### Issue tracker

Work items derive from `docs/discovery/03-user-stories.md` and live as local markdown under `.scratch/<feature>/`. See `docs/agents/issue-tracker.md`.

### Triage labels

Default canonical labels (`needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`). See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: one `CONTEXT.md` + `docs/adr/` at the repo root (created lazily). See `docs/agents/domain.md`.
