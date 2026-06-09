# Milestone Review: Phase 1-10

## 1. 文档定位

本文档记录 FrpNexus 从 Phase 1 到 Phase 10D 后的当前状态。它取代 `docs/MILESTONE_REVIEW_PHASE_1_8.md` 作为最新阶段快照；Phase 1-8 review 保留为历史记录，不再代表 Phase 9A/9B/9C/10A/10B/10C/10D 后的最新事实。

Phase 10D 仅同步文档状态，不修改源码、测试、solution、project 文件，不新增依赖，不改变 CLI 或 Desktop 行为，不提交、不合并、不 push，也不进入 Phase 11。

## 2. Phase 1-10 已完成内容

已完成阶段：

1. Phase 1 / 1.5：创建 .NET solution、SDK baseline、Core/Application/Infrastructure/Cli/Desktop 项目和基础双入口。
2. Phase 2：建立 Avalonia design system foundation，转译 `DESIGN.md` 的颜色、字体、圆角、间距和基础控件样式。
3. Phase 3：实现 Desktop main window/navigation shell，添加工作台、隧道配置、运行日志、设置等中文静态/mock 页面。
4. Phase 4：定义 Core domain interfaces 和 mock implementations，覆盖 `AvalonDaemon`、`ExcaliburTunnel`、`InvisibleAirService` 的领域边界。
5. Phase 5：建立 Cocona CLI command structure，保留 GUI/CLI 双模方向。
6. Phase 6：实现最小真实 `frpc` foreground client integration，以及 TCP/UDP 最小 TOML serializer/parser/validation。
7. Phase 7A：实现 CLI Linux user-level systemd unit preview-only 能力。
8. Phase 7B：实现 Desktop tray/minimize window behavior。
9. Phase 8C：新增 xUnit tests 项目和首批自动化测试基础。
10. Phase 8A：新增 LiteDB settings/profile persistence foundation。
11. Phase 8B：新增 CLI config/profile commands，并让 `run <profileId>` 从 LiteDB profile repository 查找 profile。
12. Phase 8E：同步 Phase 1-8 文档状态。
13. Phase 9A：Desktop GUI 接入 LiteDB settings/profile persistence，支持 settings 保存、托盘设置持久化、profile 只读列表和错误状态。
14. Phase 9B：Desktop GUI 支持 LiteDB profile 新增、编辑、删除和刷新。
15. Phase 9C：Desktop GUI 支持表单 validation 和 TCP/UDP 只读 TOML preview。
16. Phase 10A：Desktop GUI 支持当前 Desktop 进程内 foreground `frpc` start/stop。
17. Phase 10B：Desktop Logs 页面绑定当前 Desktop process daemon snapshot，展示 runtime status、active profile、health message 和 recent logs。
18. Phase 10C：Desktop runtime UX hardening，补充中文运行状态、手动刷新、空状态/错误状态和 foreground runtime 边界说明。
19. Phase 10D：同步 Phase 1-10 文档状态，补齐本 milestone review。

## 3. 当前真实能力清单

当前仓库已经不是 documentation-only。已有：

```text
Arturia.FrpNexus.sln
src/
  Arturia.FrpNexus.Core/
  Arturia.FrpNexus.Application/
  Arturia.FrpNexus.Infrastructure/
  Arturia.FrpNexus.Cli/
  Arturia.FrpNexus.Desktop/
tests/
  Arturia.FrpNexus.Tests/
```

当前技术栈：

1. Runtime：.NET 8。
2. Desktop：Avalonia。
3. CLI：Cocona。
4. Local persistence：LiteDB。
5. Tests：xUnit。

当前已实现能力：

1. Avalonia Desktop shell、设计系统、主窗口导航和中文页面。
2. Cocona CLI command structure。
3. 最小真实 `frpc` foreground client integration。
4. TCP/UDP 最小 TOML serializer/parser/validation。
5. LiteDB settings/profile persistence foundation。
6. CLI config/profile commands。
7. CLI `run <profileId>` 读取 LiteDB profile 后运行当前 CLI 进程 foreground `frpc` client。
8. CLI Linux user-level systemd unit preview。
9. Desktop tray/minimize window behavior。
10. xUnit 自动化测试项目。
11. Desktop settings persistence，包括 `frpcPath` 和 `minimizeToTrayOnClose`。
12. Desktop GUI profile CRUD，包括新增、编辑、删除、刷新。
13. Desktop GUI validation 和只读 TCP/UDP TOML preview。
14. Desktop GUI foreground `frpc` start/stop，仅管理当前 Desktop 进程内 daemon instance 启动的子进程。
15. Desktop Logs 页面绑定当前 Desktop runtime snapshot/logs。
16. Desktop runtime UX hardening，包括中文状态、手动刷新、空状态、错误状态和边界说明。

## 4. 当前 Desktop GUI 状态

当前 Desktop GUI 已从静态/mock 页面演进为能消费本地 LiteDB 和当前进程 foreground runtime 的 GUI：

1. 设置页读取/保存 `frpcPath`。
2. 设置页读取/保存 `minimizeToTrayOnClose`。
3. 关闭窗口到托盘行为使用持久化后的托盘设置。
4. 隧道配置页读取、创建、编辑、删除、刷新 LiteDB profiles。
5. 隧道配置页在保存、校验和预览前调用当前 TCP/UDP validation 边界。
6. 隧道配置页显示只读 TOML preview，不写 TOML 文件。
7. 隧道配置页可以启动/停止当前 Desktop 进程内 foreground `frpc` 子进程。
8. 运行日志页展示当前 Desktop process daemon snapshot 和 recent logs。
9. 运行日志页支持手动刷新，刷新只读取 snapshot。

Desktop GUI 当前仍不是后台 daemon，不会 attach 外部 `frpc`，不会枚举或 kill OS 进程，不会跨进程 status/stop，不会安装 service。

## 5. 当前 CLI 能力

当前 CLI 已包含：

1. Root-level `run <profileId>`。
2. `config` commands。
3. `profile` commands。
4. `tunnel` preview/validation commands。
5. `service` preview/explain/status-style commands。
6. `daemon` command group structure。

CLI config/profile 已接入 LiteDB：

1. `config show`
2. `config get frpc-path`
3. `config set frpc-path <path>`
4. `profile list`
5. `profile show <id>`
6. `profile add <id> ...`
7. `profile remove <id>`

`run <profileId>` 当前行为：

1. 从 LiteDB `ITunnelProfileRepository` 查找 profile。
2. 找不到 profile 时失败。
3. 不 fallback 到默认 profile。
4. 找不到时提示先创建持久化 profile。
5. 使用当前 CLI 进程 foreground 模式运行 `frpc` client。

`frpcPath` 优先级：

```text
--frpc-path -> LiteDB settings.frpcPath -> FRPNEXUS_FRPC_PATH
```

CLI 不搜索 PATH，不自动下载 `frpc`，不安装 service，不提供跨进程 daemon control。

## 6. 当前 FRP / Runtime 边界

当前 FRP 集成是最小可用边界：

1. CLI 支持当前 CLI 进程 foreground 模式启动 `frpc` client。
2. Desktop 支持当前 Desktop 进程 daemon instance foreground 模式启动/停止 `frpc` 子进程。
3. 支持使用项目内生成的 TCP/UDP 最小 TOML 配置。
4. 支持 TCP/UDP profile validation。
5. 支持解析本项目 serializer 生成的最小 TOML 子集。
6. Desktop Logs 页面读取当前 Desktop daemon snapshot 和 recent logs。

当前不支持：

1. 后台 daemon。
2. 跨进程 status/stop。
3. attach 外部进程。
4. 枚举或 kill OS 进程。
5. `frps` server。
6. 自动下载 `frpc`。
7. PATH 自动搜索。
8. 完整 FRP/TOML 兼容。
9. HTTP/HTTPS tunnel 完整字段支持。
10. 自动重连。
11. FRP Admin API 或流量图表。

## 7. 当前 LiteDB 持久化能力

当前已有 LiteDB settings/profile persistence：

1. `FrpNexusSettings` model。
2. `IFrpNexusSettingsStore` 与 LiteDB implementation。
3. `ITunnelProfileRepository` 与 LiteDB implementation。
4. `FrpNexusDatabasePathProvider`。
5. `SettingsService`。
6. `TunnelProfileService`。

当前 LiteDB database 文件名：

```text
frpnexus.db
```

平台路径策略：

```text
Windows: %APPDATA%\Arturia\FrpNexus\frpnexus.db
Linux: $XDG_CONFIG_HOME/frpnexus/frpnexus.db，fallback ~/.config/frpnexus/frpnexus.db
macOS: ~/Library/Application Support/Arturia/FrpNexus/frpnexus.db
```

当前 collections：

1. `settings`：固定 `_id = "default"`。
2. `tunnel_profiles`：以 `TunnelProfile.Id` 作为 `_id`。

Desktop 和 CLI 当前都会消费这些 persistence 边界。Desktop run/smoke test 可能访问真实用户 LiteDB 路径，除非后续引入显式临时路径策略。

## 8. 当前测试能力

当前已有 xUnit tests 项目：

```text
tests/Arturia.FrpNexus.Tests/
```

测试覆盖包括：

1. TOML serializer/parser 的最小 TCP/UDP 子集。
2. `FrpExcaliburTunnel` validation。
3. systemd unit preview builder。
4. `CliProfileFactory`。
5. LiteDB settings store。
6. LiteDB tunnel profile repository。
7. database path provider / connection factory。
8. CLI config/profile/run 逻辑。
9. Desktop settings persistence ViewModel 行为。
10. Desktop profile CRUD ViewModel 行为。
11. Desktop validation / TOML preview ViewModel 行为。
12. Desktop foreground start/stop ViewModel 行为。
13. Desktop logs/status binding ViewModel 行为。
14. Desktop runtime UX hardening 行为。

测试使用 fake services、fake daemon、fake repository 或临时 LiteDB 文件；不启动真实 `frpc`，不调用 `systemctl`，不枚举或 kill OS 进程。

## 9. 当前 systemd / tray 能力

当前 systemd 能力仅限 preview：

1. 生成 Linux user-level systemd unit preview。
2. 校验必要路径和 profile id。
3. 输出 safety notes。
4. 不写入 unit file。
5. 不调用 `systemctl`。
6. 不执行 `daemon-reload`、`enable`、`start`、`stop`。

当前 tray/minimize 能力：

1. 支持关闭窗口时隐藏到 tray 的窗口行为。
2. 支持 tray 菜单恢复窗口。
3. `MinimizeToTrayOnClose` 已接入 Desktop settings persistence。
4. 关闭到托盘只隐藏窗口；如果当前 Desktop foreground `frpc` 正在运行，它会继续保留在当前 Desktop 进程内。
5. 托盘“退出”或应用 shutdown 时 best-effort 停止当前 Desktop daemon instance 管理的子进程。

tray/minimize 不负责后台 daemon、service install、跨进程控制或外部 `frpc` 管理。

## 10. 明确仍未实现内容

当前仍未实现：

1. `frps` server support。
2. HTTP/HTTPS tunnel 完整字段支持。
3. full TOML parser 或完整 FRP compatibility layer。
4. 后台 daemon。
5. 跨进程 daemon status/stop。
6. attach 外部 `frpc`。
7. 枚举或 kill OS 进程。
8. PATH 自动搜索。
9. 自动下载 `frpc`。
10. 自动重连。
11. FRP Admin API。
12. 流量图表。
13. systemd install/enable/start/stop。
14. Windows Service。
15. macOS LaunchAgent。
16. CI workflows。
17. packaging、installer、release workflow。
18. auto-update、telemetry、cloud sync。
19. mutating CLI smoke test 的临时 DB path 策略。
20. GUI 自动化/截图回归测试。

## 11. 当前验证命令

当前标准验证入口：

```powershell
dotnet restore "Arturia.FrpNexus.sln"
dotnet build "Arturia.FrpNexus.sln" --no-restore
dotnet test "Arturia.FrpNexus.sln" --no-build
```

当前 read-only CLI help smoke check：

```powershell
dotnet run --project "src/Arturia.FrpNexus.Cli/Arturia.FrpNexus.Cli.csproj" -- --help
```

注意：

1. Mutating CLI smoke tests such as `config set`, `profile add/remove`, or `run <profileId>` may write to the real user LiteDB path unless an explicit temporary database path mechanism is provided.
2. Do not run mutating CLI smoke tests without explicit user approval and a temporary path strategy.
3. Do not run real `frpc`, `systemctl`, service installation, Desktop GUI, packaging, deployment, or push operations unless explicitly requested and scoped.
4. Phase 10D 是 docs-only；可执行 restore/build/test 作为 sanity check，但不执行 run/smoke tests。

## 12. 风险、限制、技术债

当前主要风险与限制：

1. CLI 与 Desktop foreground `frpc` integration 不等同于后台 daemon。
2. `daemon status` / `daemon stop` 不支持跨进程控制。
3. TOML parser 只覆盖本项目生成的最小 TCP/UDP 子集。
4. HTTP/HTTPS profile 当前仍不可用。
5. systemd 只 preview，不能安装、启用或启动 service。
6. Desktop run/smoke test 会访问真实用户 LiteDB 路径，除非后续引入显式临时路径策略。
7. 缺少 GUI 自动化测试和手工 UI 回归记录。
8. 缺少 CI，因此 build/test 需要本地手动执行。
9. LiteDB 目前没有复杂迁移、修复、加密或并发策略。
10. Desktop foreground runtime 只管理当前进程内 daemon instance 启动的子进程，不接管外部进程。

## 13. 推荐下一阶段路线

推荐下一阶段应保持单阶段审批，可考虑以下方向之一：

1. Phase 11A：为 CLI/Desktop 引入显式临时 DB path 或 test-mode path strategy，降低 smoke test 污染真实用户配置目录的风险。
2. Phase 11B：扩展 HTTP/HTTPS tunnel model、validation 和 TOML preview/run 支持。
3. Phase 11C：设计后台 daemon / cross-process status-stop 架构，但先做接口和边界计划，不直接实现服务安装。
4. Phase 11D：建立 CI workflow，但需用户单独批准。

开始任何后续阶段前仍需新的 Git preflight、明确计划和用户批准。
