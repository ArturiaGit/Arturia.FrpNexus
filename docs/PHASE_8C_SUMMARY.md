# Phase 8C Summary

## 1. Phase 8C 目标

Phase 8C 的目标是建立测试基础设施，为 Phase 6 和 Phase 7A 中已经落地的纯逻辑提供首批自动化回归测试。

本阶段按用户确认重新编号：Phase 8C = Test Foundation。

## 2. 当前分支

```text
feature/phase-8c-test-foundation
```

Phase 8C 从 `develop` 创建并切换到该 feature 分支实现。

## 3. 创建的文件

```text
tests/Arturia.FrpNexus.Tests/Arturia.FrpNexus.Tests.csproj
tests/Arturia.FrpNexus.Tests/Cli/CliProfileFactoryTests.cs
tests/Arturia.FrpNexus.Tests/ExcaliburTunnel/FrpExcaliburTunnelValidationTests.cs
tests/Arturia.FrpNexus.Tests/ExcaliburTunnel/FrpTomlParserTests.cs
tests/Arturia.FrpNexus.Tests/ExcaliburTunnel/FrpTomlSerializerTests.cs
tests/Arturia.FrpNexus.Tests/InvisibleAirService/SystemdServiceUnitBuilderTests.cs

src/Arturia.FrpNexus.Cli/Properties/AssemblyInfo.cs

docs/PHASE_8C_SUMMARY.md
```

## 4. 修改的文件

```text
Arturia.FrpNexus.sln
```

## 5. 新增测试依赖

Phase 8C 只新增以下测试依赖：

```text
Microsoft.NET.Test.Sdk
xunit
xunit.runner.visualstudio
```

未新增 mocking framework、coverage 工具、GUI 自动化测试框架或 CI workflow。

## 6. 测试项目结构

```text
tests/
  Arturia.FrpNexus.Tests/
    Arturia.FrpNexus.Tests.csproj
    Cli/
      CliProfileFactoryTests.cs
    ExcaliburTunnel/
      FrpExcaliburTunnelValidationTests.cs
      FrpTomlParserTests.cs
      FrpTomlSerializerTests.cs
    InvisibleAirService/
      SystemdServiceUnitBuilderTests.cs
```

测试项目引用：

```text
src/Arturia.FrpNexus.Core/Arturia.FrpNexus.Core.csproj
src/Arturia.FrpNexus.Infrastructure/Arturia.FrpNexus.Infrastructure.csproj
src/Arturia.FrpNexus.Cli/Arturia.FrpNexus.Cli.csproj
```

## 7. InternalsVisibleTo

为测试 `CliProfileFactory`，Phase 8C 新增：

```text
src/Arturia.FrpNexus.Cli/Properties/AssemblyInfo.cs
```

内容为：

```csharp
[assembly: InternalsVisibleTo("Arturia.FrpNexus.Tests")]
```

这避免了将 `CliProfileFactory` 从 `internal` 改为 `public`，没有扩大生产 API。

## 8. 测试覆盖

### 8.1 FrpTomlSerializer

已覆盖：

1. TCP profile 生成 `serverAddr`、`serverPort`、`[[proxies]]`、`type = "tcp"`。
2. UDP profile 生成 `type = "udp"`。
3. 字符串转义，包括引号、反斜杠和换行。
4. HTTP/HTTPS 当前不支持并抛出异常。

### 8.2 FrpTomlParser

已覆盖：

1. 解析本项目 serializer 生成的 TCP TOML。
2. 解析本项目 serializer 生成的 UDP TOML。
3. 必填字段缺失时返回 invalid。
4. 端口非法时返回 invalid。
5. `type = "http"` / `type = "https"` 当前不支持。
6. 基础字符串反转义。

测试仍只覆盖本项目生成的最小 TOML 子集，不声明完整 TOML 兼容。

### 8.3 FrpExcaliburTunnel validation

已覆盖：

1. 有效 TCP 通过。
2. 有效 UDP 通过。
3. 空 `Id`、`Name`、`LocalHost`、`ServerAddress` 返回错误。
4. `LocalPort`、`RemotePort`、`ServerPort` 超出 `1-65535` 返回错误。
5. HTTP/HTTPS 返回当前 Phase 6 不支持错误。

### 8.4 SystemdServiceUnitBuilder

已覆盖：

1. 有效 request 生成 `frpnexus@my-server.service`。
2. `ExecStart` 包含显式 `frpnexus` path、`run`、profileId、`--frpc-path`、`frpc` path。
3. profileId 中不适合 unit name 的字符会被 sanitize。
4. 空 `profileId`、空 `frpnexusPath`、空 `frpcPath` 返回 invalid preview。
5. 控制字符返回 validation error。
6. 输出明确 safety notes。
7. `UnitContent` 不包含 `systemctl`、`daemon-reload`、`systemctl enable` 或 `systemctl start` 操作。

### 8.5 CliProfileFactory

已覆盖：

1. 默认创建 TCP profile。
2. `protocol = udp` 创建 UDP profile。
3. `protocol = http` / `https` 映射到对应 enum。
4. 未知 protocol 当前回退到 TCP。
5. 自定义 host、port、server 字段进入 `TunnelProfile`。

## 9. 明确未实现内容

Phase 8C 明确未实现：

1. 新产品功能。
2. Desktop GUI 行为修改。
3. CLI 命令语义修改。
4. Phase 6 `frpc` process management 语义修改。
5. Phase 7A systemd preview 语义修改。
6. 真实 `frpc` 启动测试。
7. `systemctl` 调用。
8. service unit 文件写入。
9. GUI 自动化测试。
10. CI workflow。
11. 数据库、配置持久化、安装器、发布脚本、自动更新、遥测。
12. mocking、coverage 或 GUI testing 依赖。
13. 完整 TOML parser 兼容声明。
14. Phase 8A、Phase 8B、Phase 9 或其他功能阶段。

## 10. 验证命令

Phase 8C 的验证入口：

```powershell
dotnet restore "Arturia.FrpNexus.sln"
dotnet build "Arturia.FrpNexus.sln" --no-restore
dotnet test "Arturia.FrpNexus.sln" --no-build
```

实际执行结果以 Phase 8C closure report 为准。
