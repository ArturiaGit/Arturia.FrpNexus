# Phase 10A Summary

## 1. Phase 10A 目标

Phase 10A 为 Desktop GUI 增加受控的 foreground `frpc` start/stop 能力。

本阶段只允许 Desktop GUI 启动和停止当前 Desktop 进程内 `IAvalonDaemon` instance 管理的 `frpc` 子进程。它不是后台 daemon，不支持跨进程 status/stop，不 attach 外部进程，不枚举 OS 进程，不按进程名 kill，也不停止外部 `frpc`。

## 2. 当前分支

```text
feature/phase-10a-desktop-foreground-frpc-run
```

Phase 10A 从已包含 Phase 9C 的 `develop` 创建。

## 3. 创建的文件

```text
docs/PHASE_10A_SUMMARY.md
```

## 4. 修改的文件

```text
src/Arturia.FrpNexus.Desktop/App.axaml.cs
src/Arturia.FrpNexus.Desktop/Composition/DesktopCompositionRoot.cs
src/Arturia.FrpNexus.Desktop/ViewModels/MainWindowViewModel.cs
src/Arturia.FrpNexus.Desktop/ViewModels/Pages/TunnelsPageViewModel.cs
src/Arturia.FrpNexus.Desktop/Views/MainWindow.axaml
src/Arturia.FrpNexus.Desktop/Views/MainWindow.axaml.cs
tests/Arturia.FrpNexus.Tests/Desktop/TunnelsPageViewModelTests.cs
```

## 5. Desktop start/stop 行为

隧道配置页新增运行控制区域：

1. 显示 `Stopped`、`Starting`、`Running`、`Stopping`、`Failed`。
2. 显示 active profile。
3. 显示最小运行摘要和错误/health message。
4. 点击“启动穿透”使用当前选中的 persisted profile。
5. 点击“停止穿透”只停止当前 Desktop 进程启动的 `frpc` 子进程。

启动前检查：

1. 必须选择 profile。
2. profile 必须 `Enabled`。
3. 必须通过 `IExcaliburTunnel.Validate`。
4. 当前仍只支持 TCP/UDP；HTTP/HTTPS 阻止启动。
5. 必须解析到 `frpcPath`。

GUI `frpcPath` 优先级：

```text
LiteDB settings.frpcPath -> FRPNEXUS_FRPC_PATH
```

GUI 不支持 `--frpc-path`，不搜索 PATH，不自动下载，不自动探测 `frpc`。

## 6. Desktop daemon wiring

Desktop composition root 现在注册：

```text
IAvalonDaemon -> FrpAvalonDaemon
```

Desktop GUI 通过：

```text
IAvalonDaemon.StartAsync(StartTunnelRequest)
IAvalonDaemon.StopAsync()
```

启动请求使用：

```text
StartTunnelRequest(Profile, FrpcPath, KeepGeneratedConfig: false)
```

`FrpAvalonDaemon` 的既有边界继续有效：只持有并停止当前 instance 启动的 `_process`，不搜索、attach 或 kill 外部 `frpc`。

## 7. 关闭/退出行为

1. 普通关闭窗口且启用“关闭窗口时最小化到托盘”时，窗口隐藏到托盘，Desktop 进程继续运行，当前 Desktop 启动的 `frpc` 子进程继续运行。
2. 托盘“退出”或应用 shutdown 时，best-effort 调用 `StopAsync`。
3. shutdown stop 只作用于当前 Desktop 进程内 daemon instance 管理的子进程。
4. 不留下后台 daemon。
5. 不控制任何外部 `frpc`。

## 8. UI 说明

UI 继续遵守 `DESIGN.md`：

1. 中文界面。
2. WinUI 3-inspired light theme。
3. 白色 card、弱边框、无阴影。
4. 未新增随机颜色。
5. “启动穿透”和“停止穿透”位于隧道页运行控制区域。
6. 文案明确当前是 Desktop foreground run，不是后台 daemon/service。
7. 停止文案明确只停止当前 Desktop 启动的 `frpc` 子进程。

Phase 10A 只显示最小状态摘要，不实现完整实时日志 UI。

## 9. 测试覆盖

扩展 Desktop ViewModel tests，使用 fake daemon、fake settings store 和 fake repository：

1. 使用 `settings.frpcPath` 启动 enabled TCP profile。
2. `settings.frpcPath` 为空时 fallback 到 `FRPNEXUS_FRPC_PATH`。
3. `settings.frpcPath` 优先于 `FRPNEXUS_FRPC_PATH`。
4. `frpcPath` 缺失时显示错误且不调用 `StartAsync`。
5. missing binary / daemon start failure 显示 `Failed`。
6. HTTP/HTTPS profile 阻止 `StartAsync`。
7. disabled profile 阻止 `StartAsync`。
8. `Starting` 状态阻止重复 start。
9. `Running` 状态下 stop 调用当前 fake daemon instance。
10. stop failure 显示中文错误。
11. edit/delete 不调用 `StopAsync`。
12. app shutdown 时 best-effort 调用 `StopAsync`。
13. stopped/tray-like idle state 不调用 `StopAsync`。
14. `MainWindowViewModel` shutdown path 委托到 tunnel page。

测试不访问真实用户 LiteDB，不启动 Desktop GUI，不启动真实 `frpc`，不调用 `systemctl`，不枚举 OS 进程，不 kill 外部进程。

## 10. 明确未实现内容

Phase 10A 明确未实现：

1. `frps`。
2. 后台 daemon。
3. 跨进程 status/stop。
4. systemd/service。
5. 自动下载 `frpc`。
6. PATH 搜索。
7. attach 外部进程。
8. 枚举 OS 进程。
9. kill 外部进程。
10. 按进程名 kill。
11. 完整实时日志 UI。
12. service unit 文件写入。
13. `systemctl` 调用。
14. CLI 行为修改。
15. 新增 NuGet package。
16. CI、安装器、自动更新、遥测。
17. 真实 `frpc` smoke test。
18. Desktop run smoke test。
19. Phase 10B 或其他阶段。
20. push。

## 11. 验证命令

Phase 10A 验证入口：

```powershell
dotnet restore "Arturia.FrpNexus.sln"
dotnet build "Arturia.FrpNexus.sln" --no-restore
dotnet test "Arturia.FrpNexus.sln" --no-build
```

不执行：

```powershell
dotnet run --project "src/Arturia.FrpNexus.Desktop/Arturia.FrpNexus.Desktop.csproj"
```

不执行真实 `frpc` smoke test，不执行 mutating CLI smoke test，不调用 `systemctl`。

## 12. 需要人工确认的 GUI 行为

1. 隧道页运行控制区域显示状态、active profile 和最小摘要。
2. 选择 enabled TCP/UDP profile 后，“启动穿透”按钮行为符合预期。
3. 未配置 `frpcPath` 时显示中文错误。
4. 启动失败时显示 `Failed` 和 daemon health/error。
5. “停止穿透”只停止当前 Desktop 启动的 `frpc` 子进程。
6. 关闭到托盘时当前 Desktop foreground run 继续运行。
7. 托盘“退出”或 app shutdown 时 best-effort 停止当前子进程。
