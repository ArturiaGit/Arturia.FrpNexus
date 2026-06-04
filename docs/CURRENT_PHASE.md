# Current Phase Constraints

## Active Phase

Current phase: `Phase 3: Remote Operations And Diagnostics`

## Allowed In This Phase

Allowed work:

- Credential-safe SSH connection testing.
- Session-only credential input for passwords and private key passphrases.
- SFTP upload through `Application` interfaces and `Infrastructure` implementations.
- FRP release download and local binary preparation.
- Remote start, stop, restart, and process status checks.
- Remote log reading and diagnostics.
- Persisting only safe runtime, deployment, and connection result metadata locally.
- Unit tests with fake remote adapters; normal tests must remain offline and deterministic.
- Opt-in integration tests only when explicitly configured.
- Documentation and Todo updates for Phase 3.

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

- Persistent storage of SSH passwords, tokens, private key contents, or private key passphrases.
- Logging of SSH passwords, tokens, private key contents, private key passphrases, or secret-bearing command lines.
- Real SFTP upload before SSH credential strategy and SSH test are in place.
- Real FRP release download before SSH test is stable.
- Real remote start, stop, or restart before SFTP and deployment records are ready.
- Real remote log streaming before remote runtime control is stable.
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

## Phase 3 Completion Direction

Phase 3 should move toward a complete remote FRP operations workflow:

- SSH connection testing works through `ISshConnectionService`.
- SFTP can upload FRP binaries and TOML configuration.
- FRP release preparation can select a suitable local binary.
- Runtime controls can start, stop, restart, and inspect remote FRP processes.
- Logs can be read remotely with safe diagnostics.
- Sensitive credentials never enter SQLite, logs, screenshots, or test snapshots.
- `dotnet build` and full `dotnet test` pass.
