# FrpNexus Product Constraints

## Product Identity

FrpNexus is an open-source desktop control console for FRP deployment, configuration, process control, log inspection, and basic diagnostics.

It is not only a configuration editor. It should behave like a lightweight desktop operations console for personal developers.

## Primary User

The first version targets personal developers, independent developers, self-hosting users, and lightweight operations users.

Typical users:

- Own one or more VPS, cloud servers, home servers, or NAS devices.
- Need to expose local Web, API, SSH, RDP, NAS, or game services through FRP.
- Understand basic server, port, SSH, and domain concepts.
- Prefer not to repeatedly write FRP configuration files and SSH commands by hand.

## MVP Goal

The MVP must let a user complete one full remote FRP deployment and runtime management workflow.

The MVP must support:

- Managing remote Linux nodes.
- Testing SSH connections.
- Downloading FRP releases.
- Selecting suitable `frpc` / `frps` binaries.
- Uploading FRP binaries through SFTP.
- Generating TOML configuration through forms.
- Uploading configuration files.
- Starting, stopping, and restarting remote FRP processes.
- Viewing process status and remote logs.
- Creating TCP, UDP, HTTP, and HTTPS tunnels.

## Core Modules

The product must keep runtime management as an MVP capability, but the main navigation follows the current Stitch UI source.

The current Stitch-driven main navigation uses these Chinese modules:

- 仪表盘
- 节点
- 隧道
- 配置
- 日志
- 设置

Runtime-related process control, status, and diagnostics may be represented inside the existing Stitch pages, especially 节点, 配置, and 日志, until a dedicated Stitch runtime page is added.

## Configuration Direction

Use TOML as the primary FRP configuration format.

Do not prioritize INI configuration. INI may be treated as legacy import/export support in a later phase, but not as the MVP default.

## Non-Goals For MVP

Do not build these in the first version:

- Agent mode
- Cloud sync
- Team permission management
- Web Dashboard
- Billing
- Kubernetes integration
- Multi-tenant backend
- Complex traffic analytics
- Advanced audit logs
