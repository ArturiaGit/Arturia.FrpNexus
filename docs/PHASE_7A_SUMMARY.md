# Phase 7A Summary

## 1. Phase 7A 目标

Phase 7A 只实现 CLI/systemd 安全预览子集，为后续 Linux user-level service 集成提供可审查的 unit 文本生成能力。

已确认目标：

1. 实现 `service status`。
2. 实现 `service explain`。
3. 实现 `service preview <profileId> --frpnexus-path <path> --frpc-path <path>`。
4. 生成 Linux user-level systemd unit 文本并输出到 stdout。
5. 输出平台能力说明和安全边界说明。
6. 不写 service 文件，不调用 `systemctl`，不启动后台服务。

## 2. 当前分支

```text
feature/phase-7a-systemd-preview
```

Phase 7A 从 `develop` 创建并切换到该 feature 分支实现。

## 3. 创建的文件

```text
src/Arturia.FrpNexus.Core/InvisibleAirService/ServicePlatform.cs
src/Arturia.FrpNexus.Core/InvisibleAirService/SystemdServiceUnitRequest.cs
src/Arturia.FrpNexus.Core/InvisibleAirService/SystemdServiceUnitPreview.cs

src/Arturia.FrpNexus.Infrastructure/InvisibleAirService/LinuxInvisibleAirService.cs
src/Arturia.FrpNexus.Infrastructure/InvisibleAirService/SystemdServiceUnitBuilder.cs

docs/PHASE_7A_SUMMARY.md
```

## 4. 修改的文件

```text
src/Arturia.FrpNexus.Core/InvisibleAirService/IInvisibleAirService.cs

src/Arturia.FrpNexus.Infrastructure/Mocks/MockInvisibleAirService.cs

src/Arturia.FrpNexus.Cli/Commands/CliOutput.cs
src/Arturia.FrpNexus.Cli/Commands/ServiceCommands.cs
src/Arturia.FrpNexus.Cli/Program.cs
```

## 5. Core interface 变化

`IInvisibleAirService` 新增 systemd unit preview 入口：

```csharp
SystemdServiceUnitPreview PreviewUserServiceUnit(SystemdServiceUnitRequest request);
```

用途：

1. 让 CLI 通过 Core abstraction 请求 preview。
2. 避免 CLI 直接依赖 unit 文本生成细节。
3. 保持 Phase 7A 只生成 preview，不执行平台服务操作。

## 6. CLI service 命令变化

Phase 7A 的 `service` 命令包括：

```powershell
dotnet run --project "src/Arturia.FrpNexus.Cli/Arturia.FrpNexus.Cli.csproj" -- service status
dotnet run --project "src/Arturia.FrpNexus.Cli/Arturia.FrpNexus.Cli.csproj" -- service explain
dotnet run --project "src/Arturia.FrpNexus.Cli/Arturia.FrpNexus.Cli.csproj" -- service preview my-server --frpnexus-path "/opt/frpnexus/frpnexus" --frpc-path "/opt/frp/frpc"
```

说明：

1. `service status` 只输出 .NET 平台检测和说明性状态。
2. `service explain` 说明 Phase 7A 范围、安全边界和显式路径要求。
3. `service preview` 只输出 Linux user-level systemd unit 文本到 stdout。
4. `service preview` 必须显式接收 `<profileId>`、`--frpnexus-path`、`--frpc-path`。
5. Phase 7A 只做路径字符串基本校验，不要求文件真实存在。

## 7. systemd unit preview 设计

`SystemdServiceUnitBuilder` 生成 user-level unit preview。

核心输出：

```ini
[Unit]
Description=FrpNexus user service preview for my-server
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
ExecStart="/opt/frpnexus/frpnexus" run "my-server" --frpc-path "/opt/frp/frpc"
Restart=on-failure
RestartSec=5s

[Install]
WantedBy=default.target
```

边界：

1. 不假设 `frpnexus` 在 PATH 中。
2. 不假设 `frpc` 在 PATH 中。
3. `ExecStart` 使用显式 `frpnexus` path 和显式 `frpc` path。
4. 只输出 preview，不写入任何 unit 文件。

## 8. 如何复用 Phase 6 frpc integration

Phase 7A 没有新增第二套 `frpc` process management。

复用方式：

1. systemd unit preview 的 `ExecStart` 调用现有 CLI `run <profileId>`。
2. `run` 继续使用 Phase 6 的 `IAvalonDaemon.StartAsync(StartTunnelRequest)`。
3. `run` 继续通过 `--frpc-path` 显式接收 `frpc` binary path。
4. Phase 6 的前台阻塞语义保持不变。

## 9. 明确未实现内容

Phase 7A 明确未实现：

1. 写 service unit 文件。
2. 写 `~/.config/systemd/user`。
3. 写 `/etc/systemd/system`、`/usr/lib/systemd/system`、`/lib/systemd/system`。
4. 调用 `systemctl`。
5. 调用 `sudo`、`pkexec`、UAC、PowerShell elevation 或任何提权操作。
6. `systemctl daemon-reload`、`enable`、`start`、`stop`、`status`。
7. system-level service。
8. Windows Service。
9. macOS LaunchAgent。
10. GUI tray。
11. Desktop GUI 修改。
12. 后台常驻 daemon。
13. 自动下载 `frpc`。
14. 搜索 PATH。
15. kill、attach、搜索或接管外部进程。
16. 新增 NuGet package。
17. Phase 7B 或 Phase 8。

## 10. 验证命令和结果

### 10.1 Solution build

```powershell
dotnet build "Arturia.FrpNexus.sln" --no-restore
```

结果：成功，`0` warning，`0` error。

### 10.2 service status

```powershell
dotnet run --project "src/Arturia.FrpNexus.Cli/Arturia.FrpNexus.Cli.csproj" -- service status
```

结果：成功。输出当前平台为 Windows，并明确说明 Phase 7A 只提供 Linux user-level systemd unit preview，未调用 `systemctl`，未安装或启动服务。

### 10.3 service explain

```powershell
dotnet run --project "src/Arturia.FrpNexus.Cli/Arturia.FrpNexus.Cli.csproj" -- service explain
```

结果：成功。输出 Phase 7A 范围、显式路径要求和安全边界。

### 10.4 service preview

```powershell
dotnet run --project "src/Arturia.FrpNexus.Cli/Arturia.FrpNexus.Cli.csproj" -- service preview my-server --frpnexus-path "/opt/frpnexus/frpnexus" --frpc-path "/opt/frp/frpc"
```

结果：成功。输出 `frpnexus@my-server.service` 的 Linux user-level systemd unit preview 到 stdout。

验证限制：

1. 不执行 `systemctl`。
2. 不写 unit 文件。
3. 不启动真实 service。
4. 当前 Windows 环境无法验证真实 Linux systemd runtime。

## 11. Phase 7B 入口建议

Phase 7B 可单独规划 GUI tray/background behavior。

进入 Phase 7B 前仍需：

1. 用户明确批准 Phase 7B mini-plan。
2. 重新执行 Git preflight。
3. 明确是否允许修改 Desktop GUI、托盘图标资源和窗口关闭行为。
4. 不从 Phase 7A 自动进入 Phase 7B。
