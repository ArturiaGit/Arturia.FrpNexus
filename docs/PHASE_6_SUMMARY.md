# Phase 6 Summary

## 1. Phase 6 目标

Phase 6 的目标是将 FrpNexus 从 Phase 5 的 CLI structure-only / mock mode 推进到最小真实 `frpc` client 集成。

已确认目标：

1. 只集成 `frpc` client，不集成 `frps` server。
2. 使用 `System.Diagnostics.Process` 启动当前 FrpNexus CLI 进程管理的 `frpc` 子进程。
3. `run <profileId>` 使用前台阻塞模式运行。
4. 支持 `Ctrl+C` / cancellation 时尽量停止当前 FrpNexus CLI 进程启动的 `frpc` 子进程。
5. 采集 stdout/stderr 日志并映射为 `DaemonLogEntry`。
6. 更新 `RuntimeStatus` 与 `DaemonRuntimeSnapshot`。
7. 支持 TCP/UDP `TunnelProfile` 到最小 FRP TOML 子集的生成。
8. 提供受限 TOML parser，仅覆盖本项目生成的最小 TOML 子集。
9. 明确 `daemon status` / `daemon stop` 不支持跨进程 daemon 状态或停止。
10. 不进入 Phase 7 service/tray/systemd/platform API 范围。

## 2. 当前分支

```text
feature/phase-6-frp-integration
```

Phase 6 从 `develop` 创建并切换到该 feature 分支实现。

## 3. 创建的文件

```text
src/Arturia.FrpNexus.Cli/Commands/CliProfileFactory.cs

src/Arturia.FrpNexus.Core/AvalonDaemon/StartTunnelRequest.cs
src/Arturia.FrpNexus.Core/ExcaliburTunnel/TunnelConfigurationParseResult.cs

src/Arturia.FrpNexus.Infrastructure/AvalonDaemon/FrpAvalonDaemon.cs

src/Arturia.FrpNexus.Infrastructure/ExcaliburTunnel/FrpExcaliburTunnel.cs
src/Arturia.FrpNexus.Infrastructure/ExcaliburTunnel/FrpTomlParser.cs
src/Arturia.FrpNexus.Infrastructure/ExcaliburTunnel/FrpTomlSerializer.cs

docs/PHASE_6_SUMMARY.md
```

## 4. 修改的文件

```text
src/Arturia.FrpNexus.Cli/Commands/CliOutput.cs
src/Arturia.FrpNexus.Cli/Commands/DaemonCommands.cs
src/Arturia.FrpNexus.Cli/Commands/RootCommands.cs
src/Arturia.FrpNexus.Cli/Commands/ServiceCommands.cs
src/Arturia.FrpNexus.Cli/Commands/TunnelCommands.cs
src/Arturia.FrpNexus.Cli/Program.cs

src/Arturia.FrpNexus.Core/AvalonDaemon/IAvalonDaemon.cs
src/Arturia.FrpNexus.Core/ExcaliburTunnel/IExcaliburTunnel.cs

src/Arturia.FrpNexus.Infrastructure/Mocks/MockAvalonDaemon.cs
src/Arturia.FrpNexus.Infrastructure/Mocks/MockExcaliburTunnel.cs
```

## 5. Core interfaces 的变化

### 5.1 IAvalonDaemon

`IAvalonDaemon` 保留 Phase 4/5 的 profile-id based lifecycle intent，同时新增真实 `frpc` 启动需要的领域化 request overload。

新增成员：

```csharp
Task StartAsync(StartTunnelRequest request, CancellationToken cancellationToken = default);

Task RestartAsync(StartTunnelRequest request, CancellationToken cancellationToken = default);
```

说明：

1. `StartAsync(string profileId)` 保留给既有 mock/structure 用法，但真实 Phase 6 `frpc` 启动需要 `StartTunnelRequest`。
2. `StartTunnelRequest` 将 `TunnelProfile`、`frpc` binary 路径和是否保留临时配置文件组合为领域请求。
3. CLI 仍通过 `IAvalonDaemon` 访问 daemon 能力，保持 GUI/CLI 可共享 Core interfaces。

### 5.2 IExcaliburTunnel

`IExcaliburTunnel` 保留校验与配置预览能力，并新增受限 TOML 解析入口。

新增成员：

```csharp
TunnelConfigurationParseResult ParseConfiguration(string configuration);
```

说明：

1. `Validate(TunnelProfile profile)` 在真实实现中执行 TCP/UDP 最小规则校验。
2. `PreviewConfiguration(TunnelProfile profile)` 在真实实现中输出最小 `frpc` TOML 子集。
3. `ParseConfiguration` 只承诺解析本项目生成的最小 TOML 子集，不声明完整 TOML/FRP 配置兼容。

### 5.3 StartTunnelRequest

新增文件：

```text
src/Arturia.FrpNexus.Core/AvalonDaemon/StartTunnelRequest.cs
```

字段：

```csharp
TunnelProfile Profile
string FrpcPath
bool KeepGeneratedConfig = false
```

用途：

1. 表达真实 `frpc` 启动所需的最小领域请求。
2. 避免 CLI 直接绕过 Core interface 调用 Infrastructure。
3. 保留后续 GUI 通过相同 Core interface 发起启动请求的可能性。

### 5.4 TunnelConfigurationParseResult

新增文件：

```text
src/Arturia.FrpNexus.Core/ExcaliburTunnel/TunnelConfigurationParseResult.cs
```

字段：

```csharp
bool IsValid
TunnelProfile? Profile
IReadOnlyList<string> Errors
```

用途：

1. 表达受限 TOML parser 的解析结果。
2. 在解析失败时返回明确错误列表。
3. 避免将 parser 异常直接暴露给 CLI 或后续 GUI。

## 6. Infrastructure 真实实现

### 6.1 FrpAvalonDaemon

新增文件：

```text
src/Arturia.FrpNexus.Infrastructure/AvalonDaemon/FrpAvalonDaemon.cs
```

职责：

1. 实现 `IAvalonDaemon`。
2. 使用 `System.Diagnostics.Process` 启动 `frpc -c <generatedConfigPath>`。
3. 只管理当前 FrpNexus CLI 进程启动的 `frpc` 子进程。
4. 采集 stdout/stderr 并转换为 `DaemonLogEntry`。
5. 根据启动、运行、停止、异常退出更新 `RuntimeStatus`。
6. 维护最近日志 ring buffer。
7. 将临时配置文件写入 OS temp 下的 `FrpNexus` 子目录。
8. 默认在停止或进程退出后清理临时配置文件。

边界：

1. 不搜索系统进程。
2. 不 attach 外部 `frpc`。
3. 不 kill 非当前 FrpNexus CLI 进程启动的进程。
4. 不实现后台常驻 daemon。
5. 不实现 service/tray/systemd/platform API。

### 6.2 FrpExcaliburTunnel

新增文件：

```text
src/Arturia.FrpNexus.Infrastructure/ExcaliburTunnel/FrpExcaliburTunnel.cs
```

职责：

1. 实现 `IExcaliburTunnel`。
2. 校验 `TunnelProfile` 基础字段和端口范围。
3. Phase 6 只允许 TCP/UDP。
4. HTTP/HTTPS 返回明确 validation error。
5. 输出最小 FRP TOML 子集预览。
6. 调用受限 parser 解析本项目生成的最小 TOML 子集。

### 6.3 FrpTomlSerializer

新增文件：

```text
src/Arturia.FrpNexus.Infrastructure/ExcaliburTunnel/FrpTomlSerializer.cs
```

职责：

1. 将 TCP/UDP `TunnelProfile` 序列化为最小 `frpc` TOML 子集。
2. 输出字段包括 `serverAddr`、`serverPort`、`[[proxies]]`、`name`、`type`、`localIP`、`localPort`、`remotePort`。
3. 对字符串执行基础 TOML 转义。
4. 不支持 HTTP/HTTPS 专用字段。

示例输出：

```toml
serverAddr = "frp.example.internal"
serverPort = 7000

[[proxies]]
name = "my-server"
type = "tcp"
localIP = "127.0.0.1"
localPort = 8080
remotePort = 18080
```

### 6.4 FrpTomlParser

新增文件：

```text
src/Arturia.FrpNexus.Infrastructure/ExcaliburTunnel/FrpTomlParser.cs
```

职责：

1. 解析本项目 `FrpTomlSerializer` 生成的最小 TOML 子集。
2. 支持 `serverAddr`、`serverPort` 和单个 `[[proxies]]` block。
3. 支持 TCP/UDP 类型。
4. 返回 `TunnelConfigurationParseResult`。

限制：

1. 不声明完整 TOML parser 能力。
2. 不声明完整 FRP 配置兼容。
3. 不支持多个 proxy 的完整业务管理。
4. 不支持复杂 TOML 类型、数组、inline table 或第三方 FRP 扩展字段。

## 7. CLI 变化

### 7.1 run <profileId>

`run <profileId>` 从 Phase 5 的 mock 启动意图变为 Phase 6 的真实 `frpc` 前台启动命令。

行为：

1. 构造 `TunnelProfile`。
2. 解析 `frpc` binary 路径。
3. 调用 `IAvalonDaemon.StartAsync(StartTunnelRequest)`。
4. 若启动成功，当前 CLI 进程前台阻塞。
5. 按 `Ctrl+C` 时停止当前 CLI 进程启动的 `frpc` 子进程。

### 7.2 --frpc-path

`run` 支持参数：

```text
--frpc-path <path>
```

用途：

1. 显式指定 `frpc` binary 路径。
2. 优先级高于环境变量。
3. 若路径不存在，返回清晰错误并将状态置为 `Failed`。

### 7.3 FRPNEXUS_FRPC_PATH fallback

当未传入 `--frpc-path` 时，CLI 读取：

```text
FRPNEXUS_FRPC_PATH
```

边界：

1. Phase 6 不搜索系统 `PATH`。
2. Phase 6 不自动下载 `frpc`。
3. Phase 6 不写入系统配置目录、服务目录或启动项目录。

### 7.4 tunnel validate

`tunnel validate` 使用 `FrpExcaliburTunnel` 执行 Phase 6 validation。

默认 profile 为 TCP：

```text
my-server
127.0.0.1:8080
remotePort: 18080
frp.example.internal:7000
```

Phase 6 中 TCP/UDP 可通过；HTTP/HTTPS 会返回明确 validation error。

### 7.5 tunnel preview

`tunnel preview` 输出 Phase 6 最小 TCP/UDP FRP TOML 子集预览。

说明：

1. 不写入 TOML 文件。
2. 不声明完整 FRP TOML 兼容。
3. 用于验证 serializer 输出结构。

### 7.6 daemon status/stop 的 Phase 6 限制说明

`daemon status` 和 `daemon stop` 明确说明：

```text
Phase 6 不支持跨进程 daemon 状态/停止；真实后台服务化留到 Phase 7。
```

`daemon stop` 还明确说明：

```text
daemon stop 不会查找、attach 或 kill 外部 frpc 进程。
只有 run 命令所在的当前 CLI 进程会在 Ctrl+C 时停止自己启动的 frpc 子进程。
```

## 8. 支持范围

Phase 6 支持：

1. 只支持 `frpc` client。
2. 只支持 TCP/UDP 最小 FRP TOML 子集。
3. `run` 是前台阻塞模式。
4. `frpc` 进程只由当前 FrpNexus CLI 进程管理。
5. `frpc` 路径只来自 `--frpc-path` 或 `FRPNEXUS_FRPC_PATH`。
6. 日志采集来自当前子进程 stdout/stderr。
7. 临时 TOML 配置写入 OS temp 下的 `FrpNexus` 子目录。

## 9. 明确未实现内容

Phase 6 明确未实现：

1. `frps` server。
2. HTTP/HTTPS tunnel。
3. 完整 TOML parser。
4. 自动下载 `frpc`。
5. `PATH` 自动搜索。
6. 跨进程 daemon 状态持久化。
7. 跨进程 daemon stop。
8. `systemd`。
9. Windows Service。
10. macOS LaunchAgent。
11. tray/platform API。
12. 后台常驻服务化。
13. 数据库。
14. CI。
15. 打包/安装器。
16. 遥测。

## 10. 验证命令和结果

### 10.1 Solution build

已执行：

```powershell
dotnet build "Arturia.FrpNexus.sln" --no-restore
```

最终结果：成功，`0` warning，`0` error。

首次 build 曾失败一次，原因是 `FrpTomlParser.cs` 中 `string.Split` overload 二义性。已改为显式 `char[]` overload 后重新执行 full build，结果成功。

### 10.2 CLI help

已执行：

```powershell
dotnet run --project "src/Arturia.FrpNexus.Cli/Arturia.FrpNexus.Cli.csproj" -- --help
```

结果：成功，显示 root commands：`run`、`help`、`daemon`、`tunnel`、`service`。

### 10.3 tunnel validate

已执行：

```powershell
dotnet run --project "src/Arturia.FrpNexus.Cli/Arturia.FrpNexus.Cli.csproj" -- tunnel validate
```

结果：成功。默认 TCP profile validation 通过。

### 10.4 tunnel preview

已执行：

```powershell
dotnet run --project "src/Arturia.FrpNexus.Cli/Arturia.FrpNexus.Cli.csproj" -- tunnel preview
```

结果：成功。输出最小 TCP `frpc` TOML 子集预览。

### 10.5 missing binary negative test

已执行：

```powershell
dotnet run --project "src/Arturia.FrpNexus.Cli/Arturia.FrpNexus.Cli.csproj" -- run my-server --frpc-path "Z:\frpnexus-missing\frpc.exe"
```

结果：成功完成 negative test。命令未崩溃，状态为 `Failed`，并清晰输出：

```text
frpc binary 不存在：Z:\frpnexus-missing\frpc.exe
```

## 11. 真实 frpc smoke test 状态

未执行真实 `frpc` smoke test。

原因：没有提供可用 `frpc` binary 路径。根据 Phase 6 边界，只有在用户明确提供可用 `frpc` 路径并确认准备执行的命令后，才执行真实 `frpc` 启动验证。

## 12. 已知限制

1. `run` 是前台阻塞模式，不是后台 daemon。
2. `daemon status` / `daemon stop` 不支持跨进程状态或停止。
3. CLI 进程退出后，内存中的 runtime snapshot 不会持久化。
4. 只支持当前 FrpNexus CLI 进程启动的 `frpc` 子进程。
5. 不 attach、不搜索、不停止外部 `frpc` 进程。
6. 只支持 TCP/UDP。
7. HTTP/HTTPS 因当前 `TunnelProfile` 缺少域名字段，Phase 6 暂不实现。
8. TOML parser 只覆盖本项目生成的最小 TOML 子集。
9. 没有新增自动化测试项目，验证以 build 与 CLI smoke checks 为主。
10. 未执行真实 `frpc` 启动验证，因为没有提供可用 binary 路径。

## 13. Phase 7 入口建议

Phase 7 建议进入 Service/Tray Behavior，但开始前仍需用户明确批准并重新执行 Git preflight。

建议入口：

1. Review Phase 6 代码和本文档。
2. 按需要提交 Phase 6，并将 `feature/phase-6-frp-integration` 合入 `develop`。
3. 从 `develop` 创建新的 Phase 7 feature 分支。
4. 重新读取 `AGENTS.md`、`DESIGN.md`、`SPC.md`、PRD、Phase summaries 和任何已批准计划。
5. 明确 Phase 7 的 platform targets，例如 Linux `systemd`、Windows Service、macOS LaunchAgent、desktop tray 是否分批实现。
6. 严格隔离平台服务能力，不影响 Phase 6 前台 `frpc` client 最小闭环。
7. 不自动进入 Phase 7，必须等待用户明确批准。
