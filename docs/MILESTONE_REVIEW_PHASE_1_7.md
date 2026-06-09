# Milestone Review: Phase 1-7

> Historical snapshot: this document records the repository state through Phase 7B only.
> After Phase 8A/8B/8C/8E, the current implementation and documentation status is tracked in `docs/MILESTONE_REVIEW_PHASE_1_8.md`.

## 1. 当前已完成阶段概览

FrpNexus 当前已从项目骨架推进到 Phase 7B。

已完成阶段：

1. Phase 1 / 1.5：solution/project skeleton 与基础双入口。
2. Phase 2：Avalonia 设计系统基础。
3. Phase 3：Desktop 主窗口、导航和静态占位页面。
4. Phase 4：Core domain interfaces 与 mock implementations。
5. Phase 5：Cocona CLI command structure。
6. Phase 6：最小真实 `frpc` client foreground integration。
7. Phase 7A：CLI/systemd user service unit preview-only。
8. Phase 7B：Desktop tray/minimize window behavior。

当前主分支状态：`develop` 已包含 Phase 7B。

## 2. 每个 Phase 的 commit 和交付内容

### Phase 1 / 1.5

Commit：`913262d chore: establish FrpNexus baseline through phase 2`

主要交付：

1. 创建 `Arturia.FrpNexus.sln`。
2. 创建 `Core`、`Application`、`Infrastructure`、`Cli`、`Desktop` 项目。
3. 建立 `Arturia.FrpNexus` 命名空间方向。
4. 创建 Avalonia Desktop 基础入口。
5. 创建 Cocona CLI 基础入口。
6. 固定 .NET/Avalonia/Cocona 基础依赖。

### Phase 2

Commit：`913262d chore: establish FrpNexus baseline through phase 2`

主要交付：

1. 将 `DESIGN.md` 的颜色、字体、圆角、间距转为 Avalonia resources/styles。
2. 创建 `Colors.axaml`、`Typography.axaml`、`Layout.axaml`、`Controls.axaml`。
3. 加载 Fluent theme 和 FrpNexus design resources。
4. 建立白色卡片、弱边框、accent button、TextBox、terminal container 基础样式。
5. 保持静态 UI，不接入真实业务。

### Phase 3

Commit：`550b8a8 feat: implement phase 3 main window navigation shell`

主要交付：

1. 创建 Desktop ViewModel 基础结构。
2. 实现主窗口两栏布局。
3. 添加左侧导航。
4. 添加工作台、隧道配置、运行日志、设置占位页。
5. 使用中文界面文本和 mock/static 数据。
6. 复用 Phase 2 design system。

### Phase 4

Commit：`20d4417 feat: add phase 4 core domain interfaces`

主要交付：

1. 定义 `AvalonDaemon` 接口、状态和日志模型。
2. 定义 `ExcaliburTunnel` profile、协议、校验和接口。
3. 定义 `InvisibleAirService` service/tray/status 模型和接口。
4. 添加 `MockAvalonDaemon`、`MockExcaliburTunnel`、`MockInvisibleAirService`。
5. 明确 mock 行为不代表真实 FRP/service/tray 能力。

### Phase 5

Commit：`bd730ff feat: add phase 5 cli command structure`

主要交付：

1. 建立 Cocona command structure。
2. 添加 root-level `run <profileId>` mock command。
3. 添加 `daemon`、`tunnel`、`service` 分组命令。
4. 通过 DI 注册 Phase 4 Core interfaces 和 mock implementations。
5. CLI 输出明确标记 structure-only / mock mode。

### Phase 6

Commit：`771d1d6 feat: add phase 6 frpc client integration`

主要交付：

1. 添加真实 `frpc` foreground process integration。
2. `run <profileId>` 支持 `--frpc-path` 和 `FRPNEXUS_FRPC_PATH` fallback。
3. 使用 `System.Diagnostics.Process` 启动当前 CLI 进程管理的 `frpc` 子进程。
4. 采集 stdout/stderr 并映射为 `DaemonLogEntry`。
5. 生成 OS temp 下的临时 TOML 配置。
6. 实现 TCP/UDP 最小 TOML serializer。
7. 实现受限 TOML parser。
8. 明确不支持跨进程 daemon status/stop。

### Phase 7A

Commit：`75c65d2 feat: add phase 7a systemd preview commands`

主要交付：

1. 扩展 `IInvisibleAirService`，支持 systemd unit preview request。
2. 添加 `SystemdServiceUnitRequest`、`SystemdServiceUnitPreview`、`ServicePlatform`。
3. 添加 `SystemdServiceUnitBuilder`。
4. 添加 `LinuxInvisibleAirService`。
5. CLI 增加 `service status`、`service explain`、`service preview`。
6. 只输出 Linux user-level systemd unit preview 到 stdout。
7. 明确不写 unit 文件、不调用 `systemctl`、不启用或启动 service。

### Phase 7B

Commit：`39301fa feat: add phase 7b gui tray minimize behavior`

主要交付：

1. 使用 Avalonia `TrayIcon` / `NativeMenu`。
2. 添加托盘菜单：`显示 FrpNexus`、`隐藏到托盘`、`退出`。
3. 设置页增加内存级开关：`关闭窗口时最小化到托盘`。
4. 默认关闭窗口仍直接退出。
5. 启用开关后，关闭窗口只隐藏窗口。
6. 托盘 `退出` 使用 explicit-exit flag 直接退出应用。
7. 不控制 `frpc` 或任何外部进程。

## 3. 当前架构状态

当前 solution/project 结构：

```text
Arturia.FrpNexus.sln
src/
  Arturia.FrpNexus.Core/
  Arturia.FrpNexus.Application/
  Arturia.FrpNexus.Infrastructure/
  Arturia.FrpNexus.Cli/
  Arturia.FrpNexus.Desktop/
```

项目边界：

1. `Core`：领域模型和接口，无项目引用。
2. `Application`：引用 `Core`，当前仍是轻量边界。
3. `Infrastructure`：引用 `Core` 和 `Application`，包含 FRP、TOML、mock、systemd preview 实现。
4. `Cli`：引用 `Application` 和 `Infrastructure`，使用 Cocona。
5. `Desktop`：引用 `Application` 和 `Infrastructure`，使用 Avalonia。

领域模块状态：

1. `AvalonDaemon`：已有真实 `frpc` foreground process adapter。
2. `ExcaliburTunnel`：已有最小 TCP/UDP TOML serializer/parser/validation。
3. `InvisibleAirService`：已有 systemd preview 和 Desktop tray/window behavior，但尚无真实 service install。

## 4. 当前 UI 状态

当前 Desktop UI 状态：

1. WinUI 3-inspired light theme。
2. 中文界面。
3. Mica-like fallback background。
4. 白色 card、弱边框、无投影。
5. 主强调色 `#D4A017`。
6. Windows Terminal 风格日志容器。
7. 左侧导航和右侧内容区。
8. 工作台、隧道配置、运行日志、设置页面仍以 mock/static 数据为主。
9. 设置页包含内存级 tray minimize toggle。
10. 托盘菜单可显示、隐藏、退出窗口。

当前 UI 尚未完成：

1. GUI 未接入真实 tunnel profile repository。
2. GUI 未接入真实 `IAvalonDaemon` start/stop/restart。
3. GUI 未接入真实日志流。
4. 托盘没有自定义图标资源。
5. 托盘行为仍需人工验证。

## 5. 当前 CLI 能力

当前 CLI 命令结构：

```text
run <profileId>
daemon status
daemon stop
tunnel validate
tunnel preview
service status
service explain
service preview <profileId> --frpnexus-path <path> --frpc-path <path>
```

能力说明：

1. `run <profileId>` 可以前台模式启动当前 CLI 进程管理的 `frpc` client。
2. `daemon status` / `daemon stop` 明确不支持跨进程 daemon 状态或停止。
3. `tunnel validate` 执行 TCP/UDP 最小 validation。
4. `tunnel preview` 输出最小 TCP/UDP FRP TOML preview。
5. `service status` / `explain` 输出 Phase 7A service preview 边界说明。
6. `service preview` 只生成 Linux user-level systemd unit preview。

## 6. 当前 FRP 集成能力

当前已实现：

1. 只支持 `frpc` client。
2. 只支持当前 CLI 进程启动和管理的 `frpc` 子进程。
3. 只支持 foreground blocking run。
4. 支持 `Ctrl+C` cancellation 时尽量停止当前子进程。
5. 支持 stdout/stderr 日志采集。
6. 支持 TCP/UDP 最小 TOML 生成。
7. 支持受限 TOML parse。
8. 支持 missing binary negative path。

当前未实现：

1. `frps` server。
2. HTTP/HTTPS tunnel。
3. 完整 TOML parser。
4. 自动下载 `frpc`。
5. PATH 自动搜索。
6. 跨进程 daemon status/stop。
7. 后台常驻 daemon。
8. GUI 启停真实 `frpc`。

## 7. 当前 systemd preview 能力

当前已实现：

1. Linux user-level systemd unit preview。
2. `service preview` 必须显式接收 `<profileId>`、`--frpnexus-path`、`--frpc-path`。
3. `ExecStart` 使用显式 frpnexus path 和 frpc path。
4. 只输出 preview 到 stdout。
5. `service status` 只做 .NET 平台检测和说明性输出。
6. `service explain` 输出安全边界说明。

明确未实现：

1. 不写 unit 文件。
2. 不写 `~/.config/systemd/user`。
3. 不写 `/etc/systemd/system`、`/usr/lib/systemd/system`、`/lib/systemd/system`。
4. 不调用 `systemctl`。
5. 不执行 daemon-reload/enable/start/stop/status。
6. 不调用 sudo/pkexec/UAC/PowerShell elevation。
7. 不实现 system-level service。
8. 不实现 Windows Service 或 macOS LaunchAgent。

## 8. 当前 tray/minimize 能力

当前已实现：

1. Desktop 启动后创建 Avalonia `TrayIcon`。
2. 托盘菜单包含 `显示 FrpNexus`、`隐藏到托盘`、`退出`。
3. 默认关闭窗口直接退出。
4. 用户启用 `关闭窗口时最小化到托盘` 后，关闭窗口只隐藏。
5. `显示 FrpNexus` 调用 `Show()` 和 `Activate()`。
6. `隐藏到托盘` 调用 `Hide()`。
7. `退出` 使用 explicit-exit flag 并调用 shutdown。

限制：

1. 当前没有自定义托盘图标资源。
2. TrayIcon 在不同平台的显示可能受系统托盘环境影响。
3. 设置为内存级，不持久化。
4. 托盘行为只影响窗口显示，不代表 tunnel 或 `frpc` 在后台运行。

## 9. 明确仍未实现的内容

仍未实现：

1. GUI 真实 profile CRUD。
2. GUI 真实 tunnel validation/preview/save。
3. GUI 真实启动/停止/restart `frpc`。
4. GUI 真实日志流绑定。
5. 配置持久化。
6. Profile repository。
7. 完整 TOML parser。
8. HTTP/HTTPS tunnel。
9. `frps` server。
10. 自动下载 `frpc`。
11. PATH 自动搜索。
12. 后台 daemon。
13. 跨进程 daemon status/stop。
14. systemd install/enable/start/stop。
15. Windows Service。
16. macOS LaunchAgent。
17. 托盘图标 asset。
18. 自动化测试项目。
19. CI workflow。
20. 打包、安装器、发布脚本。
21. 自动更新。
22. 遥测或分析。
23. 数据库。

## 10. 验证命令

当前已使用过的验证命令：

```powershell
dotnet restore "Arturia.FrpNexus.sln"
dotnet build "Arturia.FrpNexus.sln" --no-restore
dotnet run --project "src/Arturia.FrpNexus.Desktop/Arturia.FrpNexus.Desktop.csproj"
dotnet run --project "src/Arturia.FrpNexus.Cli/Arturia.FrpNexus.Cli.csproj" -- --help
dotnet run --project "src/Arturia.FrpNexus.Cli/Arturia.FrpNexus.Cli.csproj" -- run my-server --frpc-path "Z:\frpnexus-missing\frpc.exe"
dotnet run --project "src/Arturia.FrpNexus.Cli/Arturia.FrpNexus.Cli.csproj" -- tunnel validate
dotnet run --project "src/Arturia.FrpNexus.Cli/Arturia.FrpNexus.Cli.csproj" -- tunnel preview
dotnet run --project "src/Arturia.FrpNexus.Cli/Arturia.FrpNexus.Cli.csproj" -- service status
dotnet run --project "src/Arturia.FrpNexus.Cli/Arturia.FrpNexus.Cli.csproj" -- service explain
dotnet run --project "src/Arturia.FrpNexus.Cli/Arturia.FrpNexus.Cli.csproj" -- service preview my-server --frpnexus-path "/opt/frpnexus/frpnexus" --frpc-path "/opt/frp/frpc"
```

验证说明：

1. `dotnet build` 是当前主要自动化验证入口。
2. CLI smoke checks 已覆盖 help、tunnel、service preview 和 missing binary negative test。
3. Desktop run 只能证明启动后未立即崩溃，不等同于自动化 UI 测试。
4. 托盘菜单和窗口行为需要人工确认。
5. 未执行真实 `frpc` smoke test，因为没有提供可用 `frpc` binary path。

## 11. 风险和限制

主要风险：

1. 当前没有自动化测试项目，回归风险较高。
2. Desktop GUI 大量页面仍是 static/mock。
3. `FrpAvalonDaemon` 只维护进程内状态，CLI 进程退出后状态丢失。
4. `daemon status` / `daemon stop` 不支持跨进程后台控制。
5. `FrpAvalonDaemon.StopAsync` 使用 `Kill(entireProcessTree: true)` 停止当前子进程，后续需要更优雅的 shutdown 策略。
6. TOML parser 只覆盖本项目生成的最小子集。
7. systemd 只 preview，不安装、不启用、不启动。
8. TrayIcon 没有自定义图标资源，平台显示不确定。
9. GUI 未接入真实 Core/Application use cases。
10. 缺少配置持久化和 profile repository。

## 12. 技术债

当前技术债：

1. `AGENTS.md` 顶部仍称仓库是 documentation-only，已与当前事实不一致。
2. `SPC.md` 的当前状态段落仍包含旧阶段状态描述。
3. `Application` 项目尚未承载明确 use case orchestration。
4. Core interfaces 已被 CLI 直接使用，但 GUI 尚未接入。
5. Mock/static GUI 与真实 CLI/Infrastructure 能力之间尚未打通。
6. 没有测试项目。
7. 没有统一配置路径策略。
8. 没有 profile storage abstraction。
9. 没有日志订阅/流式 UI binding。
10. 没有平台差异测试。

## 13. 推荐下一阶段路线

推荐候选路线：

1. Phase 8E：文档状态同步。
   目标：更新 `AGENTS.md`、`SPC.md` 和相关当前状态描述，使其匹配 Phase 1-7 实际成果。

2. Phase 8D：测试基础。
   目标：新增测试项目，覆盖 TOML serializer/parser、validation、systemd unit builder 等纯逻辑。

3. Phase 8A：配置持久化与 Profile repository。
   目标：建立 tunnel profile 的本地文件存储和 repository abstraction，为 GUI/CLI 共享真实 profile 数据做准备。

4. Phase 8B：Desktop GUI 接入 tunnel preview/validate。
   目标：让 GUI 隧道配置页使用真实 `IExcaliburTunnel` 能力，但暂不启动真实 `frpc`。

5. Phase 8C：Desktop GUI 接入当前窗口生命周期内的 `frpc` start/stop。
   目标：形成 Desktop 最小真实穿透闭环。

建议优先级：

1. 若目标是降低后续误判风险：优先 Phase 8E。
2. 若目标是提高工程安全性：优先 Phase 8D。
3. 若目标是推进产品可用性：优先 Phase 8A，然后 Phase 8B。
