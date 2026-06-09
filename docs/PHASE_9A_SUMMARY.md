# Phase 9A Summary

## 1. Phase 9A 目标

Phase 9A 将 Desktop GUI 初步接入 Phase 8A/8B 已有 LiteDB 配置持久化能力。

本阶段只实现：

1. 设置页读取和保存 `frpcPath`。
2. 设置页读取和保存 `minimizeToTrayOnClose`。
3. Desktop 关闭窗口到托盘行为使用持久化后的 `minimizeToTrayOnClose`。
4. 隧道页只读显示 LiteDB profiles。
5. profiles 为空时显示中文空状态。
6. LiteDB 打开失败时页面显示中文错误状态，应用继续启动。

## 2. 当前分支

```text
feature/phase-9a-desktop-lite-db-integration
```

Phase 9A 从 `develop` 创建并切换到该 feature 分支实现。

## 3. 创建的文件

```text
src/Arturia.FrpNexus.Desktop/Composition/DesktopCompositionRoot.cs
tests/Arturia.FrpNexus.Tests/Desktop/SettingsPageViewModelTests.cs
tests/Arturia.FrpNexus.Tests/Desktop/TunnelsPageViewModelTests.cs
docs/PHASE_9A_SUMMARY.md
```

## 4. 修改的文件

```text
src/Arturia.FrpNexus.Desktop/App.axaml.cs
src/Arturia.FrpNexus.Desktop/Views/MainWindow.axaml
src/Arturia.FrpNexus.Desktop/Views/MainWindow.axaml.cs
src/Arturia.FrpNexus.Desktop/ViewModels/MainWindowViewModel.cs
src/Arturia.FrpNexus.Desktop/ViewModels/Pages/SettingPreviewViewModel.cs
src/Arturia.FrpNexus.Desktop/ViewModels/Pages/SettingsPageViewModel.cs
src/Arturia.FrpNexus.Desktop/ViewModels/Pages/TunnelsPageViewModel.cs
tests/Arturia.FrpNexus.Tests/Arturia.FrpNexus.Tests.csproj
```

## 5. Desktop composition root

新增最小手动 composition root，不引入 DI 容器或新 NuGet package。

注册内容：

1. `FrpNexusDatabasePathProvider`
2. `LiteDbConnectionFactory`
3. `LiteDbFrpNexusSettingsStore`
4. `LiteDbTunnelProfileRepository`
5. `SettingsService`
6. `TunnelProfileService`

Desktop 运行时允许读取真实用户配置目录中的 `frpnexus.db`。测试使用 fake store/repository，不污染真实用户配置目录。

## 6. Settings 持久化行为

设置页现在：

1. 启动时读取 `FrpcPath`。
2. 启动时读取 `MinimizeToTrayOnClose`。
3. 点击“保存设置”时一次性保存 `frpcPath` 和 `minimizeToTrayOnClose`。
4. 不在 checkbox 改变时自动写库。
5. 不校验 `frpcPath` 是否存在。
6. 不搜索 PATH。
7. 不自动下载 `frpc`。

托盘说明文案：

```text
托盘设置只影响窗口关闭行为，不会启动、停止或后台运行穿透进程。
```

## 7. Tunnels 只读 profiles 行为

隧道页现在：

1. 只调用 `TunnelProfileService.ListAsync()`。
2. 将 profiles 映射为只读展示项。
3. 空列表显示：

```text
暂无持久化隧道配置。可先通过 CLI profile add 创建配置。
```

本阶段不调用 `SaveAsync`，不调用 `DeleteAsync`，不生成 TOML，不启动 `frpc`。

## 8. LiteDB 打开失败处理

如果 settings 或 profiles 读取/保存失败：

1. Desktop App 继续启动。
2. 对应页面显示中文错误状态。
3. 不自动删除 DB。
4. 不自动覆盖 DB。
5. 不自动重命名 DB。
6. 不自动重建 DB。
7. 不自动修复或迁移 DB。

## 9. 测试覆盖

新增 Desktop ViewModel 测试：

1. settings 初始化读取持久化 `frpcPath`。
2. settings 初始化读取持久化 `minimizeToTrayOnClose`。
3. settings 保存时一次性保存两个字段。
4. settings 读取失败时显示中文错误并保留默认值。
5. tunnels 空列表显示中文空状态。
6. tunnels profiles 映射为只读展示项。
7. tunnels 读取失败时显示中文错误。
8. tunnels 初始化不调用 profile `SaveAsync` 或 `DeleteAsync`。

## 10. 验证

已执行：

```powershell
dotnet restore "Arturia.FrpNexus.sln"
dotnet build "Arturia.FrpNexus.sln" --no-restore
dotnet test "Arturia.FrpNexus.sln" --no-build
dotnet run --project "src/Arturia.FrpNexus.Desktop/Arturia.FrpNexus.Desktop.csproj"
```

结果：

1. Restore 成功。
2. Build 成功，最终重跑结果为 `0` warning，`0` error。
3. Test 成功，`76` passed，`0` failed。
4. Desktop run 10 秒内未立即崩溃、无错误输出；GUI 进程持续运行，验证命令超时结束。

### 10.1 Desktop runtime LiteDB 路径说明

Phase 9A 测试没有污染真实用户配置目录。Desktop ViewModel tests 使用 fake settings store / fake repository，不访问真实 LiteDB 文件。

Desktop run smoke test 按 Phase 9A 设计会使用真实 Desktop runtime 配置路径。本次只读 closure 检查检测到真实用户 LiteDB 文件存在：

```text
C:\Users\Arturia\AppData\Roaming\Arturia\FrpNexus\frpnexus.db
```

检测结果：

1. 文件长度：`8192 bytes`。
2. LastWriteTime：`2026/5/21 13:47:20`。
3. 只读检查无法严格区分该文件是本次 Desktop smoke test 创建、更新，还是之前已经存在。
4. 但该路径确实是 Desktop runtime 会使用的真实用户配置路径。
5. 后续如果需要完全无副作用的 Desktop smoke test，应引入 Desktop runtime DB path override 或测试模式。

## 11. 明确未实现内容

Phase 9A 明确未实现：

1. GUI profile add/edit/delete。
2. GUI 调用 `ITunnelProfileRepository.SaveAsync`。
3. GUI 调用 `ITunnelProfileRepository.DeleteAsync`。
4. GUI 启动或停止真实 `frpc`。
5. GUI 控制 daemon。
6. GUI 实时日志流。
7. GUI 生成生产 TOML。
8. `IAvalonDaemon.StartAsync/StopAsync/RestartAsync` 调用。
9. `Process.Start` 调用。
10. CLI 修改。
11. `systemctl` 调用。
12. service unit 文件写入。
13. 自动下载 `frpc`。
14. PATH 搜索。
15. 新增 NuGet package。
16. CI、安装器、自动更新、遥测。
17. Phase 9B 或其他阶段。
18. push。

## 12. 需要人工确认的 GUI 行为

1. 设置页能显示真实 LiteDB 中的 `frpcPath`。
2. 点击“保存设置”后，重启 Desktop 仍保留 `frpcPath`。
3. 点击“保存设置”后，重启 Desktop 仍保留“关闭窗口时最小化到托盘”。
4. 启用持久化托盘设置后关闭窗口会隐藏到托盘。
5. 隧道页能显示 CLI 创建的 profiles。
6. 空库时隧道页显示中文空状态。
7. DB 无法打开时页面显示中文错误且应用不崩溃。
