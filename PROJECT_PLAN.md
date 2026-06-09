# FrpNexus 项目实施计划

## 1. 产品目标理解

FrpNexus 是一个跨平台 FRP 智能管理终端，目标是把 FRP 的手写 TOML、进程守护、状态监控、无头服务器运行等复杂操作降维成更易用的 GUI 与 CLI 体验。

核心目标：

1. 降低 FRP 配置门槛：用表单、配置模型和校验替代直接手写 TOML。
2. 提供可视化管理：桌面端展示隧道状态、日志、配置项和后续流量数据。
3. 支持守护运行：CLI/daemon 模式服务于 Linux/macOS/服务器场景。
4. 保持双模架构：普通启动进入 Avalonia 桌面 GUI；带参数启动进入 CLI/daemon。
5. 保持 Arturia 抽象美学：使用骑士王、圣剑、阿瓦隆概念进行命名和视觉隐喻，但不使用任何动漫图像或侵权素材。

## 2. 命名空间与整体架构理解

全局命名空间应使用：

```text
Arturia.FrpNexus
```

推荐架构方向是“共享核心 + 双入口”：

1. `Core` 承载领域模型、接口、状态、配置抽象，不依赖 GUI 或 CLI。
2. `Application` 承载应用服务、用例编排、依赖注入注册。
3. `Desktop` 使用 Avalonia UI + CommunityToolkit.MVVM，负责桌面交互。
4. `Cli` 使用 Cocona，负责命令行入口和 daemon 行为。
5. `Infrastructure` 后续承载真实 FRP 进程管理、文件系统、TOML 读写、平台服务集成。

在早期阶段不应直接实现真实 FRP 进程控制，而是先建立接口、模型和 mock 行为，避免过早耦合。

## 3. 模块边界理解

### AvalonDaemon

`AvalonDaemon` 是 FRP 进程守护与运行状态边界。

职责：

1. 定义进程生命周期抽象：启动、停止、重启、查询状态。
2. 定义运行状态模型：离线、启动中、运行中、异常、停止中等。
3. 定义日志流抽象：向 GUI 终端面板和 CLI 输出日志。
4. 定义健康检查与异常重启策略。
5. 后续才接入真实 `frpc`/`frps` 子进程管理。

不在早期做：

1. 不直接启动真实 FRP。
2. 不实现异常重启循环。
3. 不绑定具体 OS service。

### ExcaliburTunnel

`ExcaliburTunnel` 是隧道配置与 TOML 映射边界。

职责：

1. 定义隧道 profile 模型。
2. 定义 server/client 配置模型。
3. 定义表单字段到 FRP TOML 的映射策略。
4. 定义配置校验规则。
5. 后续支持 TOML 生成、解析、双向绑定。

不在早期做：

1. 不生成生产可用 FRP 配置。
2. 不承诺完整 TOML 兼容。
3. 不加入数据库或复杂配置存储，除非后续批准。

### InvisibleAirService

`InvisibleAirService` 是后台、服务、托盘、平台集成边界。

职责：

1. 定义服务模式抽象。
2. 定义托盘隐藏/显示行为。
3. 定义后台生命周期意图。
4. 后续承载 Linux `systemd`、桌面托盘、静默运行等平台特性。

不在早期做：

1. 不安装真实系统服务。
2. 不写入 `systemd` 文件。
3. 不实现跨平台托盘细节，除非进入对应阶段并批准。

## 4. GUI 模式与 CLI/daemon 模式关系

推荐使用共享核心、分离入口的关系：

```text
Desktop GUI
  -> ViewModel
  -> Application Services
  -> Core Interfaces
  -> Infrastructure Implementations

CLI / daemon
  -> Cocona Commands
  -> Application Services
  -> Core Interfaces
  -> Infrastructure Implementations
```

桌面 GUI 与 CLI 不应各自实现业务逻辑。它们只负责不同交互方式：

1. GUI：窗口、导航、表单、状态显示、日志终端。
2. CLI：命令解析、终端输出、daemon 生命周期。
3. 共享层：隧道配置、状态模型、守护控制接口、日志抽象、校验规则。

启动方式建议：

1. 无参数启动：进入 Avalonia GUI。
2. 有 CLI 参数启动：交给 Cocona 处理。
3. 具体入口实现需要在项目 skeleton 阶段设计清楚，避免 GUI 与 CLI 入口互相污染。

当前不能声明 `./frpnexus run my-server` 可用，因为仓库还没有可执行项目。

## 5. 推荐 .NET Solution / Project 结构

建议使用 .NET 8 或 .NET 9，优先 .NET 8 LTS；如果需要最新平台能力，可以选择 .NET 9。

推荐结构：

```text
Arturia.FrpNexus.sln
src/
  Arturia.FrpNexus.Core/
    Arturia.FrpNexus.Core.csproj
  Arturia.FrpNexus.Application/
    Arturia.FrpNexus.Application.csproj
  Arturia.FrpNexus.Infrastructure/
    Arturia.FrpNexus.Infrastructure.csproj
  Arturia.FrpNexus.Desktop/
    Arturia.FrpNexus.Desktop.csproj
  Arturia.FrpNexus.Cli/
    Arturia.FrpNexus.Cli.csproj
tests/
  Arturia.FrpNexus.Core.Tests/
    Arturia.FrpNexus.Core.Tests.csproj
```

项目职责：

1. `Core`：纯领域层，不依赖 Avalonia、Cocona、文件系统、进程 API。
2. `Application`：用例服务、接口编排、DTO、依赖注入扩展。
3. `Infrastructure`：真实 FRP 进程、TOML、文件、OS 服务、平台能力。
4. `Desktop`：Avalonia UI、ViewModels、Views、Resources。
5. `Cli`：Cocona 命令、daemon 命令入口、终端输出。
6. `Core.Tests`：领域模型和校验逻辑测试，等项目文件创建后再加入。

如果第一阶段需要更轻，可以暂不创建 `tests/`，但建议从 Phase 1 或 Phase 4 起加入测试项目，尤其是配置校验和 TOML 映射后会需要测试。

## 6. Avalonia UI + CommunityToolkit.MVVM + Cocona 集成方案

### Avalonia Desktop

1. `Arturia.FrpNexus.Desktop` 作为 GUI 启动项目。
2. 使用 Avalonia Application + MainWindow。
3. 使用 CommunityToolkit.MVVM 生成 `ObservableObject`、`RelayCommand` 等 ViewModel 基础能力。
4. ViewModel 不直接调用进程或文件系统，而是依赖 Application 层接口。
5. 设计系统资源放在 `Styles` / `Resources` 中，后续逐步拆分颜色、字体、控件样式。

### Cocona CLI

1. `Arturia.FrpNexus.Cli` 作为 CLI 启动项目。
2. 使用 Cocona 定义命令结构，例如后续的 `run`、`status`、`stop`、`validate`。
3. CLI 命令调用 Application 层服务，而不是直接控制 FRP。
4. CLI 输出可复用日志事件模型。
5. 早期只建立命令 skeleton，不承诺真实 daemon 能力。

### 双模入口选择

推荐：保留 `Desktop` 与 `Cli` 两个可执行项目，打包时再决定单文件或双入口策略。

备选：单一 executable 入口，根据 args 分流 GUI/Cocona，但实现复杂度更高，早期不建议。

建议先用两个入口项目降低复杂度，待架构稳定后再评估是否合并为单可执行入口。

## 7. UI 实现策略

`DESIGN.md` 优先级高于 PRD 附录。PRD 中旧版 UI 提到深色模式、深蓝背景、投影等内容；当前实现必须以 `DESIGN.md` 为准。

UI 方向：

1. WinUI 3-inspired。
2. 浅色主题优先。
3. 中文界面。
4. Mica-like 背景，回退色 `#F3F3F3`。
5. 内容卡片使用 `#FFFFFF`。
6. 弱边框 `#E5E5E5`，不使用投影。
7. 主强调色 `#D4A017`。
8. 终端背景 `#1C1C1C`。
9. UI 字体 `Segoe UI Variable`, `Microsoft YaHei`。
10. 终端字体 `Cascadia Code` 或 `Consolas`。
11. 图标风格使用 Segoe Fluent Icons 语义的线性图标。
12. 所有界面文本优先中文。

UI 结构：

1. 主窗口使用左侧导航 + 右侧内容区。
2. 左侧模拟 WinUI `NavigationView` 行为。
3. 右侧上半区为配置/状态卡片。
4. 右侧下半区为 Windows Terminal 风格日志面板。
5. 页面包括 Dashboard、隧道配置、日志、设置等占位页。
6. 早期只使用 mock 数据，避免未实现功能看起来像已可用。

交互策略：

1. 主按钮“启动穿透”使用 accent 样式。
2. 运行态按钮变为“停止穿透”，样式降级为标准按钮。
3. Hover 使用轻微底色变化模拟 Reveal。
4. Press 使用极短缩放反馈。
5. 不添加复杂装饰、不添加动画泛滥、不使用动漫素材。

## 8. 分阶段实施计划

### Phase 0: Planning

目标：

1. 阅读并对齐 `AGENTS.md`、PRD、`DESIGN.md`。
2. 输出完整实施计划。
3. 不创建任何代码、项目、脚本或 CI 文件。

预计文件变更：

```text
PROJECT_PLAN.md
```

验证：

1. Markdown 阅读。
2. 三份文档一致性审查。
3. 明确 `DESIGN.md` 覆盖 PRD 中旧 UI 描述。

### Phase 1: Project Skeleton

目标：

1. 创建 `.sln`。
2. 创建基础项目结构。
3. 建立 `Arturia.FrpNexus` 命名空间。
4. 创建最小 Avalonia 应用 shell。
5. 创建 CLI 项目 skeleton。
6. 建立项目引用关系。
7. 不实现真实 FRP 逻辑。
8. 不实现复杂 UI。
9. 不添加 CI、安装器、打包、自动更新。

预计创建文件：

```text
Arturia.FrpNexus.sln
src/Arturia.FrpNexus.Core/Arturia.FrpNexus.Core.csproj
src/Arturia.FrpNexus.Application/Arturia.FrpNexus.Application.csproj
src/Arturia.FrpNexus.Infrastructure/Arturia.FrpNexus.Infrastructure.csproj
src/Arturia.FrpNexus.Desktop/Arturia.FrpNexus.Desktop.csproj
src/Arturia.FrpNexus.Desktop/Program.cs
src/Arturia.FrpNexus.Desktop/App.axaml
src/Arturia.FrpNexus.Desktop/App.axaml.cs
src/Arturia.FrpNexus.Desktop/Views/MainWindow.axaml
src/Arturia.FrpNexus.Desktop/Views/MainWindow.axaml.cs
src/Arturia.FrpNexus.Cli/Arturia.FrpNexus.Cli.csproj
src/Arturia.FrpNexus.Cli/Program.cs
```

可能创建文件：

```text
Directory.Build.props
.editorconfig
```

是否创建这些需要用户确认。

### Phase 2: Design System Foundation

目标：

1. 将 `DESIGN.md` 转换为 Avalonia 资源。
2. 定义颜色 token、字体、圆角、间距。
3. 定义基础按钮、卡片、输入框、导航项、终端容器样式。
4. 保持 UI 静态，不接入真实业务。

预计创建或修改文件：

```text
src/Arturia.FrpNexus.Desktop/App.axaml
src/Arturia.FrpNexus.Desktop/Styles/Colors.axaml
src/Arturia.FrpNexus.Desktop/Styles/Typography.axaml
src/Arturia.FrpNexus.Desktop/Styles/Controls.axaml
src/Arturia.FrpNexus.Desktop/Styles/Layout.axaml
```

### Phase 3: Main Window 与导航

目标：

1. 实现主窗口布局。
2. 实现左侧导航。
3. 添加 Dashboard、隧道、日志、设置占位页。
4. 使用 mock 状态和 mock 日志。
5. 所有 UI 文本使用中文。

预计创建或修改文件：

```text
src/Arturia.FrpNexus.Desktop/Views/MainWindow.axaml
src/Arturia.FrpNexus.Desktop/ViewModels/MainWindowViewModel.cs
src/Arturia.FrpNexus.Desktop/Views/Pages/DashboardPage.axaml
src/Arturia.FrpNexus.Desktop/Views/Pages/DashboardPage.axaml.cs
src/Arturia.FrpNexus.Desktop/Views/Pages/TunnelsPage.axaml
src/Arturia.FrpNexus.Desktop/Views/Pages/TunnelsPage.axaml.cs
src/Arturia.FrpNexus.Desktop/Views/Pages/LogsPage.axaml
src/Arturia.FrpNexus.Desktop/Views/Pages/LogsPage.axaml.cs
src/Arturia.FrpNexus.Desktop/Views/Pages/SettingsPage.axaml
src/Arturia.FrpNexus.Desktop/Views/Pages/SettingsPage.axaml.cs
```

### Phase 4: Core Domain Interfaces

目标：

1. 定义 `AvalonDaemon` 抽象。
2. 定义 `ExcaliburTunnel` 配置模型与校验入口。
3. 定义 `InvisibleAirService` 抽象。
4. 添加 mock/in-memory 实现供 GUI 使用。
5. 不执行真实 FRP 进程，不生成生产 TOML。

预计创建文件：

```text
src/Arturia.FrpNexus.Core/AvalonDaemon/IAvalonDaemon.cs
src/Arturia.FrpNexus.Core/AvalonDaemon/TunnelRuntimeStatus.cs
src/Arturia.FrpNexus.Core/AvalonDaemon/DaemonLogEntry.cs
src/Arturia.FrpNexus.Core/ExcaliburTunnel/TunnelProfile.cs
src/Arturia.FrpNexus.Core/ExcaliburTunnel/TunnelProtocol.cs
src/Arturia.FrpNexus.Core/ExcaliburTunnel/TunnelValidationResult.cs
src/Arturia.FrpNexus.Core/InvisibleAirService/IInvisibleAirService.cs
src/Arturia.FrpNexus.Application/Services/TunnelWorkspaceService.cs
src/Arturia.FrpNexus.Infrastructure/Mock/MockAvalonDaemon.cs
```

如果批准测试项目，可能创建：

```text
tests/Arturia.FrpNexus.Core.Tests/Arturia.FrpNexus.Core.Tests.csproj
tests/Arturia.FrpNexus.Core.Tests/TunnelProfileTests.cs
```

### Phase 5: CLI Structure

目标：

1. 引入 Cocona 命令结构。
2. 添加基础命令 skeleton。
3. 保留 daemon 运行意图。
4. 输出真实可执行的基础 CLI 帮助和 mock 行为。
5. 不宣称真实隧道已建立。

预计创建或修改文件：

```text
src/Arturia.FrpNexus.Cli/Program.cs
src/Arturia.FrpNexus.Cli/Commands/RunCommand.cs
src/Arturia.FrpNexus.Cli/Commands/StatusCommand.cs
src/Arturia.FrpNexus.Cli/Commands/ValidateCommand.cs
src/Arturia.FrpNexus.Cli/Console/ArturiaBanner.cs
```

### Phase 6: FRP Integration

目标：

1. 接入真实 FRP 二进制路径配置。
2. 实现进程启动、停止、重启。
3. 捕获 stdout/stderr 日志。
4. 实现基础 TOML 生成/解析。
5. 增加配置校验。
6. 明确错误处理和平台差异。

预计创建或修改文件：

```text
src/Arturia.FrpNexus.Infrastructure/AvalonDaemon/FrpProcessAvalonDaemon.cs
src/Arturia.FrpNexus.Infrastructure/ExcaliburTunnel/TomlTunnelSerializer.cs
src/Arturia.FrpNexus.Infrastructure/ExcaliburTunnel/TunnelProfileValidator.cs
src/Arturia.FrpNexus.Application/Services/TunnelRuntimeService.cs
```

可能需要引入 TOML 依赖。引入前需要单独说明原因、使用位置和替代方案。

### Phase 7: Service / Tray Behavior

目标：

1. 实现托盘隐藏/恢复。
2. 设计后台运行生命周期。
3. 增加 Linux `systemd` 服务生成/注册能力。
4. 所有平台能力必须显式 guard，避免 Windows/macOS/Linux 行为混淆。

预计创建或修改文件：

```text
src/Arturia.FrpNexus.Infrastructure/InvisibleAirService/PlatformServiceController.cs
src/Arturia.FrpNexus.Infrastructure/InvisibleAirService/LinuxSystemdServiceInstaller.cs
src/Arturia.FrpNexus.Desktop/Services/TrayService.cs
src/Arturia.FrpNexus.Cli/Commands/ServiceCommand.cs
```

### Phase 8: Polish、Packaging、Docs

目标：

1. 完善 README。
2. 补充使用说明。
3. 增加发布打包策略。
4. 可选加入 CI，但只有在明确批准后进行。
5. 可选加入图标、启动页、安装器。

预计文件：

```text
README.md
docs/
packaging/
.github/workflows/
```

CI、打包、安装器均需单独批准。

## 9. 每阶段预计创建文件汇总

Phase 0：

```text
PROJECT_PLAN.md
```

Phase 1：

```text
Arturia.FrpNexus.sln
src/Arturia.FrpNexus.Core/
src/Arturia.FrpNexus.Application/
src/Arturia.FrpNexus.Infrastructure/
src/Arturia.FrpNexus.Desktop/
src/Arturia.FrpNexus.Cli/
```

Phase 2：

```text
src/Arturia.FrpNexus.Desktop/Styles/*.axaml
```

Phase 3：

```text
src/Arturia.FrpNexus.Desktop/Views/Pages/*.axaml
src/Arturia.FrpNexus.Desktop/ViewModels/*.cs
```

Phase 4：

```text
src/Arturia.FrpNexus.Core/AvalonDaemon/*.cs
src/Arturia.FrpNexus.Core/ExcaliburTunnel/*.cs
src/Arturia.FrpNexus.Core/InvisibleAirService/*.cs
src/Arturia.FrpNexus.Application/Services/*.cs
src/Arturia.FrpNexus.Infrastructure/Mock/*.cs
```

Phase 5：

```text
src/Arturia.FrpNexus.Cli/Commands/*.cs
src/Arturia.FrpNexus.Cli/Console/*.cs
```

Phase 6：

```text
src/Arturia.FrpNexus.Infrastructure/AvalonDaemon/*.cs
src/Arturia.FrpNexus.Infrastructure/ExcaliburTunnel/*.cs
```

Phase 7：

```text
src/Arturia.FrpNexus.Infrastructure/InvisibleAirService/*.cs
src/Arturia.FrpNexus.Desktop/Services/*.cs
```

Phase 8：

```text
README.md
docs/*
packaging/*
.github/workflows/*
```

## 10. 当前阶段验证方式

当前仓库只有 Markdown 文档，没有 solution/project/package/build/test/run 文件。

当前只能验证：

1. 已读取 `AGENTS.md`。
2. 已读取 PRD。
3. 已读取 `DESIGN.md`。
4. 计划符合 `AGENTS.md` 的 Mandatory Workflow。
5. 计划符合 Approval Gate：未获得“开始实现”前不实施代码、项目、脚本、CI。
6. 计划符合 Verification 规则：不声明构建、测试、运行成功。
7. 已识别 UI 冲突：PRD 附录偏深色和投影，`DESIGN.md` 要求浅色、白卡、弱边框、无投影，因此实现时以 `DESIGN.md` 为准。

## 11. 需要用户确认的问题

1. .NET 版本选择：使用 `.NET 8 LTS` 还是 `.NET 9`？
2. 入口结构：Phase 1 是否采用推荐的双入口项目 `Desktop` + `Cli`，暂不做单 executable 分流？
3. Phase 1 是否创建测试项目，还是等 Phase 4 有领域模型后再创建？
4. 是否允许在 Phase 1 创建 `Directory.Build.props` 和 `.editorconfig`？
5. 第一阶段是否只做到“项目可创建并能标准构建”，不做任何真实 FRP、真实 CLI 命令、复杂 UI？
6. 后续 TOML 支持是倾向引入成熟 TOML 库，还是先用自定义最小生成器？该问题可等 Phase 6 再决定。

### 已确认决策

1. .NET 版本使用 .NET 8 LTS。
2. Phase 1 采用双入口结构：Arturia.FrpNexus.Desktop 和 Arturia.FrpNexus.Cli。
3. Phase 1 暂不创建测试项目，测试项目等 Phase 4 有核心领域模型后再创建。
4. Phase 1 允许创建 Directory.Build.props、.editorconfig 和 .gitignore。
5. Phase 1 只做 solution/project skeleton、基础命名空间、最小 Avalonia shell、最小 CLI skeleton 和项目引用关系。
6. Phase 1 不实现真实 FRP 逻辑、不实现复杂 UI、不添加 CI、不添加打包、不添加安装器。
7. TOML 支持推迟到 Phase 6 再决定，不在 Phase 1 引入 TOML 依赖。
8. 如需引入 NuGet 包，必须符合 PRD/PROJECT_PLAN.md，并在阶段总结中说明用途。

## 12. 第一实施里程碑

在用户明确回复“开始实现”或“Implement Phase 1”后，第一里程碑建议是：

```text
Phase 1: 创建 .NET solution/project skeleton，建立 Arturia.FrpNexus 命名空间、Avalonia GUI 基础 shell、Cocona CLI 基础项目和核心分层边界；不实现真实 FRP 逻辑，不实现复杂 UI，不添加 CI。
```
