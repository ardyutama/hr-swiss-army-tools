# 01: Project scaffold

**What to build:** An empty but runnable ASP.NET Core API + Vue SPA + database, wired
together and containerised with Docker, with the VSA folder conventions in place
(`Features/` backend slices, `src/features/` frontend folders) and a health-check endpoint
the SPA can call. Nothing user-visible yet.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] API starts, exposes a health endpoint, connects to the database
- [ ] Vue SPA starts and calls the health endpoint successfully
- [ ] Docker setup runs the whole stack with one command
- [ ] Backend VSA (`Features/`) and frontend (`src/features/`, `src/shared/`) folder conventions established
