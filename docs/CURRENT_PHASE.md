# Current Phase Constraints

## Active Phase

Current phase: `Phase 4: Packaging, Reliability, And Product Polish`

## Allowed In This Phase

Allowed work:

- Release packaging for Windows desktop builds.
- Version metadata, release notes, and manual upgrade documentation.
- Reliability improvements for existing local persistence and remote operation workflows.
- User-facing error recovery, cancellation, timeout, and retry-state polish.
- Import, export, and backup workflows for safe local data.
- Accessibility and keyboard navigation improvements.
- Optional theme evaluation after the default WinUI 3 / Fluent style remains stable.
- Documentation and Todo updates for Phase 4.

## Required MVP Direction

The implementation should prepare FrpNexus for a usable preview release:

- A Windows x64 release package can be produced consistently.
- Local SQLite data stays outside the release package.
- User logs, credentials, private keys, tokens, and FRP caches stay outside the release package.
- Existing SSH, SFTP, FRP release, runtime, TOML, log, and SQLite flows remain buildable and testable.
- UI refinements improve day-to-day use without changing the product into a web dashboard or enterprise platform.

## Not Allowed In This Phase

Do not implement:

- Agent mode.
- Agent API.
- Cloud sync.
- Team permission management.
- Web Dashboard.
- Billing.
- Kubernetes integration.
- Multi-tenant backend.
- Complex traffic analytics.
- Advanced audit logs.
- Automatic update installation without a separate explicit design.
- Installer/MSIX packaging before the folder-based preview package is stable.
- Legacy INI as the MVP default configuration format.
- Persistent storage of SSH passwords, tokens, private key contents, or private key passphrases in ordinary SQLite models, logs, exports, backups, or plain-text configuration.
- SSH session passwords may only be remembered when the user explicitly opts in, and only through the approved DPAPI `CurrentUser` credential secret service. The protected secret itself must stay out of local export and backup snapshots.
- Logging of SSH passwords, tokens, private key contents, private key passphrases, or secret-bearing command lines.

## Phase Rule

If a requested change conflicts with this file, Codex must state that it is outside the current phase before proposing or implementing it.

## Phase 4 Completion Direction

Phase 4 should move toward a preview-ready desktop tool:

- Windows x64 release packaging works through a documented command.
- Release outputs are easy to inspect and do not include user-local data.
- Existing full build and test verification remains green.
- Main workflows provide clearer failure states and recovery options.
- Import/export, backup, keyboard navigation, accessibility, and optional theme work are planned and completed incrementally.
