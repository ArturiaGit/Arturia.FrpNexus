# Current Phase Constraints

## Active Phase

Current phase: `Phase 1: SSH/SFTP MVP + UI Skeleton Implementation`

## Allowed In This Phase

Allowed work:

- Product and UI constraint documentation.
- Avalonia project scaffolding.
- Main shell layout.
- Chinese navigation.
- WinUI 3 / Fluent-inspired styles.
- Static UI pages based on Stitch designs.
- Placeholder view models and sample data.
- UI state examples for online, offline, running, stopped, warning, and error.
- Interfaces or placeholders for future SSH/SFTP services.

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

