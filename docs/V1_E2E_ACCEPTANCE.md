# v1.0 End-to-End Acceptance

This document defines the manual release-candidate gate for FrpNexus v1.0. The goal is to prove that one real remote FRP deployment and runtime-management workflow can be completed from the desktop app.

This is not Phase 5. It is a verification gate after Phase 4 closure.

## Scope

Validate one realistic workflow:

1. Create and persist a Linux node.
2. Test SSH connectivity.
3. Download and select FRP binaries.
4. Upload binaries and TOML configuration through SFTP.
5. Generate and validate TOML from local tunnel data.
6. Start, stop, restart, and inspect the remote FRP process.
7. Read remote logs.
8. Confirm the exposed tunnel reaches a local test service.
9. Confirm ordinary data persists after restarting FrpNexus.
10. Confirm sensitive values are not persisted, logged, or exported.

## Out Of Scope

- Preview release publishing.
- Windows installer, MSIX, automatic update, or single-file packaging.
- Legacy INI as the MVP default.
- Keyboard shortcuts and accessibility-specific polish.
- Agent mode, cloud sync, team permissions, web dashboard, billing, Kubernetes, or multi-tenant backend features.
- Persistent storage of SSH passwords, tokens, private key contents, private key passphrases, or secret-bearing command lines.

## Test Environment

Use a dedicated test environment, not a production server.

- Local machine: Windows development machine running FrpNexus.
- Remote node: one Linux VPS, preferably Ubuntu or Debian.
- Authentication: private key path, SSH agent, or session password. Private key contents and passphrases must remain session-only.
- Local test service: a simple HTTP service, for example `python -m http.server 8080`.
- Remote firewall or cloud security group: allow SSH and the selected FRP test ports.

Suggested minimum ports:

- SSH: `22`.
- Local service: `127.0.0.1:8080`.
- Remote tunnel endpoint: any safe test port such as `18080`.

## Preflight Checks

Before manual testing:

```powershell
dotnet build
dotnet test D:\Arturia\E\PersonalProjects\Arturia.FrpNexus\Arturia.FrpNexus.sln
```

Both commands must pass before the manual gate starts.

Do not run `scripts/publish-win-x64.ps1` during this gate.

## Acceptance Steps

### 1. Node And SSH

- Open FrpNexus.
- Create a node for the test VPS.
- Fill ordinary fields such as node name, host, SSH port, username, authentication type, OS description, FRP version, and remote paths.
- Save the node.
- Restart FrpNexus and confirm the node is still present.
- Run SSH connection testing.

Pass criteria:

- Node data persists after app restart.
- SSH test succeeds.
- SSH failure states, if any, are shown as Chinese user-facing status text.
- No password, token, private key content, or passphrase is stored in SQLite.

### 2. Tunnel Record

- Create one HTTP or TCP tunnel record.
- Use HTTP first when possible:
  - Local endpoint: `127.0.0.1:8080`.
  - Remote endpoint: the selected VPS port or domain.
- Save the tunnel.
- Restart FrpNexus and confirm the tunnel is still present.

Pass criteria:

- Tunnel can be created, edited, deleted, and reloaded from local persistence.
- Protocol, ports, endpoints, and node relation remain correct after restart.

### 3. TOML Configuration

- Open the configuration workflow.
- Generate TOML from the selected node and tunnel.
- Validate the generated TOML.
- Save the configuration version.
- Upload the TOML to the remote node.

Pass criteria:

- Generated TOML matches the selected tunnel and node data.
- Local validation succeeds.
- Saved configuration version can be reloaded.
- Upload succeeds without persisting secrets.

### 4. FRP Binary Preparation

- Download or select the required FRP release.
- Select the correct Linux binary for the test node.
- Upload `frpc` or `frps` as needed for the selected workflow.

Pass criteria:

- Release lookup/download completes.
- Correct binary is selected.
- Binary upload succeeds.
- User-facing failures remain recoverable and in Chinese.

### 5. Remote Runtime

- Start the remote FRP process.
- Refresh process status.
- Stop the process.
- Restart the process.
- Refresh status again.

Pass criteria:

- Start, stop, restart, and refresh operations complete successfully.
- Runtime records and deployment records update with safe metadata.
- The UI does not expose raw secret-bearing command lines.

### 6. Remote Logs

- Open the Logs workflow.
- Read the remote FRP log.
- Refresh logs.
- Confirm WARN or ERROR rows remain visually readable if present.

Pass criteria:

- Logs can be read from the remote node.
- Log failure states are recoverable and shown in Chinese.
- Logs are not written into SQLite by default.
- Sensitive credentials are not shown or persisted.

### 7. Real Tunnel Access

- Start a local test service, such as:

```powershell
python -m http.server 8080
```

- Access the remote endpoint from a browser or command line.

Example:

```text
http://<VPS_IP>:18080/
```

Pass criteria:

- Remote access reaches the expected local test service or target.
- Stopping the remote FRP process causes the tunnel to stop responding.
- Restarting the process restores access.

### 8. Persistence And Safety

- Restart FrpNexus.
- Confirm settings, nodes, tunnels, configuration versions, runtime records, and deployment records remain available.
- Export local data if needed for inspection.
- Inspect SQLite, local logs, and export snapshots for sensitive-field boundaries.

Pass criteria:

- Ordinary data persists.
- SSH passwords, tokens, private key contents, and private key passphrases are absent from SQLite, logs, and export snapshots.
- Private key paths or display names may appear only when explicitly configured and non-secret.

## Failure Record Template

Use this template for each failed check:

```text
页面：
操作：
期望结果：
实际结果：
错误文案：
是否可重试：
是否阻断闭环：
是否涉及敏感信息：
截图或日志片段：
备注：
```

## v1.0 Gate Pass Criteria

The v1.0 RC gate passes only when:

- `dotnet build` passes.
- Full `dotnet test` passes.
- One real Linux node completes SSH testing.
- FRP binaries and TOML configuration can be uploaded.
- At least one HTTP or TCP tunnel reaches the local test service through the remote endpoint.
- Remote start, stop, restart, status refresh, and log reading work.
- Ordinary data survives app restart.
- Sensitive data is not persisted, logged, or exported.
- Preview publishing remains deferred until the user explicitly re-enables it.
