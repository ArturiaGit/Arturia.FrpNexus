# Phase 9B Summary

## 1. Phase 9B 目标

Phase 9B 在 Desktop 隧道配置页实现 LiteDB profile CRUD。目标是让 GUI 可以新增、编辑、删除和刷新本地持久化 profiles，但仍不启动、停止或控制真实 `frpc`。

本阶段默认不执行 Desktop run smoke test，避免访问真实用户 LiteDB 路径。

## 2. 当前分支

```text
feature/phase-9b-desktop-profile-crud
```

Phase 9B 从已包含 Phase 9A 的 `develop` 创建。

## 3. 创建的文件

```text
docs/PHASE_9B_SUMMARY.md
```

## 4. 修改的文件

```text
src/Arturia.FrpNexus.Desktop/Composition/DesktopCompositionRoot.cs
src/Arturia.FrpNexus.Desktop/ViewModels/MainWindowViewModel.cs
src/Arturia.FrpNexus.Desktop/ViewModels/Pages/TunnelPreviewViewModel.cs
src/Arturia.FrpNexus.Desktop/ViewModels/Pages/TunnelsPageViewModel.cs
src/Arturia.FrpNexus.Desktop/Views/MainWindow.axaml
src/Arturia.FrpNexus.Desktop/Views/MainWindow.axaml.cs
tests/Arturia.FrpNexus.Tests/Desktop/TunnelsPageViewModelTests.cs
```

## 5. Desktop profile CRUD 行为

隧道配置页现在支持：

1. 新增 profile。
2. 编辑 profile。
3. 删除 profile。
4. 保存后刷新列表。
5. 取消新增/编辑。
6. 手动刷新列表。
7. Validation 错误展示。
8. HTTP/HTTPS 不支持错误展示。

编辑 profile 时 `Id` 锁定，不支持修改 `Id`。如果需要改 `Id`，应新建 profile 再删除旧 profile。

## 6. Profile form 字段

内嵌表单字段：

1. `Id`
2. `Name`
3. `Protocol`
4. `LocalHost`
5. `LocalPort`
6. `RemotePort`
7. `ServerAddress`
8. `ServerPort`
9. `Enabled`

`Protocol` 选项显示：

```text
TCP
UDP
HTTP
HTTPS
```

当前阶段仅 TCP/UDP 可以保存。HTTP/HTTPS 会显示 Phase 6 validation 不支持错误，不写入 LiteDB。

## 7. Validation 策略

保存前必须调用：

```text
IExcaliburTunnel.Validate
```

行为：

1. TCP/UDP validation 通过后调用 `TunnelProfileService.SaveAsync`。
2. HTTP/HTTPS validation 失败并显示当前不支持。
3. 空 `Id`、`Name`、`LocalHost`、`ServerAddress` validation 失败。
4. 非法端口 validation 失败或被表单端口解析拦截。
5. validation 失败时不调用 repository `SaveAsync`。

## 8. 删除行为

删除使用页面内二次确认，不使用 Dialog，不新增弹窗依赖：

1. 第一次点击“删除”显示确认状态和安全说明。
2. 第二次点击“确认删除”才调用 `TunnelProfileService.DeleteAsync(id)`。
3. 删除只删除 LiteDB profile。
4. 删除不会停止任何正在运行的 `frpc`。
5. 删除不会控制 daemon。

## 9. UI 说明

UI 使用隧道配置页内嵌表单：

1. 左侧为持久化 profile 列表。
2. 右侧为新增/编辑表单。
3. 顶部提供“刷新列表”和“新增配置”。
4. 表单区提供“编辑”“删除/确认删除”“保存配置”“取消”。

UI 继续遵守 `DESIGN.md`：

1. 中文界面。
2. WinUI 3-inspired light theme。
3. 白色 card、弱边框、无阴影。
4. 使用现有 Button/TextBox/CheckBox/ComboBox 控件风格。
5. 未新增随机颜色。

删除确认文案明确说明：只删除配置，不会停止任何正在运行的 `frpc`。

## 10. 测试覆盖

扩展 Desktop ViewModel tests：

1. 初始化加载 profiles 并显示列表。
2. 新增 TCP profile 成功保存并刷新列表。
3. 新增 UDP profile 成功保存并刷新列表。
4. HTTP profile validation 失败，不调用 `SaveAsync`。
5. HTTPS profile validation 失败，不调用 `SaveAsync`。
6. 空 `Id` / `Name` / `LocalHost` / `ServerAddress` 显示错误，不保存。
7. 非法端口显示错误，不保存。
8. 编辑已有 profile 时 `Id` 保持不变。
9. 编辑已有 profile 后保存更新字段。
10. 删除已有 profile 只调用 `DeleteAsync(id)`。
11. 删除不存在 profile 显示中文错误。
12. 刷新列表失败时显示中文错误。
13. 保存失败时显示中文错误。
14. 删除失败时显示中文错误。
15. CRUD 操作不会调用任何 daemon/frpc 相关逻辑。

测试使用 fake repository 和 fake validation，不访问真实用户 LiteDB，不启动 Desktop GUI，不启动真实 `frpc`，不调用 `systemctl`。

## 11. 明确未实现内容

Phase 9B 明确未实现：

1. 启动真实 `frpc`。
2. 停止真实 `frpc`。
3. 重启真实 `frpc`。
4. daemon 控制。
5. 实时日志流。
6. `IAvalonDaemon.StartAsync/StopAsync/RestartAsync` 调用。
7. `Process.Start` 调用。
8. `systemctl` 调用。
9. service unit 文件写入。
10. 自动下载 `frpc`。
11. PATH 搜索。
12. CLI 修改。
13. 新增 NuGet package。
14. CI、安装器、自动更新、遥测。
15. Phase 9C 或其他阶段。
16. push。

## 12. 验证命令

Phase 9B 验证入口：

```powershell
dotnet restore "Arturia.FrpNexus.sln"
dotnet build "Arturia.FrpNexus.sln" --no-restore
dotnet test "Arturia.FrpNexus.sln" --no-build
```

默认不执行：

```powershell
dotnet run --project "src/Arturia.FrpNexus.Desktop/Arturia.FrpNexus.Desktop.csproj"
```

如需 Desktop smoke test，应由用户单独确认，并记录真实用户 LiteDB 路径访问风险。

## 13. 需要人工确认的 GUI 行为

1. 隧道页新增 TCP profile 后列表刷新并显示新 profile。
2. 隧道页新增 UDP profile 后列表刷新并显示新 profile。
3. HTTP/HTTPS 保存时显示当前不支持，不写入 LiteDB。
4. 编辑 profile 时 `Id` 不可编辑。
5. 删除第一次点击进入确认状态。
6. 第二次点击“确认删除”后只删除 profile。
7. 删除操作不会停止任何正在运行的 `frpc`。
8. 刷新列表、取消编辑、validation 错误文案显示正常。
