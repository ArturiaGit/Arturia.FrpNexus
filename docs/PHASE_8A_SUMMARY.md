# Phase 8A Summary

## 1. Phase 8A 目标

Phase 8A 建立基于 LiteDB 的本地嵌入式配置持久化基础，为后续 CLI 与 GUI 共享真实配置数据做准备。

本阶段只实现并测试 repository、settings store 和数据库路径 provider，不让 CLI 或 GUI 实际消费 LiteDB 数据。

## 2. 当前分支

```text
feature/phase-8a-config-persistence
```

Phase 8A 从 `develop` 创建并切换到该 feature 分支实现。

## 3. 创建的文件

```text
src/Arturia.FrpNexus.Core/Configuration/FrpNexusSettings.cs
src/Arturia.FrpNexus.Core/Configuration/IFrpNexusDatabasePathProvider.cs
src/Arturia.FrpNexus.Core/Configuration/IFrpNexusSettingsStore.cs
src/Arturia.FrpNexus.Core/ExcaliburTunnel/ITunnelProfileRepository.cs

src/Arturia.FrpNexus.Application/Configuration/SettingsService.cs
src/Arturia.FrpNexus.Application/ExcaliburTunnel/TunnelProfileService.cs

src/Arturia.FrpNexus.Infrastructure/Configuration/FrpNexusDatabasePathProvider.cs
src/Arturia.FrpNexus.Infrastructure/Configuration/FrpNexusStorageException.cs
src/Arturia.FrpNexus.Infrastructure/Configuration/LiteDbConnectionFactory.cs
src/Arturia.FrpNexus.Infrastructure/Configuration/LiteDbFrpNexusSettingsStore.cs
src/Arturia.FrpNexus.Infrastructure/ExcaliburTunnel/LiteDbTunnelProfileRepository.cs

tests/Arturia.FrpNexus.Tests/Configuration/FrpNexusDatabasePathProviderTests.cs
tests/Arturia.FrpNexus.Tests/Configuration/LiteDbConnectionFactoryTests.cs
tests/Arturia.FrpNexus.Tests/Configuration/LiteDbFrpNexusSettingsStoreTests.cs
tests/Arturia.FrpNexus.Tests/Configuration/TemporaryDatabasePathProvider.cs
tests/Arturia.FrpNexus.Tests/ExcaliburTunnel/LiteDbTunnelProfileRepositoryTests.cs

docs/PHASE_8A_SUMMARY.md
```

## 4. 修改的文件

```text
src/Arturia.FrpNexus.Infrastructure/Arturia.FrpNexus.Infrastructure.csproj
```

## 5. 新增 NuGet package

Phase 8A 只新增：

```text
LiteDB 5.0.21
```

新增位置：

```text
src/Arturia.FrpNexus.Infrastructure/Arturia.FrpNexus.Infrastructure.csproj
```

未新增 ORM、SQLite、配置框架、mocking、coverage、CI 或 GUI testing 依赖。

## 6. LiteDB collection 设计

### 6.1 settings

Collection 名称：

```text
settings
```

单文档固定 `_id`：

```text
default
```

字段：

```text
version
frpcPath
minimizeToTrayOnClose
activeProfileId
```

### 6.2 tunnel_profiles

Collection 名称：

```text
tunnel_profiles
```

`_id` 使用 `TunnelProfile.Id`。

字段：

```text
id
name
protocol
localHost
localPort
remotePort
serverAddress
serverPort
enabled
```

## 7. DB 路径策略

数据库文件名：

```text
frpnexus.db
```

平台路径：

```text
Windows: %APPDATA%\Arturia\FrpNexus\frpnexus.db
Linux: $XDG_CONFIG_HOME/frpnexus/frpnexus.db，fallback ~/.config/frpnexus/frpnexus.db
macOS: ~/Library/Application Support/Arturia/FrpNexus/frpnexus.db
```

实现文件：

```text
src/Arturia.FrpNexus.Infrastructure/Configuration/FrpNexusDatabasePathProvider.cs
```

测试使用 `TemporaryDatabasePathProvider` 注入临时目录，不写入真实用户配置目录。

## 8. Repository 和 Store

### 8.1 settings store

接口：

```text
IFrpNexusSettingsStore
```

实现：

```text
LiteDbFrpNexusSettingsStore
```

行为：

1. 无 DB 或无 settings document 时返回 `FrpNexusSettings.Default`。
2. `SaveAsync` 使用固定 `_id = default` upsert settings document。
3. 持久化 `version`、`frpcPath`、`minimizeToTrayOnClose`、`activeProfileId`。

### 8.2 tunnel profile repository

接口：

```text
ITunnelProfileRepository
```

实现：

```text
LiteDbTunnelProfileRepository
```

行为：

1. `ListAsync` 返回所有 profiles。
2. `FindByIdAsync` 找不到返回 `null`。
3. `SaveAsync` 采用 upsert 策略。
4. `SaveAsync` 遇到空白 `Id` 抛 `ArgumentException`。
5. `DeleteAsync` 删除存在 profile 返回 `true`。
6. `DeleteAsync` 删除不存在 profile 返回 `false`。

## 9. DB 打开失败处理

新增异常：

```text
FrpNexusStorageException
```

策略：

1. 打开 DB 失败时抛明确异常。
2. 错误信息包含数据库路径。
3. 不自动删除 DB。
4. 不自动覆盖 DB。
5. 不自动重命名为 `.corrupt`。
6. 不自动重建默认 DB。

## 10. 明确未实现内容

Phase 8A 明确未实现：

1. 未改变 `run <profileId>` 行为。
2. 未新增 CLI config get/set 命令。
3. 未修改 `src/Arturia.FrpNexus.Cli/Program.cs`。
4. 未修改 Desktop Program、App、MainWindow 或 GUI 行为文件。
5. 未让托盘设置跨重启生效。
6. 未做 GUI profile CRUD。
7. 未存储 token、密码或敏感凭据。
8. 未做加密密钥管理。
9. 未自动下载 `frpc`。
10. 未自动搜索 PATH。
11. 未启动真实 `frpc`。
12. 未调用 `systemctl`。
13. 未写 service unit。
14. 未新增除 LiteDB 以外的持久化技术。
15. 未新增除 LiteDB 5.0.21 以外的 NuGet package。
16. 未新增 CI、安装器、自动更新或遥测。
17. 未修改 Phase 6、Phase 7A、Phase 7B 的既有行为语义。
18. 未进入 Phase 8B、Phase 9 或其他阶段。

## 11. 测试覆盖

Phase 8A 新增测试覆盖：

1. settings 默认值。
2. 首次无 DB 或无 settings document 时返回默认 settings。
3. 保存后可读取 settings。
4. `frpcPath` 持久化。
5. `minimizeToTrayOnClose` 持久化。
6. `activeProfileId` 持久化。
7. 空数据库 `ListAsync` 返回空列表。
8. `SaveAsync` 新增 profile。
9. `SaveAsync` 更新同 Id profile。
10. `DeleteAsync` 删除存在 profile 返回 `true`。
11. `DeleteAsync` 删除不存在 profile 返回 `false`。
12. `FindByIdAsync` 找不到返回 `null`。
13. 多个 profiles 可列出。
14. 空白 Id 保存失败。
15. DB 打开失败抛明确异常。
16. 测试使用临时目录和临时 LiteDB 文件，不污染真实用户配置目录。

## 12. 验证命令

Phase 8A 验证入口：

```powershell
dotnet restore "Arturia.FrpNexus.sln"
dotnet build "Arturia.FrpNexus.sln" --no-restore
dotnet test "Arturia.FrpNexus.sln" --no-build
```

实际执行结果以 Phase 8A closure report 为准。
