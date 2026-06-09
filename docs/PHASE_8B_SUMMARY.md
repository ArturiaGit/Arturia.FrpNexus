# Phase 8B Summary

## 1. Phase 8B 目标

Phase 8B 将 CLI 接入 Phase 8A 的 LiteDB 配置持久化能力，新增 CLI config/profile 管理命令，并让 `run <profileId>` 使用 LiteDB 中的持久化 profile。

本阶段不修改 Desktop GUI，不启动真实 `frpc`，不调用 `systemctl`。

## 2. 当前分支

```text
feature/phase-8b-cli-config-profiles
```

Phase 8B 从 `develop` 创建并切换到该 feature 分支实现。

## 3. 创建的文件

```text
src/Arturia.FrpNexus.Cli/Commands/ConfigCommands.cs
src/Arturia.FrpNexus.Cli/Commands/ProfileCommands.cs

tests/Arturia.FrpNexus.Tests/Cli/ConfigCommandsTests.cs
tests/Arturia.FrpNexus.Tests/Cli/ConsoleCapture.cs
tests/Arturia.FrpNexus.Tests/Cli/FakeAvalonDaemon.cs
tests/Arturia.FrpNexus.Tests/Cli/ProfileCommandsTests.cs
tests/Arturia.FrpNexus.Tests/Cli/RootCommandsTests.cs

docs/PHASE_8B_SUMMARY.md
```

## 4. 修改的文件

```text
src/Arturia.FrpNexus.Cli/Program.cs
src/Arturia.FrpNexus.Cli/Commands/CliOutput.cs
src/Arturia.FrpNexus.Cli/Commands/RootCommands.cs
```

## 5. 新增 NuGet package

Phase 8B 未新增 NuGet package。

继续复用 Phase 8A 引入的：

```text
LiteDB 5.0.21
```

## 6. CLI DI 注册

`src/Arturia.FrpNexus.Cli/Program.cs` 新增 LiteDB 相关注册：

```text
IFrpNexusDatabasePathProvider -> FrpNexusDatabasePathProvider
LiteDbConnectionFactory
IFrpNexusSettingsStore -> LiteDbFrpNexusSettingsStore
ITunnelProfileRepository -> LiteDbTunnelProfileRepository
SettingsService
TunnelProfileService
```

同时新增命令组：

```text
config
profile
```

## 7. config 命令

新增：

```text
config show
config get frpc-path
config set frpc-path <path>
```

行为：

1. `config show` 输出 LiteDB settings 摘要。
2. `config get frpc-path` 输出当前 `frpcPath`；未设置时输出明确提示。
3. `config set frpc-path <path>` 保存 `frpcPath`。
4. `config set frpc-path` 只校验 path 非空白。
5. 不验证 path 是否存在。
6. 不搜索 PATH。
7. 不自动下载 `frpc`。

## 8. profile 命令

新增：

```text
profile list
profile show <id>
profile add <id> --name <name> --protocol <tcp|udp|http|https> --local-host <host> --local-port <port> --remote-port <port> --server-address <addr> --server-port <port> [--disabled]
profile remove <id>
```

行为：

1. `profile list` 列出 LiteDB profiles；空库输出明确提示。
2. `profile show <id>` 找到时输出字段，找不到返回失败。
3. `profile add` 使用 Phase 6 `IExcaliburTunnel.Validate` 校验。
4. TCP/UDP 可通过 validation。
5. HTTP/HTTPS 当前返回 Phase 6 不支持错误。
6. duplicate id 使用 Phase 8A upsert 策略，输出“已保存 profile”。
7. `profile remove <id>` 删除存在 profile 返回成功。
8. `profile remove <id>` 找不到返回失败并输出明确错误。

## 9. run <profileId> 行为变化

Phase 8B 已按用户确认修改 `run <profileId>` 行为：

1. 必须从 LiteDB `ITunnelProfileRepository` 查找 profile。
2. 找不到 profile 时失败。
3. 不 fallback 到 `CliProfileFactory` 默认 profile。
4. 找不到时输出明确说明，提示先用 `profile add` 创建持久化 profile。

`frpcPath` 优先级：

```text
--frpc-path -> LiteDB settings.frpcPath -> FRPNEXUS_FRPC_PATH
```

如果最终没有可用 `frpcPath`：

1. 输出明确错误。
2. 不搜索 PATH。
3. 不自动下载 `frpc`。
4. 不启动 `frpc`。

## 10. 测试覆盖

Phase 8B 新增测试覆盖：

1. `config show` 默认 settings。
2. `config get frpc-path` 未设置时输出明确提示。
3. `config set frpc-path` 保存后可读取。
4. `config set frpc-path` 空白 path 失败。
5. `profile list` 空 DB 输出空提示。
6. `profile add` TCP profile 后 repository 可查到。
7. `profile add` UDP profile 后 repository 可查到。
8. `profile add` HTTP/HTTPS 返回 Phase 6 不支持 validation error。
9. `profile show` 找到时输出字段。
10. `profile show` 找不到时失败。
11. `profile remove` 删除存在 profile。
12. `profile remove` 删除不存在 profile 失败。
13. `run` 找不到 profile 失败。
14. `run` 使用 DB profile。
15. `run` 使用 `frpcPath` 优先级：`--frpc-path` -> LiteDB settings -> `FRPNEXUS_FRPC_PATH`。
16. 无可用 `frpcPath` 时 `run` 失败且不启动 daemon。

测试使用临时 LiteDB 文件和 fake daemon，不污染真实用户配置目录，不启动真实 `frpc`。

## 11. 明确未实现内容

Phase 8B 明确未实现：

1. 未修改 Desktop GUI。
2. 未修改 Desktop Program、App、MainWindow 或 GUI 行为文件。
3. 未做 GUI profile CRUD。
4. 未让托盘设置跨重启生效。
5. 未启动真实 `frpc`。
6. 未调用 `systemctl`。
7. 未写 service unit。
8. 未自动下载 `frpc`。
9. 未自动搜索 PATH。
10. 未做多进程 DB 并发策略。
11. 未做 DB 迁移或修复 UI。
12. 未存储 token、密码或敏感凭据。
13. 未新增 NuGet package。
14. 未新增 CI、安装器、自动更新或遥测。
15. 未进入 Phase 8C、Phase 9 或其他阶段。

## 12. 验证命令

Phase 8B 验证入口：

```powershell
dotnet restore "Arturia.FrpNexus.sln"
dotnet build "Arturia.FrpNexus.sln" --no-restore
dotnet test "Arturia.FrpNexus.sln" --no-build
dotnet run --project "src/Arturia.FrpNexus.Cli/Arturia.FrpNexus.Cli.csproj" -- --help
```

不执行会写入真实用户配置目录的 mutating CLI smoke test。
