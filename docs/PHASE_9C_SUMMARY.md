# Phase 9C Summary

## 1. Phase 9C 目标

Phase 9C 在 Desktop 隧道配置页补充 GUI validation 和 TOML preview。目标是让用户在保存前基于当前表单字段校验 profile，并查看只读 TOML 预览。

本阶段仍不启动、停止或重启真实 `frpc`，不控制 daemon，不写 TOML 文件，不修改 CLI。

## 2. 当前分支

```text
feature/phase-9c-desktop-validation-preview
```

Phase 9C 从已包含 Phase 9B 的 `develop` 创建。

## 3. 创建的文件

```text
docs/PHASE_9C_SUMMARY.md
```

## 4. 修改的文件

```text
src/Arturia.FrpNexus.Desktop/ViewModels/Pages/TunnelsPageViewModel.cs
src/Arturia.FrpNexus.Desktop/Views/MainWindow.axaml
src/Arturia.FrpNexus.Desktop/Views/MainWindow.axaml.cs
tests/Arturia.FrpNexus.Tests/Desktop/TunnelsPageViewModelTests.cs
```

## 5. Desktop validation 行为

隧道配置页新增“校验配置”按钮。

行为：

1. 基于当前表单字段构造临时 `TunnelProfile`。
2. 调用 `IExcaliburTunnel.Validate`。
3. TCP/UDP 校验通过后显示中文成功状态。
4. HTTP/HTTPS 显示不支持错误。
5. 空必填字段或非法端口显示中文 validation 错误。
6. 校验不会调用 `ITunnelProfileRepository.SaveAsync`。
7. 校验不会调用 `ITunnelProfileRepository.DeleteAsync`。
8. 校验不会启动真实 `frpc`，不会控制 daemon。

HTTP/HTTPS 不支持文案明确说明：

```text
当前阶段仅支持 TCP/UDP 的校验和 TOML 预览。
```

## 6. Desktop TOML preview 行为

隧道配置页新增“预览 TOML”按钮和只读预览区域。

行为：

1. 基于当前表单字段构造临时 `TunnelProfile`。
2. 调用 `IExcaliburTunnel.PreviewConfiguration`。
3. TCP/UDP 预览成功后显示 TOML 文本。
4. HTTP/HTTPS 显示 preview 错误，不生成生产 TOML。
5. 如果 `PreviewConfiguration` 返回 `# ERROR:` 内容，GUI 显示 preview 错误状态。
6. 如果 `PreviewConfiguration` 抛出异常，GUI 显示中文错误状态。
7. 预览不会调用 repository `SaveAsync` 或 `DeleteAsync`。
8. 预览不会写 TOML 文件。
9. 预览不会修改 LiteDB。

预览区域只读显示，不提供保存到文件按钮，不提供写入 TOML 文件功能，不提供复制按钮。

## 7. UI 说明

UI 继续遵守 `DESIGN.md`：

1. 中文界面。
2. WinUI 3-inspired light theme。
3. 白色 card、弱边框、无阴影。
4. 未新增随机颜色。
5. 使用现有 Button/TextBox/CheckBox/ComboBox 风格。
6. TOML 预览区域使用终端/代码视觉。

TOML 预览区域：

```text
background: #1C1C1C
font: Cascadia Code, Consolas
read-only display
```

## 8. 测试覆盖

扩展 Desktop ViewModel tests：

1. TCP 表单校验成功，显示成功状态，不调用保存。
2. UDP 表单校验成功，显示成功状态，不调用保存。
3. TCP TOML 预览成功，显示 TOML 文本。
4. UDP TOML 预览成功，显示 TOML 文本。
5. HTTP 校验/预览失败，显示当前阶段不支持。
6. HTTPS 校验/预览失败，显示当前阶段不支持。
7. 空必填字段显示中文错误。
8. 非法端口显示中文错误。
9. `PreviewConfiguration` 返回错误内容时显示 preview 错误状态。
10. `PreviewConfiguration` 抛异常时显示中文错误。
11. 校验/预览不调用 repository `SaveAsync`。
12. 校验/预览不调用 repository `DeleteAsync`。

测试使用 fake repository / fake service，不访问真实用户 LiteDB，不启动 Desktop GUI，不启动真实 `frpc`，不调用 `systemctl`。

## 9. 明确未实现内容

Phase 9C 明确未实现：

1. 启动真实 `frpc`。
2. 停止真实 `frpc`。
3. 重启真实 `frpc`。
4. daemon 控制。
5. 实时日志流。
6. `IAvalonDaemon.StartAsync/StopAsync/RestartAsync` 调用。
7. `Process.Start` 调用。
8. `systemctl` 调用。
9. TOML 文件写入。
10. service unit 文件写入。
11. CLI 修改。
12. mutating CLI smoke test。
13. Desktop run smoke test。
14. 新增 NuGet package。
15. CI、安装器、自动更新、遥测。
16. Phase 10A 或其他阶段。
17. push。

## 10. 验证命令

Phase 9C 验证入口：

```powershell
dotnet restore "Arturia.FrpNexus.sln"
dotnet build "Arturia.FrpNexus.sln" --no-restore
dotnet test "Arturia.FrpNexus.sln" --no-build
```

本次验证结果：

1. `dotnet restore "Arturia.FrpNexus.sln"`：通过。
2. `dotnet build "Arturia.FrpNexus.sln" --no-restore`：通过，0 warning，0 error。
3. `dotnet test "Arturia.FrpNexus.sln" --no-build`：通过，105 passed，0 failed，0 skipped。

默认不执行：

```powershell
dotnet run --project "src/Arturia.FrpNexus.Desktop/Arturia.FrpNexus.Desktop.csproj"
```

本阶段不执行 Desktop run smoke test，避免访问真实用户 LiteDB 路径。

本次未执行：

1. Desktop run smoke test。
2. mutating CLI smoke test。
3. `run <profileId>`。
4. 真实 `frpc` 启动。
5. `systemctl` 调用。
6. 真实用户 LiteDB 路径访问。

## 11. 需要人工确认的 GUI 行为

1. “校验配置”按钮在 TCP/UDP 表单上显示校验通过状态。
2. “校验配置”按钮在 HTTP/HTTPS 表单上显示当前阶段不支持。
3. “预览 TOML”按钮在 TCP/UDP 表单上显示只读 TOML。
4. TOML 预览区域滚动、字体、配色符合 `DESIGN.md`。
5. Preview 错误、validation 错误文案显示清晰。
6. 预览操作不会保存 profile，不会写 TOML 文件，不会启动 `frpc`。
