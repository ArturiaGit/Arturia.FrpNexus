# Phase 10C Summary

## 1. Phase 10C 目标

Phase 10C 对 Desktop runtime UX 做 hardening，不新增底层 runtime 能力。

本阶段只优化当前 Desktop 前台运行体验：

1. 隧道页运行状态中文化。
2. 启动、停止、错误和边界说明文案优化。
3. 运行日志页新增手动刷新按钮。
4. 运行日志页补充刷新状态、空状态和错误状态说明。
5. 统一关闭窗口、托盘和退出应用的用户可见说明。

## 2. 当前分支

```text
feature/phase-10c-desktop-runtime-ux-hardening
```

Phase 10C 从已包含 Phase 10B 的 `develop` 创建。

## 3. 创建的文件

```text
docs/PHASE_10C_SUMMARY.md
```

## 4. 修改的文件

```text
src/Arturia.FrpNexus.Desktop/ViewModels/Pages/TunnelsPageViewModel.cs
src/Arturia.FrpNexus.Desktop/ViewModels/Pages/LogsPageViewModel.cs
src/Arturia.FrpNexus.Desktop/Views/MainWindow.axaml
src/Arturia.FrpNexus.Desktop/Views/MainWindow.axaml.cs
tests/Arturia.FrpNexus.Tests/Desktop/TunnelsPageViewModelTests.cs
tests/Arturia.FrpNexus.Tests/Desktop/LogsPageViewModelTests.cs
```

## 5. Desktop UX hardening 行为

隧道页运行控制区现在使用中文状态：

```text
已停止
启动中
运行中
停止中
失败
未知
```

启动、停止、删除配置和运行摘要文案统一说明：

1. 当前只管理本 Desktop 进程启动的前台 `frpc` 子进程。
2. 当前不是后台守护服务。
3. 不会查找、接管或结束外部 `frpc`。
4. 缺少 `frpcPath` 时提示去设置页配置或设置 `FRPNEXUS_FRPC_PATH`。
5. 启动成功后提示可到运行日志页查看当前进程日志。

## 6. 运行日志页刷新体验

运行日志页新增“手动刷新”按钮。

按钮只调用：

```text
LogsPageViewModel.RefreshAsync()
```

`RefreshAsync()` 仍只调用：

```text
IAvalonDaemon.GetSnapshotAsync()
```

它不会调用 `StartAsync`、`StopAsync` 或 `RestartAsync`。

运行日志页现在显示：

1. 当前状态。
2. 当前配置。
3. 运行摘要。
4. 最后刷新时间。
5. 错误状态。
6. 刷新状态说明。
7. 当前 Desktop 前台运行边界说明。

无日志时显示中文空状态，说明只有启动当前 Desktop 前台 `frpc` 后才会显示本进程采集到的 stdout/stderr。

## 7. 关闭窗口、托盘和退出说明

设置页托盘说明更新为：

1. 关闭窗口且启用托盘设置时只隐藏窗口。
2. 当前 Desktop 前台运行会继续保留。
3. 托盘“退出”或应用退出时会尽力停止当前 Desktop 管理的 `frpc` 子进程。

本阶段不改变 Phase 10A 已有 shutdown 行为，只统一用户可见文案。

## 8. 测试覆盖

扩展 Desktop ViewModel tests：

1. 隧道页 runtime status 中文映射。
2. 启动成功后显示中文运行状态和日志页提示。
3. 缺少 `frpcPath` 时显示中文错误和不搜索 PATH 边界。
4. 启动中阻止重复启动并保留中文状态。
5. 停止成功/失败显示中文状态和外部 `frpc` 边界。
6. 删除配置文案说明不停止当前 Desktop 前台运行。
7. 日志页刷新成功显示刷新状态。
8. 日志页刷新失败显示中文错误和刷新失败状态。
9. 日志页空状态说明当前 Desktop 前台运行日志。
10. 手动刷新路径只读取 snapshot，不调用 start/stop/restart。

测试继续使用 fake daemon、fake repository 和 fake settings store，不访问真实用户 LiteDB，不启动 Desktop GUI，不启动真实 `frpc`，不调用 `systemctl`。

## 9. 明确未实现内容

Phase 10C 明确未实现：

1. `frps`。
2. 后台 daemon。
3. 跨进程 status/stop。
4. systemd/service install/start/enable。
5. 自动下载 `frpc`。
6. PATH 搜索。
7. attach 外部进程。
8. 枚举或 kill OS 进程。
9. 自动重连。
10. FRP Admin API。
11. 流量图表。
12. CLI 修改。
13. 新增 NuGet package。
14. Desktop run smoke test。
15. 真实 `frpc` smoke test。
16. mutating CLI smoke test。
17. CI、安装器、自动更新、遥测。
18. Phase 10D 或 Phase 11。
19. push。

## 10. 验证命令

Phase 10C 已执行：

```powershell
dotnet restore "Arturia.FrpNexus.sln"
dotnet build "Arturia.FrpNexus.sln" --no-restore
dotnet test "Arturia.FrpNexus.sln" --no-build
```

结果：

1. Restore 通过，所有项目均为最新。
2. Build 通过，`0` warning，`0` error。
3. Test 通过，`144` passed，`0` failed，`0` skipped。

默认不执行：

```powershell
dotnet run --project "src/Arturia.FrpNexus.Desktop/Arturia.FrpNexus.Desktop.csproj"
```

不执行真实 `frpc` smoke test，不执行 mutating CLI smoke test，不调用 `systemctl`，不 push。

## 11. 需要人工确认的 GUI 行为

1. 隧道页运行控制区状态显示为中文。
2. 启动、停止和错误文案清楚表达当前 Desktop 前台运行边界。
3. 运行日志页“手动刷新”按钮刷新当前状态卡片和日志，不触发启动/停止。
4. 空日志状态和刷新失败状态显示清晰。
5. 设置页托盘说明与关闭/退出行为一致。
6. 页面视觉仍符合 `DESIGN.md`：浅色中文界面、白色 card、弱边框、无阴影、终端背景 `#1C1C1C`。
