# Phase 1 Summary

## 1. Phase 1 目标

Phase 1 的目标是完成 FrpNexus 的项目骨架，不进入真实业务实现。

已确认目标：

1. 使用 .NET 8 LTS。
2. 创建 `Arturia.FrpNexus.sln`。
3. 创建 `src/Arturia.FrpNexus.Core`。
4. 创建 `src/Arturia.FrpNexus.Application`。
5. 创建 `src/Arturia.FrpNexus.Infrastructure`。
6. 创建 `src/Arturia.FrpNexus.Desktop`。
7. 创建 `src/Arturia.FrpNexus.Cli`。
8. 使用双入口结构：Desktop GUI 与 CLI 分离。
9. 建立基础项目引用关系。
10. Desktop 只提供最小 Avalonia App shell。
11. CLI 只提供最小 Cocona skeleton 和占位帮助信息。
12. 不创建测试项目。
13. 不进入 Phase 2。

## 2. Phase 1.5 Tooling Stabilization 目标

Phase 1.5 的目标是稳定 .NET SDK 选择，避免本机较新的 preview SDK 驱动构建。

已完成内容：

1. 确认当前环境存在 .NET SDK `8.0.206`。
2. 创建根目录 `global.json`。
3. 固定 SDK 版本为 `8.0.206`。
4. 使用 `rollForward: latestFeature` 允许同一 major/minor 下的 feature band 前滚。
5. 验证 `dotnet --version` 输出为 `8.0.206`。

## 3. 实际创建的项目结构

当前 Phase 1/1.5 相关结构如下：

```text
Arturia.FrpNexus.sln
Directory.Build.props
global.json
.editorconfig
.gitignore
src/
  Arturia.FrpNexus.Core/
    Arturia.FrpNexus.Core.csproj
    CoreAssemblyMarker.cs
  Arturia.FrpNexus.Application/
    Arturia.FrpNexus.Application.csproj
    ApplicationAssemblyMarker.cs
  Arturia.FrpNexus.Infrastructure/
    Arturia.FrpNexus.Infrastructure.csproj
    InfrastructureAssemblyMarker.cs
  Arturia.FrpNexus.Desktop/
    Arturia.FrpNexus.Desktop.csproj
    Program.cs
    App.axaml
    App.axaml.cs
    app.manifest
    Views/
      MainWindow.axaml
      MainWindow.axaml.cs
  Arturia.FrpNexus.Cli/
    Arturia.FrpNexus.Cli.csproj
    Program.cs
docs/
  PHASE_1_SUMMARY.md
```

构建和还原产生的 `bin/` 与 `obj/` 目录是工具产物，已由 `.gitignore` 排除。

## 4. 实际引入的 NuGet 包及用途

### Arturia.FrpNexus.Desktop

```text
Avalonia 11.3.3
Avalonia.Desktop 11.3.3
Avalonia.Themes.Fluent 11.3.3
```

用途：

1. `Avalonia`：提供 Avalonia UI 基础能力。
2. `Avalonia.Desktop`：提供桌面应用生命周期和平台启动能力。
3. `Avalonia.Themes.Fluent`：提供 Fluent 风格基础主题，符合 `DESIGN.md` 的 WinUI 3-inspired 方向。

### Arturia.FrpNexus.Cli

```text
Cocona 2.2.0
```

用途：

1. 提供 CLI skeleton 的命令入口能力。
2. 为后续 CLI/daemon 命令结构保留方向。
3. 当前只实现占位帮助命令，不实现真实 daemon 或 FRP 命令能力。

### 其他项目

```text
Arturia.FrpNexus.Core: 无 NuGet 包
Arturia.FrpNexus.Application: 无 NuGet 包
Arturia.FrpNexus.Infrastructure: 无 NuGet 包
```

未引入 TOML 依赖。TOML 支持按计划推迟到 Phase 6 再决定。

## 5. 项目引用关系

当前引用关系：

```text
Arturia.FrpNexus.Application
  -> Arturia.FrpNexus.Core

Arturia.FrpNexus.Infrastructure
  -> Arturia.FrpNexus.Core
  -> Arturia.FrpNexus.Application

Arturia.FrpNexus.Desktop
  -> Arturia.FrpNexus.Application
  -> Arturia.FrpNexus.Infrastructure

Arturia.FrpNexus.Cli
  -> Arturia.FrpNexus.Application
  -> Arturia.FrpNexus.Infrastructure
```

该结构保留了 GUI 与 CLI 共用应用层和基础设施层的方向。

## 6. 当前可用验证命令

当前仓库已经存在 solution 和 project 文件，因此可以使用以下命令验证：

```powershell
dotnet --list-sdks
dotnet --version
dotnet restore "Arturia.FrpNexus.sln"
dotnet build "Arturia.FrpNexus.sln" --no-restore
dotnet run --project "src/Arturia.FrpNexus.Cli/Arturia.FrpNexus.Cli.csproj" -- help
dotnet list "Arturia.FrpNexus.sln" package
```

## 7. 已执行过的验证结果

已执行：

1. `dotnet --list-sdks`
   结果：成功，确认存在 `8.0.206`、`9.0.110`、`9.0.300`、`10.0.100-preview.2.25164.34`。
2. `dotnet --version`
   结果：成功，在创建 `global.json` 后输出 `8.0.206`。
3. `dotnet restore "Arturia.FrpNexus.sln"`
   结果：最终成功。
4. `dotnet build "Arturia.FrpNexus.sln" --no-restore`
   结果：最终成功，`0` warning，`0` error。
5. `dotnet run --project "src/Arturia.FrpNexus.Cli/Arturia.FrpNexus.Cli.csproj" -- help`
   结果：成功，输出 CLI skeleton 占位帮助信息。
6. `dotnet list "Arturia.FrpNexus.sln" package`
   结果：成功，确认包引用为 Avalonia 相关包和 Cocona。

历史修正记录：

1. 初始使用 `Cocona 2.5.0` 还原失败，因为 NuGet 可用版本中最接近版本为 `2.2.0`；已改为 `2.2.0`。
2. 初次构建时 `Arturia.FrpNexus.Application` 命名空间与 Avalonia `Application` 类型名解析冲突；已通过完全限定 `Avalonia.Application` 修正。
3. 创建 `global.json` 前，构建由本机 .NET 10 preview SDK 驱动并出现 preview SDK 提示；Phase 1.5 后 `dotnet --version` 已固定为 `8.0.206`。

未执行：

1. 未执行 Desktop GUI 启动验证。
2. 未执行自动化测试，因为 Phase 1 未创建测试项目。
3. 未执行发布、打包、安装器、CI 相关命令，因为这些内容不属于 Phase 1/1.5。

## 8. 已知限制

1. Desktop 目前只是最小 Avalonia shell，不包含完整设计系统。
2. CLI 目前只是 Cocona skeleton 和占位帮助命令。
3. Core/Application/Infrastructure 目前只有 assembly marker，没有领域接口或应用服务。
4. 未创建测试项目，测试计划推迟到 Phase 4 有核心领域模型后再启动。
5. 未实现真实配置存储。
6. 未实现 TOML 解析或生成。
7. 未实现 FRP 二进制发现、下载、启动、停止、重启或日志采集。
8. 未实现 tray、service、daemon 或 systemd 集成。

## 9. 未实现真实 FRP 逻辑

Phase 1/1.5 没有实现任何真实 FRP 逻辑。

明确未实现：

1. 未调用 `frpc` 或 `frps`。
2. 未启动、停止、重启任何 FRP 进程。
3. 未解析或生成 FRP TOML 配置。
4. 未建立任何真实隧道连接。
5. 未实现运行状态监控或日志流。

## 10. 未添加 CI、打包、安装器或真实服务安装逻辑

Phase 1/1.5 没有添加以下内容：

1. 没有添加 CI workflow。
2. 没有添加打包配置。
3. 没有添加安装器配置。
4. 没有添加自动更新。
5. 没有添加发布脚本。
6. 没有添加 Linux `systemd` 真实服务安装逻辑。
7. 没有添加托盘或后台常驻逻辑。

## 11. global.json 的作用与 SDK 策略

`global.json` 用于固定当前仓库使用的 .NET SDK，避免本机安装了更高版本或 preview SDK 时被自动选中。

当前策略：

```json
{
  "sdk": {
    "version": "8.0.206",
    "rollForward": "latestFeature"
  }
}
```

说明：

1. `version: 8.0.206` 固定当前仓库优先使用 .NET 8 SDK。
2. `rollForward: latestFeature` 允许在 .NET 8 范围内向更新 feature band 前滚。
3. 该策略符合 Phase 1 已确认决策中的 `.NET 8 LTS` 方向。
4. 该策略避免继续使用本机 `.NET 10 preview` SDK 驱动构建。

## 12. 下一阶段 Phase 2 入口

Phase 2 是 Design System Foundation。

建议入口：

1. 重新读取 `AGENTS.md`、`PROJECT_PLAN.md`、`DESIGN.md` 和 PRD。
2. 确认 Phase 2 范围只包含设计系统基础，不接入真实业务。
3. 将 `DESIGN.md` 中的颜色、字体、圆角、间距转为 Avalonia resources/styles。
4. 定义基础卡片、按钮、输入框、导航和终端容器样式。
5. 保持 UI 静态或 mock-only。
6. 不实现真实 FRP、daemon、service、tray、TOML 或 CLI 业务功能。
