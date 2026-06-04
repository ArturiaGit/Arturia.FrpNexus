# Credential Strategy

This document defines how FrpNexus handles credentials before Phase 3 implements real SSH, SFTP, release upload, remote runtime control, or remote log reading.

## Goals

- Support remote Linux node workflows without storing secrets unsafely.
- Keep SQLite limited to ordinary metadata.
- Keep logs, tests, screenshots, and sample data free of secrets.
- Make credential handling explicit before real remote services are implemented.

## Allowed Credential References

SQLite may store only non-secret credential references:

- Authentication type, such as `PrivateKey`, `SshAgent`, or `SessionPassword`.
- Private key display name.
- Private key file path, if the user explicitly selects a file path.
- Username, host, and SSH port.
- Last connection status and timestamp.

SQLite must not store:

- SSH passwords.
- Token values.
- Private key contents.
- Private key passphrases.
- Raw credential JSON.
- Full secret-bearing connection strings.

## Recommended Authentication Modes

### Private Key File Path

Use this as the preferred MVP path.

- Store the selected private key path only.
- Do not copy private key contents into SQLite.
- If the key has a passphrase, request it for the current operation only.
- Do not persist the passphrase unless a future explicit secure-storage design is approved.

### SSH Agent

Use when available.

- Store only that the node uses `SshAgent`.
- Do not store agent-provided key material.
- Surface agent failures as Chinese user-facing status text.

### Session Password

Use only as a temporary session input.

- Do not write the password to SQLite.
- Do not write the password to logs.
- Do not keep it in long-lived view model state longer than the operation requires.
- Clear password values after command completion or cancellation.

## UI Rules

- Password and passphrase fields must be masked.
- Private key contents must never be displayed.
- Private key fingerprint may be displayed when derived safely.
- Error messages must not echo raw credentials or full command lines.
- Buttons that execute real remote operations must show clear Chinese status feedback.

## Logging Rules

Serilog may log:

- Node name.
- Host with port.
- Authentication mode.
- Operation name.
- Result status.
- Exception type and sanitized message.

Serilog must not log:

- Passwords.
- Tokens.
- Private key contents.
- Private key passphrases.
- Raw environment variables containing secrets.
- Full command lines when they include secrets.

## Testing Rules

- Unit tests must use fake credentials such as `PRIVATE_KEY_PATH` or `SESSION_PASSWORD_PLACEHOLDER`.
- Tests must not require a real SSH server by default.
- Integration tests against real SSH/SFTP targets must be opt-in through explicit environment variables.
- Test snapshots must not contain real host credentials.
- Sensitive-field boundary tests should verify that credential content fields do not exist on persistence models.

## Implementation Implications

Before real SSH work starts:

- Review `INodeManagementService` and node models to ensure they store references only.
- Add a dedicated credential input path for session-only secrets.
- Keep credential resolution in Infrastructure or a dedicated Application abstraction, not in Avalonia views.
- Ensure cancellation tokens are honored by SSH/SFTP operations.
- Ensure all real remote operations convert technical failures into recoverable Chinese UI states.

## Phase 3 Entry Requirement

Phase 3 SSH implementation may start only after this strategy is accepted and `docs/CURRENT_PHASE.md` is updated to Phase 3.
