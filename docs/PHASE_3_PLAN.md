# Phase 3 Plan

Phase 3 starts real remote operations and diagnostics. It must begin only after Phase 2 is committed and `docs/CURRENT_PHASE.md` is explicitly updated to Phase 3.

## Goals

Phase 3 should implement the first real remote MVP workflow:

1. Test SSH connectivity for a saved Linux node.
2. Download or prepare an FRP release locally.
3. Upload FRP binaries and TOML configuration through SFTP.
4. Start, stop, restart, and inspect remote FRP processes.
5. Read recent remote logs.
6. Persist runtime and deployment outcomes locally.

## Non-Negotiable Boundaries

- Do not store SSH passwords, tokens, or private key contents in SQLite.
- Do not log SSH passwords, tokens, private key contents, or full sensitive command strings.
- Do not implement remote commands in Avalonia views or code-behind.
- Do not bypass Application interfaces from Desktop view models.
- Do not use real remote integration tests unless the dependency is controlled and explicitly configured.

## Recommended Sequence

### Phase 3 First Iteration: Credential Strategy And SSH Test

- Follow `docs/CREDENTIAL_STRATEGY.md` before implementing real SSH.
- Decide which credential sources are allowed:
  - Private key file path.
  - SSH agent when available.
  - Interactive password entry for one session only.
- Keep credential material out of SQLite.
- Update or replace `ISshConnectionService` if `nodeName` alone is insufficient.
- Implement SSH connection testing in Infrastructure.
- Wire Nodes page `测试连接` to a ViewModel command.
- Persist only safe result metadata, such as connection status and timestamp.
- Add unit tests with fake SSH adapters; avoid real servers in normal tests.

### Phase 3 Second Iteration: FRP Release Preparation

- Implement `IFrpReleaseService` using a safe local cache directory.
- Support version listing and local binary preparation.
- Keep network download errors user-friendly and logged without secrets.
- Verify platform/runtime selection for `frpc` and `frps`.

### Phase 3 Third Iteration: SFTP Upload

- Implement `IRemoteFileTransferService` for binary and TOML upload.
- Reuse the credential strategy from SSH.
- Validate remote paths before upload.
- Record deployment step outcomes in local `deployment_records`.

### Phase 3 Fourth Iteration: Remote Runtime Control

- Implement `IRemoteRuntimeService`.
- Support start, stop, restart, and process status read.
- Keep command templates explicit and testable.
- Persist safe runtime snapshots in `runtime_processes`.

### Phase 3 Fifth Iteration: Remote Logs

- Implement `IRemoteLogService`.
- Read recent logs first; streaming can come later if needed.
- Keep Serilog responsible for local app logs.
- Do not write raw remote logs into SQLite by default.

## Testing Strategy

- Use fake SSH/SFTP/runtime adapters for unit tests.
- Keep normal test runs offline and deterministic.
- Add integration tests only behind explicit configuration, such as environment variables.
- Test cancellation behavior for remote operations.
- Test Chinese UI error messages in ViewModels.
- Test that sensitive fields are not persisted or logged.

## UI Wiring Strategy

- Nodes page:
  - `测试连接` becomes the first real remote action.
  - Keep `上传核心` disabled or placeholder until SFTP is ready.
- Configurations page:
  - `上传配置` stays disabled or placeholder until SFTP is ready.
- Runtime page:
  - `启动`, `停止`, `重启`, and remote `刷新` stay placeholder until runtime control is ready.
- Logs page:
  - Remote log read stays placeholder until `IRemoteLogService` is ready.

## Phase 3 Entry Gate

Before implementation:

- Phase 2 changes are committed.
- `docs/CURRENT_PHASE.md` is updated to Phase 3.
- `docs/PROJECT_TODO.md` has Phase 3 first-iteration Todo entries.
- `docs/CREDENTIAL_STRATEGY.md` is accepted by the user.
