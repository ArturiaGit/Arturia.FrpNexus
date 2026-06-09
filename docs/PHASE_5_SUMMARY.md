# Phase 5 Summary

## 1. Phase 5 目标

Phase 5 的目标是建立 FrpNexus CLI structure，让 CLI 入口从 Phase 1 的 skeleton 发展为可维护的 Cocona 命令结构，同时继续保持 structure-only / mock mode。

已确认目标：

1. 整理 `src/Arturia.FrpNexus.Cli/Program.cs`。
2. 创建最小 CLI command classes。
3. 注册 Phase 4 的 Core interfaces 和 Infrastructure mock implementations。
4. 实现 root-level `run <profileId>` mock command，以贴近 PRD 中的 `./frpnexus run my-server` 使用方向。
5. 实现 `daemon`、`tunnel`、`service` 分组命令。
6. CLI 输出明确标记为 mock / structure-only / preview。
7. 不实现真实 FRP、TOML、daemon、service 或 tray 能力。

## 2. 当前分支

```text
feature/phase-5-cli-structure
```

Phase 5 从 `develop` 创建并切换到该 feature 分支实现。

## 3. 创建的 CLI command 文件

```text
src/Arturia.FrpNexus.Cli/Commands/CliOutput.cs
src/Arturia.FrpNexus.Cli/Commands/RootCommands.cs
src/Arturia.FrpNexus.Cli/Commands/DaemonCommands.cs
src/Arturia.FrpNexus.Cli/Commands/TunnelCommands.cs
src/Arturia.FrpNexus.Cli/Commands/ServiceCommands.cs
```

职责说明：

1. `CliOutput.cs`：提供统一 Phase 5 structure-only / mock mode 提示。
2. `RootCommands.cs`：提供 root-level `run <profileId>` 和 `help` 命令。
3. `DaemonCommands.cs`：提供 `daemon status` 和 `daemon stop` 命令。
4. `TunnelCommands.cs`：提供 `tunnel preview` 和 `tunnel validate` 命令。
5. `ServiceCommands.cs`：提供 `service status` 命令。

## 4. 修改的 Program.cs

```text
src/Arturia.FrpNexus.Cli/Program.cs
```

修改说明：

1. 将 Phase 1 的单文件 skeleton 调整为 Cocona builder 入口。
2. 注册 Phase 4 mock implementations。
3. 注册 root commands。
4. 注册 `daemon`、`tunnel`、`service` subcommands。
5. 未修改 Desktop GUI 入口或业务逻辑。

## 5. Root-level run <profileId> 说明

Phase 5 实现了 root-level 命令：

```powershell
dotnet run --project "src/Arturia.FrpNexus.Cli/Arturia.FrpNexus.Cli.csproj" -- run my-server
```

行为说明：

1. 调用 `IAvalonDaemon.StartAsync(profileId)`。
2. 当前实际注入的是 `MockAvalonDaemon`。
3. 只记录 mock 启动意图。
4. 输出当前 mock snapshot。
5. 明确提示未启动真实 `frpc` / `frps` 进程。

## 6. daemon/tunnel/service 分组命令说明

### 6.1 daemon

```powershell
dotnet run --project "src/Arturia.FrpNexus.Cli/Arturia.FrpNexus.Cli.csproj" -- daemon status
dotnet run --project "src/Arturia.FrpNexus.Cli/Arturia.FrpNexus.Cli.csproj" -- daemon stop
```

说明：

1. `daemon status` 显示 `AvalonDaemon` mock runtime snapshot。
2. `daemon stop` 调用 `IAvalonDaemon.StopAsync()`，只记录 mock 停止意图。
3. 不启动、停止或管理真实进程。

### 6.2 tunnel

```powershell
dotnet run --project "src/Arturia.FrpNexus.Cli/Arturia.FrpNexus.Cli.csproj" -- tunnel preview
dotnet run --project "src/Arturia.FrpNexus.Cli/Arturia.FrpNexus.Cli.csproj" -- tunnel validate
```

说明：

1. `tunnel preview` 使用内置 mock `TunnelProfile` 输出 mock preview。
2. `tunnel validate` 使用内置 mock `TunnelProfile` 执行 basic validation。
3. 不读取真实 FRP 配置。
4. 不写入 TOML 文件。
5. 不生成生产级 FRP TOML。

### 6.3 service

```powershell
dotnet run --project "src/Arturia.FrpNexus.Cli/Arturia.FrpNexus.Cli.csproj" -- service status
```

说明：

1. `service status` 显示 `InvisibleAirService` mock status。
2. 不调用 `systemd`。
3. 不调用 Windows Service API。
4. 不调用 tray 或任何平台 API。

## 7. Cocona builder 与 DI 注册说明

`Program.cs` 当前通过 Cocona builder 建立 CLI app，并注册 Phase 4 mock services：

```csharp
builder.Services.AddSingleton<IAvalonDaemon, MockAvalonDaemon>();
builder.Services.AddSingleton<IExcaliburTunnel, MockExcaliburTunnel>();
builder.Services.AddSingleton<IInvisibleAirService, MockInvisibleAirService>();
```

命令注册结构：

```csharp
app.AddCommands<RootCommands>();
app.AddSubCommand("daemon", daemon => daemon.AddCommands<DaemonCommands>());
app.AddSubCommand("tunnel", tunnel => tunnel.AddCommands<TunnelCommands>());
app.AddSubCommand("service", service => service.AddCommands<ServiceCommands>());
```

Phase 5 未新增 NuGet package。现有 `Cocona` dependency 已满足 CLI structure 需求。

## 8. 如何复用 Phase 4 Core interfaces 和 Infrastructure mocks

Phase 5 沿用 Phase 4 建立的 Core interfaces：

1. `IAvalonDaemon`：用于 root-level `run <profileId>`、`daemon status`、`daemon stop`。
2. `IExcaliburTunnel`：用于 `tunnel preview` 和 `tunnel validate`。
3. `IInvisibleAirService`：用于 `service status`。

Phase 5 通过 DI 注入 Phase 4 mock implementations：

1. `MockAvalonDaemon`：记录 mock 启动/停止意图和 mock runtime snapshot。
2. `MockExcaliburTunnel`：提供 mock/basic validation 和 mock preview。
3. `MockInvisibleAirService`：提供 mock service/tray status。

这样 CLI 只依赖 Core 抽象，具体实现可在 Phase 6 或后续阶段替换。

## 9. CLI 当前是 structure-only / mock mode

Phase 5 CLI 当前明确处于 structure-only / mock mode。

所有命令都会输出类似提示：

```text
FrpNexus CLI - Phase 5 structure-only / mock mode
当前命令只表达 CLI/daemon 意图，不启动真实 frpc，不读取或写入真实 FRP 配置。
```

这些命令只证明 CLI command structure、Cocona routing 和 Phase 4 mock integration 可工作，不代表真实 FRP 能力已经实现。

## 10. 明确未实现内容

Phase 5 明确未实现：

1. 真实 `frpc` / `frps` 启动。
2. 真实进程停止。
3. 真实 FRP 配置读取。
4. 生产 TOML 生成。
5. TOML 解析。
6. daemon 常驻。
7. `systemd` / service 注册。
8. tray / platform API。
9. 数据库。
10. CI。
11. 打包 / 安装器。
12. 遥测。

## 11. 验证命令和结果

### 11.1 Solution build

已执行：

```powershell
dotnet build "Arturia.FrpNexus.sln" --no-restore
```

结果：成功，`0` warning，`0` error。

### 11.2 CLI help

已执行：

```powershell
dotnet run --project "src/Arturia.FrpNexus.Cli/Arturia.FrpNexus.Cli.csproj" -- --help
```

结果：成功，显示 root commands：`run`、`help`、`daemon`、`tunnel`、`service`。

### 11.3 Root run

已执行：

```powershell
dotnet run --project "src/Arturia.FrpNexus.Cli/Arturia.FrpNexus.Cli.csproj" -- run my-server
```

结果：成功。记录 `my-server` 的 mock 启动意图，显示状态 `Running`，并明确提示未启动真实 `frpc` / `frps`。

### 11.4 daemon status

已执行：

```powershell
dotnet run --project "src/Arturia.FrpNexus.Cli/Arturia.FrpNexus.Cli.csproj" -- daemon status
```

结果：成功。显示 `AvalonDaemon` mock runtime snapshot。

### 11.5 daemon stop

已执行：

```powershell
dotnet run --project "src/Arturia.FrpNexus.Cli/Arturia.FrpNexus.Cli.csproj" -- daemon stop
```

结果：成功。记录 mock 停止意图，显示状态 `Stopped`，并明确提示未停止真实进程。

### 11.6 tunnel preview

已执行：

```powershell
dotnet run --project "src/Arturia.FrpNexus.Cli/Arturia.FrpNexus.Cli.csproj" -- tunnel preview
```

结果：成功。输出 `ExcaliburTunnel` mock preview，并明确提示不是生产 FRP TOML，未写入文件。

### 11.7 tunnel validate

已执行：

```powershell
dotnet run --project "src/Arturia.FrpNexus.Cli/Arturia.FrpNexus.Cli.csproj" -- tunnel validate
```

结果：成功。对内置 mock `TunnelProfile` 执行 basic validation，结果通过。

### 11.8 service status

已执行：

```powershell
dotnet run --project "src/Arturia.FrpNexus.Cli/Arturia.FrpNexus.Cli.csproj" -- service status
```

结果：成功。显示 `InvisibleAirService` mock status，并明确提示未调用 `systemd`、tray、Windows Service 或任何平台 API。

## 12. 验证过程中的输出文件锁竞争

最初曾并行执行多个 `dotnet run` smoke checks。并行执行时，.NET build 过程出现输出文件锁竞争，例如 `.deps.json`、`.dll` 或 assembly reference cache 被另一个进程占用。

该问题来自多个 `dotnet run` 同时构建/写入同一输出目录，不是 CLI command 逻辑失败。

之后改为顺序执行相同 smoke checks，所有指定命令均通过。

## 13. 已知限制

1. Phase 5 mock 状态是进程内存级。
2. 每次 CLI run 都可能重新初始化 DI container 和 mock 状态。
3. `run my-server` 后再单独执行 `daemon status` 时，状态不保证跨进程保留。
4. 当前状态不代表真实 FRP 状态。
5. 当前 preview 不代表生产 FRP TOML。
6. 当前 validation 仅是 mock/basic validation。
7. 当前 service status 不代表真实系统服务或托盘状态。

## 14. Phase 6 入口建议

Phase 6 建议进入 FRP Integration，但开始前仍需用户明确批准并重新执行 Git preflight。

建议入口：

1. 先 review 并提交 Phase 5。
2. 将 `feature/phase-5-cli-structure` 按确认流程合入 `develop`。
3. 从 `develop` 创建新的 Phase 6 feature 分支。
4. 重新读取 `AGENTS.md`、`DESIGN.md`、`SPC.md`、PRD、Phase summaries 和任何已批准计划。
5. 在 Phase 6 中再讨论真实 `frpc` 进程管理、TOML 生成/解析、运行状态和日志采集策略。
6. 不应在未批准 Phase 6 计划前直接替换 mocks 或加入真实平台能力。
