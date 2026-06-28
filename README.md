# FrpNexus

> 本地优先的 FRP 桌面部署与运维控制台。

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)
![Avalonia](https://img.shields.io/badge/Avalonia-12-0B6B58)
![License](https://img.shields.io/badge/license-MIT-green)

FrpNexus 面向个人开发者、自托管用户和轻量运维场景，把 FRP 的下载、部署、配置、启动、日志查看和基础诊断整合进一个清晰的桌面应用。

## 快速开始

从 [GitHub Releases](https://github.com/ArturiaGit/Arturia.FrpNexus/releases) 选择最新预览版。

推荐优先使用便携版：

1. 下载 `Arturia.FrpNexus-win-Portable.zip`
2. 解压到任意目录
3. 运行 `FrpNexus.exe`

如需安装向导（支持选择安装目录、添加桌面快捷方式），下载 `FrpNexus-Setup-0.4.0-preview.3.exe`。

## 下载选择

| 文件 | 说明 | 适用 |
|------|------|------|
| `Arturia.FrpNexus-win-Portable.zip` | 免安装便携版 | 优先体验、临时使用 |
| `FrpNexus-Setup-0.4.0-preview.3.exe` | Setup 安装向导 | 固定安装、需要快捷方式 |
| `FrpNexus-win-x64-0.4.0-preview.3.zip` | 完整发布包归档 | 归档和校验 |
| `FrpNexus-win-x64-0.4.0-preview.3.sha256.txt` | SHA256 校验清单 | 验证文件完整性 |

`Arturia.FrpNexus-win-Setup.exe` 为 Velopack 原生安装器，用于自动更新链路，暂不推荐普通用户直接使用。

## 首次使用引导

安装或解压后启动应用：

1. 添加远程 Linux 节点（需要 SSH 访问）
2. 测试 SSH 连接
3. 下载或选择 FRP 核心文件
4. 创建隧道配置（TCP、UDP、HTTP、HTTPS 等）
5. 生成并预览 TOML 配置
6. 上传配置并启动远程 FRP 进程
7. 查看日志和运行状态

## 安全与数据说明

- 当前预览版尚未使用可信代码签名证书签名，Windows 可能显示 SmartScreen 或安全软件警告
- 下载后可使用 SHA256 校验文件完整性
- 本地 SQLite 数据、日志、SSH 凭据、私钥、Token 和 FRP 缓存不会包含在发布包内
- 便携版暂不承诺数据完全随解压目录迁移

## 项目定位

FrpNexus 优先服务个人开发者、自托管用户和轻量运维场景。它不是云平台、Web Dashboard、营销页面，也不是单纯的配置文件编辑器。

项目强调：

- 本地优先
- 开源透明
- 不强制绑定云端账号
- 面向真实 FRP 部署和排障流程
- 使用中文优先的桌面工具体验

## 核心能力

当前项目围绕 FRP 的远程部署与运行管理展开，核心能力包括：

- 通过 SSH/SFTP 管理远程 Linux 节点
- 下载或选择本地 FRP 核心文件
- 上传 `frpc` / `frps` 到远程节点
- 通过表单生成和预览 TOML 配置
- 上传配置文件到远程节点
- 启动、停止和重启远程 FRP 进程
- 查看日志、状态和基础错误信息
- 管理 TCP、UDP、HTTP、HTTPS 等基础隧道配置
- 管理本地设置、日志、本地数据和安全认证相关配置

## 技术栈

- .NET 8
- Avalonia desktop UI
- CommunityToolkit.Mvvm
- Microsoft.Extensions.DependencyInjection
- SQLite
- Serilog
- SSH.NET / SFTP

## 仓库结构

当前公开仓库仅保留核心代码与必要工程文件：

```text
Arturia.FrpNexus.sln
Directory.Build.props
global.json
src/
```

主要项目：

- `Arturia.FrpNexus.Core`：核心模型、基础抽象和领域类型
- `Arturia.FrpNexus.Application`：应用服务接口与业务编排
- `Arturia.FrpNexus.Infrastructure`：SQLite、SSH、SFTP、FRP Release、运行时等基础设施实现
- `Arturia.FrpNexus.Desktop`：Avalonia 桌面应用、视图、样式、ViewModel 和桌面服务
- `Arturia.FrpNexus.Cli`：命令行入口与辅助命令

## 构建

需要安装 .NET SDK 8。

```powershell
dotnet restore
dotnet build Arturia.FrpNexus.sln
```

启动桌面应用：

```powershell
dotnet run --project src/Arturia.FrpNexus.Desktop/Arturia.FrpNexus.Desktop.csproj
```

启动 CLI：

```powershell
dotnet run --project src/Arturia.FrpNexus.Cli/Arturia.FrpNexus.Cli.csproj -- --help
```

## 当前状态

当前最新预览版为 `0.4.0-preview.3`，Windows x64 预览包已可在 GitHub Releases 下载。

- MSIX 打包暂未提供
- 静默自动更新暂未提供
- 代码签名待后续完善

公开仓库已经精简为核心代码视图；测试、设计稿、阶段文档和内部规划资料仅在本地保留，不再随 GitHub 仓库发布。

## 适用场景

FrpNexus 适合：

- 使用 VPS、云服务器、家用服务器或 NAS 的个人开发者
- 需要把本地 Web/API/SSH/RDP/NAS 服务暴露到公网的用户
- 熟悉基础网络概念，但不希望反复手写 FRP 配置和 SSH 命令的人
- 偏好本地可控工具的自托管爱好者

FrpNexus 当前不以团队权限、云端同步、计费系统、Kubernetes 集成或多租户平台能力为主要目标。

## License

FrpNexus is released under the MIT License. See [LICENSE](LICENSE) for details.
