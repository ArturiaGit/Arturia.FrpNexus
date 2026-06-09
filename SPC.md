# FrpNexus (Arturia) SPC 文档

> 文档类型：Software Product Specification / 软件产品规格说明  
> 产品代号：Project Avalon (Arturia)  
> 命名空间：`Arturia.FrpNexus`  
> 当前状态：已完成至 Phase 10D：solution/project skeleton、Avalonia Desktop shell、Cocona CLI、最小 `frpc` foreground integration、LiteDB 持久化、CLI config/profile 命令、Desktop profile CRUD、Desktop validation/preview、Desktop foreground `frpc` run、Desktop logs/status binding、runtime UX hardening、xUnit 测试基础和 Phase 1-10 文档同步；当前文档继续作为产品与开发规格约束使用

> 当前实施状态以 `docs/MILESTONE_REVIEW_PHASE_1_10.md`、各 Phase summary 和实际仓库结构为准；`docs/MILESTONE_REVIEW_PHASE_1_8.md` 保留为历史快照；UI 视觉 token、样式规范和 UI-only 细节以 `DESIGN.md` 为最高依据。

## 1. 产品目标

FrpNexus 是一款面向开发者、独立极客和中小型运维场景的 FRP 跨平台智能管理终端。产品目标是降低 FRP 配置和守护进程管理门槛，将手写 TOML、进程监控、日志查看和服务化运行整合到一个轻量、清晰、可跨平台运行的工具中。

核心价值：

- 将复杂 FRP 配置转化为可视化表单操作。
- 提供桌面 GUI 与 CLI/Daemon 双模运行能力。
- 提供连接状态、运行日志、隧道配置的统一管理体验。
- 保留 Arturia / Avalon / Excalibur 的抽象命名体系，但不使用任何受版权保护的动漫图片或素材。

## 2. 产品范围

### 2.1 已完成的当前 MVP 范围

- Avalonia 桌面 GUI 启动壳和中文导航页面。
- Cocona CLI 入口结构和 config/profile/tunnel/service/daemon command groups。
- 隧道配置 Profile 基础模型和 LiteDB profile persistence。
- FRP 进程生命周期抽象，以及 CLI/Desktop foreground `frpc` current-process run 边界。
- Desktop 运行状态与当前进程日志 snapshot 展示。
- Dashboard、配置、日志、设置页面的基础导航结构。
- Desktop Profile CRUD、validation 和 TCP/UDP 只读 TOML preview。

### 2.2 非 MVP 范围

- 后台 daemon 与跨进程 FRP status/stop。
- 自动下载 FRP 二进制。
- Linux systemd 服务安装。
- Windows Service / macOS LaunchAgent。
- WebDAV / GitHub Gist 云同步。
- 流量图表和 FRP Admin API 集成。
- CI、安装包、自动更新、遥测和分析。

## 3. 目标用户

- 需要快速暴露本地服务的研发工程师。
- 需要轻量跨平台穿透管理工具的个人开发者和极客玩家。
- 需要批量部署、脚本化操作和无头服务器运行能力的中小企业 IT 运维。

## 4. 技术规格

### 4.1 推荐技术栈

- Runtime：.NET 8 或 .NET 9。
- GUI：Avalonia UI。
- MVVM：CommunityToolkit.MVVM。
- CLI：Cocona。
- 本地持久化：LiteDB。
- 自动化测试：xUnit。
- 配置格式：FRP 标准 TOML。

### 4.2 命名空间

全局命名空间使用：

```text
Arturia.FrpNexus
```

### 4.3 双模运行原则

应用应支持两种运行路径：

- 无参数启动：进入桌面 GUI 模式。
- 带参数启动：进入 CLI/Daemon 模式。

当前已创建 Desktop 与 CLI 双入口。CLI 已实现 config/profile 命令、Linux user-level systemd preview、TCP/UDP TOML preview/validation，以及 `run <profileId>` 读取 LiteDB profile 后以前台模式启动当前 CLI 进程管理的 `frpc` client。Desktop 已实现 LiteDB settings/profile 消费、profile CRUD、validation、TCP/UDP TOML preview、当前 Desktop 进程内 foreground `frpc` start/stop、runtime status/logs binding 和 UX hardening。CLI 与 Desktop 均仍不支持后台 daemon、跨进程 status/stop、service install、PATH 自动搜索或自动下载 `frpc`。

## 5. 架构规格

### 5.1 建议项目结构

```text
Arturia.FrpNexus.sln
src/
  Arturia.FrpNexus.Core/           # 核心领域模型与接口
  Arturia.FrpNexus.Application/    # 应用服务、用例编排与依赖注册边界
  Arturia.FrpNexus.Infrastructure/ # FRP、文件、系统服务等实现边界
  Arturia.FrpNexus.Desktop/        # Avalonia GUI 入口
  Arturia.FrpNexus.Cli/            # Cocona CLI 入口
tests/
  Arturia.FrpNexus.Tests/          # xUnit 自动化测试项目
```

上述结构已在 Phase 1/1.5 中创建，并在后续阶段扩展为当前实现结构。当前完成事实和已执行验证以 `docs/MILESTONE_REVIEW_PHASE_1_10.md`、各 Phase summary 和实际仓库结构为准。后续新增项目、测试、依赖或结构调整仍需按 `AGENTS.md` 的阶段审批规则执行。

### 5.2 模块边界

#### AvalonDaemon

负责 FRP 进程守护相关概念：

- 进程生命周期抽象。
- 启动、停止、重启意图。
- 运行状态模型。
- 日志流抽象。
- 健康状态报告。

当前已实现 CLI 和 Desktop current-process foreground `frpc` run 边界。后续不得扩展到后台 daemon、跨进程控制、外部进程 attach/search/kill 或 service install，除非单独阶段计划获批。

#### ExcaliburTunnel

负责隧道与 TOML 配置相关概念：

- 隧道 Profile 模型。
- 本地地址、端口、远端端口、协议等配置项。
- FRP TOML 生成与解析策略。
- 配置校验规则。

当前已提供模型、校验接口、TCP/UDP 最小 TOML serializer/parser、LiteDB profile repository、CLI profile commands、Desktop profile CRUD、Desktop validation 和只读 TOML preview。当前 TOML 支持仍是项目生成的最小子集，不是完整 TOML parser 或完整 FRP 兼容层。

#### InvisibleAirService

负责后台服务与托盘行为相关概念：

- 服务模式抽象。
- 托盘隐藏与恢复意图。
- 后台生命周期状态。
- 平台特定服务集成边界。

当前已实现 CLI systemd preview-only 和 Desktop tray/minimize window behavior。仍不实现 systemd install/enable/start/stop、Windows Service、macOS LaunchAgent 或其他平台服务安装。

## 6. UI/UX 规格

### 6.1 视觉方向

- 风格：WinUI 3 inspired。
- 主题：浅色优先。
- 语言：中文界面。
- 背景：Mica-like，无法渲染时回退 `#F3F3F3`。
- 卡片：白色 `#FFFFFF`。
- 边框：低对比细边框 `#E5E5E5`。
- 阴影：不使用投影。
- 主强调色：`#D4A017`。
- 终端背景：`#1C1C1C`。

### 6.2 字体

- UI 字体：`Segoe UI Variable`, `Microsoft YaHei`。
- 终端/代码字体：`Cascadia Code` 或 `Consolas`。

### 6.3 主界面结构

- 左侧导航栏：使用类似 WinUI `NavigationView` 的结构。
- 顶部区域：应用标题、当前页面标题、连接状态。
- 中部区域：配置表单和操作按钮。
- 底部区域：内嵌 Windows Terminal 风格日志面板。

### 6.4 页面建议

- 工作台：展示当前隧道状态、启动/停止操作和关键日志。
- 隧道配置：创建、编辑、校验隧道 Profile。
- 运行日志：查看 AvalonDaemon 日志流。
- 设置：FRP 二进制路径、默认配置路径、启动行为等。

## 7. 功能规格

### 7.1 隧道 Profile 管理

- 新建隧道配置。
- 编辑隧道配置。
- 删除隧道配置。
- 标记当前运行 Profile。
- 校验必填字段与端口范围。

### 7.2 运行控制

- 启动隧道。
- 停止隧道。
- 重启隧道。
- 展示运行中、已停止、异常、未知状态。

当前 CLI 与 Desktop 已接入 foreground `frpc` run 边界。Desktop 只管理当前 Desktop 进程内 daemon instance 启动的子进程；CLI 只管理当前 CLI 进程 foreground run。当前不支持后台 daemon、跨进程 status/stop、外部进程 attach/search/kill、PATH 搜索或自动下载。

### 7.3 日志展示

- 展示守护进程日志。
- 区分 INFO、SUCCESS、WARN、ERROR 等级。
- 保持终端风格视觉。

### 7.4 CLI 结构

CLI 应与 GUI 复用 Core/Application/Infrastructure 层逻辑。当前 CLI 已包含 `config`、`profile`、`tunnel`、`service`、`daemon` 和 root-level `run <profileId>` 命令。`run <profileId>` 从 LiteDB profile repository 查找 profile；找不到则失败，不 fallback 到默认 profile。`frpcPath` 优先级为 `--frpc-path` -> LiteDB `settings.frpcPath` -> `FRPNEXUS_FRPC_PATH`。

## 8. 数据规格

### 8.1 TunnelProfile

建议字段：

- `Id`：唯一标识。
- `Name`：配置名称。
- `Protocol`：协议类型，如 TCP、UDP、HTTP、HTTPS。
- `LocalHost`：本地地址。
- `LocalPort`：本地端口。
- `RemotePort`：远端端口。
- `ServerAddress`：FRP 服务端地址。
- `ServerPort`：FRP 服务端端口。
- `Enabled`：是否启用。

### 8.2 RuntimeStatus

建议状态：

- `Unknown`
- `Stopped`
- `Starting`
- `Running`
- `Stopping`
- `Failed`

### 8.3 LogEntry

建议字段：

- `Timestamp`
- `Level`
- `Source`
- `Message`

### 8.4 FrpNexusSettings

当前 LiteDB settings collection 使用固定 `_id = "default"`，字段包括：

- `Version`：配置版本。
- `FrpcPath`：本地 `frpc` binary 路径。
- `MinimizeToTrayOnClose`：托盘最小化设置模型字段。当前 Desktop GUI 已接入 settings persistence，设置页保存后可供后续 Desktop 启动读取。
- `ActiveProfileId`：预留的活动 profile 标识。

### 8.5 LiteDB Collections

当前本地数据库文件名为：

```text
frpnexus.db
```

平台路径策略：

- Windows：`%APPDATA%\Arturia\FrpNexus\frpnexus.db`
- Linux：`$XDG_CONFIG_HOME/frpnexus/frpnexus.db`，fallback `~/.config/frpnexus/frpnexus.db`
- macOS：`~/Library/Application Support/Arturia/FrpNexus/frpnexus.db`

当前 collections：

- `settings`：单文档配置。
- `tunnel_profiles`：以 `TunnelProfile.Id` 作为 `_id` 的 profile 文档。

## 9. 阶段计划

### Phase 0：规划

- 阅读 `AGENTS.md`、PRD、`DESIGN.md`。
- 输出实现计划或 SPC 文档。
- 不创建源码、项目文件或脚本。

### Phase 1：项目骨架

- 创建 .NET solution / project 结构。
- 建立 GUI、CLI、Core、Infrastructure 边界。
- 创建基础 Avalonia 应用壳。
- 不实现真实 FRP 逻辑。

### Phase 2：设计系统基础

- 将 `DESIGN.md` 设计令牌转为 Avalonia 资源。
- 建立颜色、字体、圆角、卡片、按钮、输入框、终端样式。
- 保持静态和 Mock 数据。

### Phase 3：主窗口与导航

- 实现主窗口布局。
- 添加工作台、隧道、日志、设置占位页。
- 使用中文界面文本。

### Phase 4：核心领域接口

- 定义 AvalonDaemon、ExcaliburTunnel、InvisibleAirService 的接口和模型。
- 添加必要的 Mock 实现。
- 不管理真实进程。

### Phase 5：CLI 结构

- 添加 Cocona 命令结构。
- 保留双模运行路径。
- 仅在实际验证后声明命令可用。

### Phase 6：FRP 集成

- 接入真实 FRP 进程启动、停止、重启。
- 实现 TOML 生成、解析和校验。
- 接入运行状态与日志采集。

### Phase 7：服务与托盘

- 实现平台特定后台服务能力。
- 实现托盘隐藏与恢复。
- Linux systemd 集成需显式隔离并谨慎验证。

### Phase 8A：LiteDB 配置持久化基础

- 已完成 LiteDB settings/profile repository 基础。
- 已新增 `FrpNexusSettings`、settings store、database path provider、tunnel profile repository。
- 未让 Desktop GUI 消费 LiteDB 数据。

### Phase 8B：CLI 接入 LiteDB 配置持久化

- 已完成 CLI `config show/get/set frpc-path`。
- 已完成 CLI `profile list/show/add/remove`。
- 已将 `run <profileId>` 改为从 LiteDB profile repository 查找 profile。
- 未执行 mutating CLI smoke test 写入真实用户配置目录。

### Phase 8C：测试基础

- 已完成 xUnit 测试项目。
- 已覆盖 TOML serializer/parser、validation、systemd unit builder、CliProfileFactory、LiteDB store/repository 和 CLI config/profile/run 逻辑。

### Phase 9A：Desktop LiteDB integration

- 已完成 Desktop 设置页读取/保存 `frpcPath` 和 `minimizeToTrayOnClose`。
- 已完成 Desktop 隧道页读取 LiteDB profiles。
- 已完成 Desktop 持久化错误状态展示。

### Phase 9B：Desktop profile CRUD

- 已完成 Desktop profile 新增、编辑、删除、刷新。
- 已完成保存前 validation。
- 删除只删除 LiteDB profile，不停止任何正在运行的 `frpc`。

### Phase 9C：Desktop validation and TOML preview

- 已完成 Desktop “校验配置”。
- 已完成 TCP/UDP 只读 TOML preview。
- 未写 TOML 文件，未启动真实 `frpc`。

### Phase 10A：Desktop foreground frpc controls

- 已完成 Desktop foreground `frpc` start/stop。
- 只管理当前 Desktop 进程内 daemon instance 启动的子进程。
- 不支持后台 daemon、跨进程 status/stop、外部进程 attach/search/kill、PATH 搜索或自动下载。

### Phase 10B：Desktop runtime logs/status

- 已完成 Logs 页面绑定当前 Desktop daemon snapshot。
- 已展示 runtime status、active profile、health message、recent logs 和 refresh time。

### Phase 10C：Desktop runtime UX hardening

- 已完成中文 runtime status、手动刷新、空状态/错误状态和 foreground runtime 边界说明。

### Phase 10D：Docs sync

- 同步 `AGENTS.md`、`SPC.md` 和 milestone review，使文档反映 Phase 1-10 当前真实状态。

### Phase 10E：Architecture diagnosis docs sync

- 新增 `docs/ARCHITECTURE_DIAGNOSIS_PHASE_10D.md`。
- 将 Phase 11+ 演进护栏同步到 `AGENTS.md` 和本 SPC。
- 本阶段仅同步文档，不修改源码、测试、项目文件、依赖、CLI/Desktop 行为或 service/runtime 行为。

### Phase 11A：Temporary DB path / test-mode strategy

- 为 CLI、Desktop 和测试引入显式数据库路径覆盖或 test-mode path strategy。
- 目标是让 mutating CLI smoke test 和后续自动化验证不写入真实用户 LiteDB 路径。
- 不修改业务功能，不启动真实 `frpc`，不进入后台 daemon。

验收标准：

- 默认用户 LiteDB 路径行为保持不变。
- 测试和受控 smoke 可使用临时 DB path。
- mutating CLI 测试具备不污染真实用户配置目录的策略。

### Phase 11B：Dashboard runtime binding

- 将 Desktop Dashboard 从静态/mock 状态改为读取当前 Desktop foreground runtime snapshot。
- 不引入后台 daemon，不实现跨进程 status/stop。

验收标准：

- Dashboard 不再显示 mock 已连接状态。
- Dashboard 状态、active profile 和 recent logs 与当前 Desktop 进程内 runtime snapshot 一致。
- Logs/Tunnels 页面既有行为不回归。

### Phase 11C：HTTP/HTTPS profile model, validation, and TOML preview

- 扩展 profile model、validation、CLI/Desktop 表单和 TOML preview，支持 HTTP/HTTPS 配置子集。
- 不实现 `frps`，不声称 full TOML parser，不实现 full FRP compatibility layer。

验收标准：

- HTTP/HTTPS profile 可创建、保存、校验和预览 TOML。
- TCP/UDP profile 行为不回归。
- 不支持字段必须有明确错误或 warning，不静默丢失。

### Phase 11D：frpc PATH discovery and version detection

- 在当前 `--frpc-path`、LiteDB `settings.frpcPath`、`FRPNEXUS_FRPC_PATH` 之外，增加 PATH 搜索和版本检测。
- 不自动下载 `frpc`。

验收标准：

- CLI/Desktop 可展示 resolved `frpc` path、version 和错误原因。
- 显式路径优先级保持不变。
- 找不到 `frpc` 时不启动 runtime，并给出明确提示。

### Phase 12+：Configuration and foreground runtime hardening

- 配置导入/导出。
- Foreground runtime snapshot 扩展：PID、启动时间、退出码、runtime mode、临时配置路径或来源。
- 临时 TOML 配置清理策略。
- LiteDB migration、backup、restore 和并发冲突处理。

### Phase 13+：Daemon / IPC / service integration

- 先实现 `frpnexus daemon serve` 和跨进程 IPC prototype。
- 后续再分平台实现 Linux user-level systemd install、Windows Service、macOS LaunchAgent。
- service install 必须支持 dry-run/preview、显式确认和清晰回滚说明。

## 9.1 Phase 11+ 演进护栏

开始任何 Phase 11+ 计划前，必须阅读：

1. `docs/MILESTONE_REVIEW_PHASE_1_10.md`
2. `docs/ARCHITECTURE_DIAGNOSIS_PHASE_10D.md`
3. `SPC.md`
4. `DESIGN.md`

任何 Phase 11+ 计划必须说明：

1. 遵循 `docs/ARCHITECTURE_DIAGNOSIS_PHASE_10D.md` 的哪一条建议。
2. 明确不实现哪些更高风险能力。
3. 为什么该阶段可独立测试。
4. 预计创建或修改哪些文件。
5. 是否能避免写入真实用户 LiteDB 路径和启动真实 `frpc`。

未经单独阶段批准，不得实现：

- 后台 daemon。
- 跨进程 daemon status/stop/restart。
- systemd install/enable/start/stop。
- Windows Service。
- macOS LaunchAgent。
- 自动下载 `frpc`。
- full TOML parser 或 full FRP compatibility layer。
- FRP Admin API traffic dashboard。
- cloud sync。
- telemetry、auto-update、packaging、installer 或 release automation。

## 10. 验收标准

### 10.1 文档阶段验收

- SPC 与 `AGENTS.md` 不冲突。
- UI 视觉 token、样式规范和 UI-only 细节以 `DESIGN.md` 为最高依据。
- 当前实施状态和已完成事实以 `docs/MILESTONE_REVIEW_PHASE_1_10.md`、各 Phase summary 和实际仓库结构为准。
- 产品命名和模块边界与 PRD 保持一致。
- 不声明不存在的构建、运行或测试结果。

### 10.2 项目骨架阶段验收

- solution 和 project 可以被 .NET SDK 识别。
- 命名空间为 `Arturia.FrpNexus`。
- GUI、CLI、Core、Infrastructure 边界清晰。
- 未引入未说明的依赖。

### 10.3 UI 阶段验收

- 浅色中文界面。
- Mica-like 背景回退 `#F3F3F3`。
- 白色卡片、弱边框、无投影。
- 主强调色使用 `#D4A017`。
- 终端背景使用 `#1C1C1C`。

### 10.4 领域接口阶段验收

- AvalonDaemon、ExcaliburTunnel、InvisibleAirService 职责分离。
- GUI 与 CLI 能够共享 Core 层模型和接口。
- Mock 行为不冒充真实 FRP 集成。

## 11. 风险与假设

### 11.1 风险

- Avalonia 与 WinUI 3 视觉语言存在控件实现差异，需要通过样式资源模拟。
- FRP 配置格式和版本差异可能影响 TOML 生成策略。
- 跨平台服务管理差异大，Linux、Windows、macOS 需分阶段处理。
- 托盘行为在不同桌面环境中的一致性存在不确定性。

### 11.2 假设

- 项目继续采用 .NET 8/9、Avalonia UI、CommunityToolkit.MVVM、Cocona。
- 不在早期引入数据库。
- 不在早期加入 CI、安装器、自动更新或遥测。
- Arturia/Fate 风格仅保留抽象命名和色彩隐喻。

## 12. 当前验证状态

当前仓库已完成至 Phase 10D，不再是纯文档仓库。当前可执行验证命令、已执行结果和已知限制以 `docs/MILESTONE_REVIEW_PHASE_1_10.md`、各 Phase summary 和实际仓库结构为准。

当前已实现：

- Avalonia Desktop shell、设计系统、主窗口导航和静态/mock 页面。
- Cocona CLI 命令结构。
- 最小真实 `frpc` foreground client integration。
- TCP/UDP 最小 TOML serializer/parser/validation。
- LiteDB settings/profile persistence foundation。
- CLI config/profile 命令和 `run <profileId>` 读取 LiteDB profile。
- CLI Linux user-level systemd unit preview。
- Desktop tray/minimize window behavior。
- xUnit 自动化测试项目。
- Desktop settings persistence，包括 `frpcPath` 和 `minimizeToTrayOnClose`。
- Desktop GUI profile CRUD。
- Desktop GUI validation 和只读 TCP/UDP TOML preview。
- Desktop GUI foreground `frpc` start/stop，仅管理当前 Desktop 进程内 daemon instance 启动的子进程。
- Desktop Logs 页面绑定当前 Desktop runtime snapshot/logs。
- Desktop runtime UX hardening，包括中文状态、手动刷新、空状态、错误状态和边界说明。

当前仍未实现：

- 完整 TOML parser。
- HTTP/HTTPS tunnel 完整字段支持。
- `frps` server。
- 自动下载 `frpc`。
- PATH 自动搜索。
- 后台 daemon。
- 跨进程 daemon status/stop。
- attach 外部 `frpc`、枚举或 kill OS 进程。
- 自动重连。
- FRP Admin API 和流量图表。
- systemd install/enable/start/stop。
- Windows Service。
- macOS LaunchAgent。
- CI、打包、安装器、发布脚本。
- 自动更新、遥测或分析。

进入后续阶段前仍需执行：

- 与 `AGENTS.md` 的阶段审批规则一致性审查。
- 与 `DESIGN.md` 的 UI 规范一致性审查。
- 与 `docs/MILESTONE_REVIEW_PHASE_1_10.md` 和各 Phase summary 的当前事实一致性审查。
- 仅声明实际执行且成功的构建、运行或测试结果。

## 13. 第一实现里程碑

第一实现里程碑 Phase 1：项目骨架已完成。当前最新阶段性里程碑详见 `docs/MILESTONE_REVIEW_PHASE_1_10.md`。

完成标准：

- 创建 .NET solution。
- 创建 GUI、CLI、Core、Application、Infrastructure 项目。
- 建立 `Arturia.FrpNexus` 命名空间。
- 创建可启动的 Avalonia 空壳应用。
- CLI 项目仅保留入口结构。
- 不实现真实 FRP 逻辑。

后续建议入口可在临时 DB path/test-mode strategy、HTTP/HTTPS tunnel support、后台 daemon architecture planning 或 CI workflow 中择一单独 Phase。开始任何后续阶段前仍需获得用户明确批准，并继续遵守本 SPC、`AGENTS.md`、`DESIGN.md`、`docs/MILESTONE_REVIEW_PHASE_1_10.md` 与对应 Phase summary 的约束。
