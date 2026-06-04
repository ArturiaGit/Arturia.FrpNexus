# Current Phase Constraints

## Active Phase

Current phase: `Phase 2: SQLite Local Persistence + Settings Persistence`

## Allowed In This Phase

Allowed work:

- SQLite local persistence infrastructure.
- Infrastructure project setup.
- Settings persistence through `Application` interfaces and `Infrastructure` implementations.
- Local database path management under `%LocalAppData%/Arturia/FrpNexus/data/`.
- Safe default settings for first run.
- Unit tests for persistence path, initialization, read/write, and DI registration.
- Documentation and Todo updates for Phase 2.

## Required MVP Direction

The implementation should prepare for:

- Linux node management.
- SSH connection testing.
- FRP release download.
- SFTP binary upload.
- TOML configuration generation.
- Remote start, stop, and restart.
- Remote log viewing.
- TCP, UDP, HTTP, and HTTPS tunnels.

## Not Allowed In This Phase

Do not implement:

- Real SSH connection testing.
- Real SFTP upload.
- Real FRP release download.
- Real remote start, stop, or restart.
- Real remote log streaming.
- FrpNexus Agent.
- Agent API.
- Cloud sync.
- Team permission management.
- Web Dashboard.
- Billing.
- Kubernetes integration.
- Multi-tenant backend.
- Complex traffic analytics.
- Advanced audit logs.

## Phase Rule

If a requested change conflicts with this file, Codex must state that it is outside the current phase before proposing or implementing it.
