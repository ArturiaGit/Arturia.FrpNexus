# FrpNexus Phase 10D Architecture Diagnosis

> 文档类型：下一阶段演进与架构诊断报告  
> 基准状态：Phase 10D 后的 `develop` 分支  
> 适用范围：Phase 11+ 规划、技术 spike、架构边界审查  
> 约束：本文档不代表已实现功能；后续实现仍需遵守 `AGENTS.md` 的计划、审批、Git preflight 和阶段验收规则。

## 1. 当前结论

FrpNexus 当前架构方向健康：Core 边界清晰，Infrastructure 明确限制真实能力，CLI/Desktop 共享 LiteDB profile/settings，测试覆盖了 TOML 子集、CLI、持久化和 Desktop ViewModel 行为。

最大风险不是功能不足，而是后续若同时推进后台 daemon、HTTP/HTTPS、自动下载、service install 和 full TOML parser，会把 `IAvalonDaemon`、`TunnelProfile`、Desktop ViewModel、LiteDB path strategy 和 TOML 子集同时拉入高复杂度区。

推荐后续路线应先补测试与路径地基，再收敛真实运行状态展示，然后扩展协议模型，最后进入 daemon/IPC 与 service install。

## 2. Feature Gap Analysis

### P0 必须实现

- HTTP/HTTPS tunnel 完整支持：当前 `TunnelProtocol` 已包含 HTTP/HTTPS，但 validation、TOML preview 和 run 边界仍拒绝这两类协议，且 `TunnelProfile` 缺少协议专属字段。
- 后台 daemon 与跨进程控制：当前 `FrpAvalonDaemon` 只管理当前进程启动的 foreground `frpc` 子进程，CLI `daemon status/stop` 仍是边界说明。
- `frpc` 探测与路径策略：当前只支持显式路径、LiteDB settings 和 `FRPNEXUS_FRPC_PATH`，不搜索 PATH，不检测版本，不自动下载。
- 显式临时 DB path / test-mode path strategy：当前 mutating CLI smoke test 可能写入真实用户 LiteDB 路径，是后续 CI 和 CLI 自动化验收的前置风险。
- 配置导入/导出：当前 TOML parser/serializer 只支持项目生成的 TCP/UDP 最小子集，没有用户级 import/export workflow。
- Runtime 错误恢复策略：缺少自动重连、退出原因分类、失败重试、运行历史和健康检查策略。

### P1 锦上添花

- `frps` server support。
- Linux user-level systemd install/enable/start/stop。
- Windows Service 与 macOS LaunchAgent。
- FRP Admin API 与流量看板。
- 多 profile 并发运行或 profile group。
- LiteDB 备份、迁移、修复、并发冲突处理。
- GUI 自动化测试和截图回归。

### P2 未来构想

- WebDAV / GitHub Gist 云同步。
- 模板市场或插件化协议扩展。
- 自动更新、遥测、崩溃上报。

## 3. Architecture & Tech Debt

### AvalonDaemon

`FrpAvalonDaemon` 当前适合作为 foreground process adapter，但 `IAvalonDaemon` 已开始同时承载当前进程和未来跨进程 daemon 语义。后续进入 daemon 前，应先拆分语义：

- `IFrpcProcessRunner`：只负责当前进程启动、停止、读取子进程日志。
- `IDaemonClient`：负责跨进程 IPC status/start/stop/restart。
- `IAvalonDaemon` 或 Application use case：作为 CLI/Desktop 使用的门面。

### Runtime Snapshot

`DaemonRuntimeSnapshot` 目前只有 status、active profile、health message 和 recent logs。进入后台 daemon 前，应逐步加入：

- runtime mode：Foreground / Daemon / Unknown。
- PID、启动时间、退出码。
- generated config path 或 config source。
- process owner/source。
- log cursor 或增量读取标记。

### ExcaliburTunnel / TOML

当前 `FrpTomlParser` 是最小手写 parser，只适合解析本项目 serializer 生成的 TCP/UDP 子集。后续应避免把它包装成 full TOML parser。

HTTP/HTTPS 支持不应简单把所有字段塞入 `TunnelProfile`。建议使用基础 profile + 协议专属 options：

- `TcpTunnelOptions`
- `UdpTunnelOptions`
- `HttpTunnelOptions`
- `HttpsTunnelOptions`

### Application Layer

`SettingsService` 和 `TunnelProfileService` 目前主要是 repository pass-through。后续应逐步把 validation、active profile、import/export、runtime orchestration 放入 Application 层，避免 CLI 和 ViewModel 复制业务规则。

### Desktop ViewModel

`TunnelsPageViewModel` 已承担 CRUD、validation、preview、runtime start/stop 和文案状态。继续扩展 HTTP/HTTPS 与 daemon 时，应考虑拆出：

- `TunnelEditorViewModel`
- `RuntimeControlViewModel`
- 或 Application 层 use case 服务

### Logs Polling

`LogsPageViewModel` 当前每 1 秒读取 snapshot，短期可接受。进入 daemon 或多 profile 后，应调研增量日志 cursor 或事件流，保留手动刷新 fallback。

### LiteDB

当前每次操作打开数据库，简单清晰；但缺少：

- 显式 DB path override。
- schema version / migration。
- backup / restore。
- 多进程锁冲突处理。
- 损坏恢复策略。

### CLI UX

未知 protocol 不应静默 fallback 到 TCP。后续 CLI profile 创建应显式报错，避免用户拼写错误产生错误配置。

### Dashboard

Dashboard 仍是静态 mock。进入更多 runtime 能力前，应先绑定当前 Desktop foreground runtime snapshot，避免工作台状态与真实运行状态冲突。

## 4. Recommended Roadmap

### Phase 11A：Temporary DB Path / Test Mode Strategy

目标：为 CLI/Desktop/testing 引入显式数据库路径覆盖能力，避免 mutating smoke test 写入真实用户 LiteDB。

验收标准：

- 测试和 smoke 可使用临时 DB path。
- 默认用户路径行为不变。
- 不启动真实 `frpc`，不修改业务功能。

### Phase 11B：Dashboard Runtime Binding

目标：把 Dashboard 从静态 mock 改为读取当前 Desktop foreground runtime snapshot。

验收标准：

- Dashboard 不再显示 mock 已连接。
- 状态、active profile、recent logs 与 Logs/Tunnels 页一致。
- 不引入后台 daemon 或跨进程控制。

### Phase 11C：HTTP/HTTPS Profile Model, Validation, and Preview

目标：扩展 profile model、validation、CLI/Desktop 表单和 TOML preview，支持 HTTP/HTTPS 配置子集。

验收标准：

- HTTP/HTTPS profile 可保存、校验、预览。
- TCP/UDP 行为不回归。
- 不承诺 full TOML parser，不实现 `frps`。

### Phase 11D：frpc PATH Discovery and Version Detection

目标：在显式路径、settings、环境变量基础上增加 PATH 搜索和版本检测。

验收标准：

- CLI/Desktop 可显示 resolved path、version 和错误原因。
- 显式路径优先级不变。
- 不自动下载 `frpc`。

### Phase 12+

- Config import/export。
- Foreground runtime hardening：PID、启动时间、退出码、runtime mode、临时配置路径或来源。
- LiteDB migration/backup/restore。

### Phase 13+

- Daemon/IPC prototype。
- Linux user-level systemd install。
- Windows Service / macOS LaunchAgent 分平台落地。

## 5. Technical Spike Suggestions

### IPC

优先调研 .NET Named Pipes。它适合本机单用户 daemon，跨平台支持较好，且不需要一开始引入 HTTP/gRPC server。应通过 `IDaemonTransport` 抽象隐藏实现细节。

### Daemon Host

先实现 `frpnexus daemon serve` 作为前台 daemon host，再由 systemd、Windows Service 或 LaunchAgent 托管。不要一开始直接写三套平台 service。

### Logs

短期保持 snapshot；中期实现 `GetLogsSince(cursor)`；长期再考虑事件流。

### TOML

完整 TOML import 前必须做 spike：评估引入成熟 TOML NuGet 的收益与成本。若不引入依赖，必须继续明确“只支持 FrpNexus 子集”。

### frpc Download

自动下载必须晚于 PATH discovery。下载阶段需要平台架构映射、缓存目录、校验 hash、用户确认和失败回滚。

### Service Install

所有 service install 必须支持 dry-run/preview、显式确认和边界说明。Linux 优先 user-level systemd，不默认 sudo，不默认 system-level service。

## 6. Phase 11+ Guardrail Summary

任何 Phase 11+ 计划必须说明：

1. 它遵循本文档的哪一条建议。
2. 它明确不实现哪些更高风险能力。
3. 它为何能独立测试。
4. 它预计创建或修改哪些文件。
5. 它如何避免污染真实用户配置或启动真实 `frpc`，除非该阶段明确获批。
