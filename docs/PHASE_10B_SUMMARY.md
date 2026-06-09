# Phase 10B Summary

## 1. Phase 10B 目标

Phase 10B 将 Desktop 运行日志页从静态 mock 内容改为绑定当前 Desktop 进程内同一个 `IAvalonDaemon` instance 的 runtime snapshot。

本阶段只读展示：

1. Runtime status。
2. Active profile。
3. Health message。
4. 最近 runtime logs。
5. 最后刷新时间。
6. 错误状态。

本阶段不新增启动能力，不扩展 daemon 边界，不修改 CLI，不新增 NuGet package。

## 2. 当前分支

```text
feature/phase-10b-desktop-runtime-logs-status
```

Phase 10B 从已包含 Phase 10A 的 `develop` 创建。

## 3. 创建的文件

```text
src/Arturia.FrpNexus.Desktop/ViewModels/Pages/RuntimeLogEntryViewModel.cs
tests/Arturia.FrpNexus.Tests/Desktop/LogsPageViewModelTests.cs
docs/PHASE_10B_SUMMARY.md
```

## 4. 修改的文件

```text
src/Arturia.FrpNexus.Desktop/ViewModels/MainWindowViewModel.cs
src/Arturia.FrpNexus.Desktop/ViewModels/Pages/LogsPageViewModel.cs
src/Arturia.FrpNexus.Desktop/ViewModels/Pages/TunnelsPageViewModel.cs
src/Arturia.FrpNexus.Desktop/Views/MainWindow.axaml
```

## 5. LogsPageViewModel 行为

`LogsPageViewModel` 现在注入 `IAvalonDaemon`，并提供：

1. `InitializeAsync`
2. `RefreshAsync`
3. `StartPolling`
4. `StopPolling`

`RefreshAsync` 只调用：

```text
IAvalonDaemon.GetSnapshotAsync
```

它不会调用 `StartAsync`、`StopAsync` 或 `RestartAsync`。

snapshot 读取失败时显示中文错误：

```text
无法读取当前 Desktop runtime 状态。
```

读取失败不会让应用崩溃，不会修复、重建、删除或迁移任何本地数据。

## 6. Runtime snapshot binding 行为

运行日志页展示当前 Desktop 进程内 foreground runtime：

1. `RuntimeStatus` 映射为中文：`已停止`、`启动中`、`运行中`、`停止中`、`失败`、`未知`。
2. `ActiveProfileId` 为空时显示 `无`。
3. `HealthMessage` 原样展示，空值时显示中文 fallback。
4. `LastRefreshText` 使用本地时间 `HH:mm:ss`。
5. 状态说明文案明确：这里只显示当前 Desktop 进程内 foreground runtime。

`MainWindowViewModel` 创建 `LogsPageViewModel(avalonDaemon)`，确保日志页与隧道页共享 Phase 10A 已注册的同一个 daemon instance。

## 7. DaemonLogEntry 展示行为

新增 `RuntimeLogEntryViewModel` 映射 `DaemonLogEntry`：

1. 本地时间格式：`HH:mm:ss`。
2. Level 映射：`Info -> INFO`、`Success -> SUCCESS`、`Warning -> WARN`、`Error -> ERROR`。
3. Display line 格式：

```text
[HH:mm:ss LEVEL] Source: Message
```

日志页按 snapshot 返回顺序展示最近日志。无日志时显示：

```text
暂无 runtime 日志。启动 Desktop foreground frpc 后会显示当前进程采集到的 stdout/stderr。
```

本阶段不做日志过滤、搜索、导出、复制、持久化、无限滚动或日志文件读取。

## 8. 轮询行为

运行日志页新增受控轮询：

1. 默认每 `1000ms` 调用一次 `GetSnapshotAsync`。
2. 使用 .NET BCL `PeriodicTimer`，未新增 NuGet package。
3. `MainWindowViewModel.InitializeAsync()` 初始化 Settings、Tunnels、Logs 后启动日志轮询。
4. `MainWindowViewModel.StopForegroundTunnelForShutdownAsync()` 先停止日志轮询，再委托 Phase 10A 的 tunnel shutdown stop。
5. 多次 `StartPolling()` 不重复创建 polling loop。
6. 多次 `StopPolling()` 不抛错。

## 9. UI 变更

运行日志页从静态终端 mock 升级为：

1. 顶部白色状态卡片，展示当前状态、Active profile、Health message、最后刷新时间和错误状态。
2. 下方终端日志区域，展示 `RuntimeLogEntryViewModel.DisplayLine`。
3. 空日志时展示中文空状态。

UI 继续遵守 `DESIGN.md`：

1. 中文界面。
2. WinUI 3-inspired light theme。
3. 白色 card。
4. 弱边框。
5. 无阴影。
6. 终端背景 `#1C1C1C`。
7. 终端字体 `Cascadia Code, Consolas`。
8. 不新增随机配色或复杂日志色彩系统。

## 10. 测试覆盖

新增 `LogsPageViewModelTests`，覆盖：

1. Logs 初始化读取 stopped snapshot。
2. Running snapshot 显示 active profile。
3. Running snapshot 显示 health message。
4. RuntimeStatus 中文映射。
5. Active profile 为空显示 `无`。
6. `Info/Success/Warning/Error` 映射为 `INFO/SUCCESS/WARN/ERROR`。
7. `DisplayLine` 格式正确。
8. 空日志显示中文空状态。
9. snapshot 读取失败显示中文错误，不崩溃。
10. `RefreshAsync` 只调用 `GetSnapshotAsync`。
11. `RefreshAsync` 不调用 `StartAsync`、`StopAsync`、`RestartAsync`。
12. `MainWindowViewModel.InitializeAsync()` 初始化 Logs page 并启动 polling。
13. `MainWindowViewModel.StopForegroundTunnelForShutdownAsync()` 停止日志轮询并继续委托 Phase 10A shutdown stop。
14. 多次 `StartPolling()` 幂等。
15. 多次 `StopPolling()` 不抛错。

测试使用 fake daemon，不启动真实 `frpc`，不访问真实 LiteDB，不调用 `systemctl`，不枚举或 kill OS 进程。

## 11. 明确未实现内容

Phase 10B 明确未实现：

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
11. 自动重连。
12. FRP Admin API。
13. 流量图表。
14. CLI 修改。
15. 新增 NuGet package。
16. Desktop run smoke test。
17. 真实 `frpc` smoke test。
18. mutating CLI smoke test。
19. `systemctl` 调用。
20. service install/start/enable。
21. packaging/deployment。
22. push。

## 12. 验证命令和验证结果

已执行：

```powershell
dotnet restore "Arturia.FrpNexus.sln"
dotnet build "Arturia.FrpNexus.sln" --no-restore
dotnet test "Arturia.FrpNexus.sln" --no-build
```

结果：

1. Restore 成功，所有项目均为最新。
2. Build 成功，`0` warning，`0` error。
3. Test 成功，`137` passed，`0` failed，`0` skipped。

未执行：

1. Desktop run smoke test。
2. 真实 `frpc` smoke test。
3. mutating CLI smoke test。
4. `run <profileId>`。
5. `systemctl`。
6. service install/start/enable。
7. packaging/deployment。
8. push。

## 13. 需要人工确认的 GUI 行为

1. 运行日志页顶部状态卡片显示当前状态、Active profile、Health message 和最后刷新时间。
2. 启动 Desktop foreground frpc 后，运行日志页显示当前 Desktop 进程内 daemon instance 采集到的 stdout/stderr 日志。
3. 停止当前 Desktop foreground frpc 后，运行日志页状态随 snapshot 刷新。
4. 无日志时显示中文空状态。
5. 日志页视觉符合 `DESIGN.md` 的白色状态卡片和深色终端区域要求。
6. 关闭应用时先停止日志轮询，再 best-effort 停止当前 Desktop 进程内 daemon instance 管理的子进程。
