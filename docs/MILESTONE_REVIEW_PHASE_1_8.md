# Milestone Review: Phase 1-8

> Superseded note: This document is a historical Phase 1-8 snapshot. The current repository state after Phase 9A-10D is documented in `docs/MILESTONE_REVIEW_PHASE_1_10.md`. Keep the body below unchanged as historical context.

## 1. 文档定位

本文档记录 FrpNexus 从 Phase 1 到 Phase 8E 后的当前状态。它取代 `docs/MILESTONE_REVIEW_PHASE_1_7.md` 作为当前阶段快照；Phase 1-7 review 保留为历史记录，不再代表 Phase 8A/8B/8C/8E 后的最新事实。

本次 Phase 8E 仅同步文档状态，不修改源码、测试、solution、project 文件，不提交、不合并、不 push，也不进入 Phase 9A。

## 2. Phase 1-8 已完成内容

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
12. Phase 8E：同步当前文档状态，补齐 Phase 1-8 milestone review。

## 3. 当前架构状态

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

当前模块边界仍遵循：

1. `AvalonDaemon`：FRP process supervision concepts、runtime status、log abstraction。
2. `ExcaliburTunnel`：tunnel profile、TOML generation/parsing、validation。
3. `InvisibleAirService`：service preview、tray/background behavior concepts。

## 4. 当前 Desktop UI 状态

当前已有 Avalonia Desktop shell：

1. 主窗口与导航结构已建立。
2. UI 方向遵循 `DESIGN.md`：WinUI 3-inspired、浅色中文界面、Mica-like fallback background、白色 cards、弱边框、无 drop shadow、金色 accent。
3. 已有工作台、隧道配置、运行日志、设置等静态/mock 页面。
4. 已有 tray/minimize window behavior。

当前 Desktop GUI 仍未接入 LiteDB profile repository，也未形成真实 profile CRUD、真实 tunnel start/stop 或实时日志流绑定。

## 5. 当前 CLI 能力

当前已有 Cocona CLI，并包含以下能力：

1. Root-level `run <profileId>`。
2. `config` commands。
3. `profile` commands。
4. `tunnel` preview/validation commands。
5. `service` preview/explain/status-style commands。
6. `daemon` command group structure。

Phase 8B 后，CLI config/profile 已接入 LiteDB：

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

`frpcPath` 优先级：

```text
--frpc-path -> LiteDB settings.frpcPath -> FRPNEXUS_FRPC_PATH
```

如果最终没有可用 `frpcPath`，CLI 会失败并输出明确错误；不会搜索 PATH，不会自动下载 `frpc`。

## 6. 当前 FRP 集成能力

当前 FRP 集成是最小可用边界：

1. 支持当前 CLI 进程 foreground 模式启动 `frpc` client。
2. 支持使用项目内生成的 TCP/UDP 最小 TOML 配置。
3. 支持 TCP/UDP profile validation。
4. 支持解析本项目 serializer 生成的最小 TOML 子集。

当前不支持：

1. 后台 daemon。
2. 跨进程 status/stop。
3. `frps` server。
4. 自动下载 `frpc`。
5. PATH 自动搜索。
6. 完整 FRP/TOML 兼容。

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

Desktop GUI 尚未消费这些 repository/store，因此 GUI 里的 profile、状态、日志仍不是持久化真实数据。

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

测试使用临时 LiteDB 文件和 fake daemon，不应污染真实用户配置目录，不启动真实 `frpc`。

## 9. 当前 systemd preview 能力

当前 systemd 能力仅限 preview：

1. 生成 Linux user-level systemd unit preview。
2. 校验必要路径和 profile id。
3. 输出 safety notes。
4. 不写入 unit file。
5. 不调用 `systemctl`。
6. 不执行 `daemon-reload`、`enable`、`start`、`stop`。

## 10. 当前 tray/minimize 能力

当前 Desktop 已有 tray/minimize window behavior：

1. 支持关闭窗口时隐藏到 tray 的窗口行为。
2. 支持 tray 菜单恢复窗口。
3. tray 行为只影响 Desktop window visibility。

当前 tray/minimize 不负责启动、停止或监督 `frpc`。`MinimizeToTrayOnClose` 已存在于 settings model，但 Desktop GUI 尚未接入 LiteDB settings，因此该设置不会跨重启持久化。

## 11. 明确仍未实现内容

当前仍未实现：

1. Desktop GUI LiteDB 接入。
2. Desktop GUI profile CRUD。
3. Desktop GUI frpc start/stop。
4. Desktop GUI 实时日志流。
5. 托盘设置跨重启持久化。
6. full TOML parser。
7. HTTP/HTTPS tunnel support。
8. `frps`。
9. service install/start/enable。
10. Windows Service/macOS LaunchAgent。
11. 后台 daemon。
12. 跨进程 daemon status/stop。
13. 自动下载 `frpc`。
14. PATH 自动搜索。
15. CI/installer/auto-update/telemetry。
16. packaging、release workflow、cloud sync、traffic dashboard。

## 12. 当前验证命令

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

Phase 8E 本次文档更新不执行验证命令，等待用户确认。

## 13. 风险、限制、技术债

当前主要风险与限制：

1. Desktop GUI 与真实 LiteDB persistence 尚未打通，GUI 仍偏 mock/static。
2. GUI 没有真实 tunnel lifecycle controls。
3. GUI 没有实时 log stream binding。
4. CLI foreground `frpc` integration 不等同于后台 daemon。
5. `daemon status` / `daemon stop` 不支持跨进程控制。
6. TOML parser 只覆盖本项目生成的最小子集。
7. HTTP/HTTPS profile 当前仍不可用。
8. systemd 只 preview，不能安装、启用或启动 service。
9. tray setting model 已存在，但 GUI settings persistence 尚未接入。
10. 缺少 GUI 自动化测试和手工 UI 回归记录。
11. 缺少 CI，因此 build/test 需要本地手动执行。
12. LiteDB 目前没有复杂迁移、修复、加密或并发策略。

## 14. 推荐下一阶段路线

推荐下一阶段：Phase 9A Desktop LiteDB integration。

建议 Phase 9A 范围：

1. 让 Desktop GUI 通过 Application services 读取 LiteDB profiles。
2. 在 Desktop tunnel/settings 相关 ViewModel 中接入 repository/store。
3. 实现 Desktop profile list/create/update/delete 的最小闭环。
4. 让 `MinimizeToTrayOnClose` settings 在 Desktop 中读取和保存。
5. 保持 `frpc` real start/stop、实时日志流和 daemon/service 安装在后续单独阶段中处理，除非用户另行批准。

Phase 9A 开始前仍需新的 Git preflight、明确计划和用户批准。
