# Phase 7B Summary

## 1. Phase 7B 目标

Phase 7B 只实现 Desktop GUI tray/minimize behavior，让用户可以显式启用“关闭窗口时最小化到托盘”，并通过托盘菜单显示、隐藏或退出 FrpNexus。

已确认目标：

1. 使用 Avalonia 内置 `TrayIcon` / `NativeMenu` 能力。
2. 增加托盘菜单：`显示 FrpNexus`、`隐藏到托盘`、`退出`。
3. 在设置页增加内存级开关：`关闭窗口时最小化到托盘`。
4. 默认关闭窗口仍直接退出。
5. 启用开关后，关闭窗口只隐藏窗口。
6. 托盘 `退出` 直接退出应用，并使用 explicit-exit flag 避免关闭拦截。
7. 不控制 `frpc` 或任何外部进程。

## 2. 当前分支

```text
feature/phase-7b-gui-tray-minimize
```

Phase 7B 从 `develop` 创建并切换到该 feature 分支实现。

## 3. 创建的文件

```text
docs/PHASE_7B_SUMMARY.md
```

## 4. 修改的文件

```text
src/Arturia.FrpNexus.Desktop/App.axaml.cs
src/Arturia.FrpNexus.Desktop/Views/MainWindow.axaml
src/Arturia.FrpNexus.Desktop/Views/MainWindow.axaml.cs
src/Arturia.FrpNexus.Desktop/ViewModels/MainWindowViewModel.cs
src/Arturia.FrpNexus.Desktop/ViewModels/Pages/SettingsPageViewModel.cs
```

## 5. Tray/window lifecycle 设计

`App.axaml.cs` 负责管理 tray lifecycle：

1. 创建 `TrayIcon`。
2. 创建 `NativeMenu`。
3. 菜单项包括 `显示 FrpNexus`、`隐藏到托盘`、`退出`。
4. `显示 FrpNexus` 调用 `Show()` 和 `Activate()`。
5. `隐藏到托盘` 调用 `Hide()`。
6. `退出` 设置 explicit-exit flag 后调用 `Shutdown()`。
7. 主窗口关闭事件会检查 `ShouldMinimizeToTrayOnClose`。
8. 如果未启用设置，关闭窗口直接退出。
9. 如果已启用设置且不是 explicit exit，则取消关闭并隐藏窗口。

## 6. 设置页行为

设置页新增：

```text
关闭窗口时最小化到托盘
```

说明文案：

```text
该功能只影响窗口显示，不会启动、停止或后台运行穿透进程。应用重启后默认恢复为直接退出。
```

该设置为内存级：

1. 不持久化。
2. 不新增配置文件。
3. 不新增数据库。
4. 应用重启后默认恢复为直接退出。

## 7. 图标资源状态

当前 Desktop 项目中未发现可复用 `.ico`、`.png`、`.svg`、`.jpg` 或 `.jpeg` 图标资源。

Phase 7B 遵守边界：

1. 未新增二进制图标资源。
2. 未生成图标文件。
3. 未使用动漫图片、Fate 素材、角色图或任何版权素材。
4. 未修改 `.csproj` 引用 asset。

限制：

1. 当前 `TrayIcon` 未绑定自定义图标资源。
2. 托盘图标在不同平台上的显示可能受 Avalonia 和系统托盘实现限制。
3. 如后续平台要求必须提供图标资源，应单独规划一个非侵权抽象图标 asset 阶段。

## 8. 如何避免触碰 Phase 7A/systemd/service 边界

Phase 7B 未修改：

1. `src/Arturia.FrpNexus.Cli/**`
2. `src/Arturia.FrpNexus.Infrastructure/InvisibleAirService/**`
3. `docs/PHASE_7A_SUMMARY.md`

Phase 7B 未实现：

1. `systemd` install/enable/start/stop。
2. `systemctl` 调用。
3. service unit 文件写入。
4. Windows Service。
5. macOS LaunchAgent。

## 9. 如何避免控制真实 frpc 进程

Phase 7B 只控制 Desktop window visibility。

明确未做：

1. 未调用 `IAvalonDaemon.StartAsync`。
2. 未调用 `IAvalonDaemon.StopAsync`。
3. 未调用 `IAvalonDaemon.RestartAsync`。
4. 未调用 `Process.Start`。
5. 未搜索、kill、attach 或接管任何 `frpc`、`frps` 或外部进程。
6. 未修改 Phase 6 `run <profileId>` 前台阻塞语义。

## 10. 验证命令和结果

### 10.1 Solution build

```powershell
dotnet build "Arturia.FrpNexus.sln" --no-restore
```

结果：成功，`0` warning，`0` error。

### 10.2 Desktop launch smoke check

```powershell
dotnet run --project "src/Arturia.FrpNexus.Desktop/Arturia.FrpNexus.Desktop.csproj"
```

结果：命令启动后未立即崩溃、无错误输出。由于 Desktop GUI 进程会持续运行，验证命令在 10 秒超时后由工具终止；托盘菜单和窗口交互仍需要人工确认。

GUI 手动验证项需要人工确认：

1. 默认关闭窗口会退出应用。
2. 启用“关闭窗口时最小化到托盘”后，关闭窗口只隐藏。
3. 托盘菜单“显示 FrpNexus”可以恢复窗口。
4. 托盘菜单“隐藏到托盘”可以隐藏窗口。
5. 托盘菜单“退出”可以退出应用。
6. 未执行 CLI service 命令。
7. 未执行 `systemctl`。
8. 未启动或停止真实 `frpc`。

## 11. 明确未实现内容

Phase 7B 明确未实现：

1. CLI service 命令修改。
2. Phase 7A systemd preview 逻辑修改。
3. `systemctl` 调用。
4. service unit 文件写入。
5. Windows Service。
6. macOS LaunchAgent。
7. systemd service install/enable/start/stop。
8. 启动、停止、kill、attach、搜索或接管任何外部进程。
9. `IAvalonDaemon` start/stop/restart 调用。
10. `Process.Start` 调用。
11. 新增 NuGet package。
12. `.csproj` 修改。
13. 二进制图标资源新增。
14. 持久化配置文件。
15. 数据库、CI、安装器、自动更新、遥测。
16. Phase 8。

## 12. Phase 8 入口建议

不从 Phase 7B 自动进入 Phase 8。

后续若继续，应先：

1. Review Phase 7B 行为。
2. 完成人工 GUI smoke test。
3. 提交并按确认流程合入 `develop`。
4. 重新执行 Git preflight。
5. 输出新的 mini-plan 并等待用户明确批准。
