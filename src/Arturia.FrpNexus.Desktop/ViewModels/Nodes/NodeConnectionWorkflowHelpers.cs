using System;
using System.IO;
using Arturia.FrpNexus.Application.Abstractions;

namespace Arturia.FrpNexus.Desktop.ViewModels.Nodes;

internal static class NodeConnectionWorkflowHelpers
{
    public const string DefaultRemoteCoreDirectory = "/opt/frp";
    public const string DefaultRemoteCoreFileName = "frps";
    public const string DefaultServerConfigFileName = "frps.toml";
    public const string DefaultServerConfigPath = "/opt/frp/frps.toml";

    public const string SessionPasswordAuthenticationOption = "会话密码";
    public const string PrivateKeyAuthenticationOption = "私钥文件";
    public const string SshAgentAuthenticationOption = "SSH Agent（暂未接入）";

    public static bool TryParseAuthenticationMode(string value, out SshAuthenticationMode mode)
    {
        if (string.Equals(value, "PrivateKey", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, PrivateKeyAuthenticationOption, StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "绉侀挜鏂囦欢", StringComparison.OrdinalIgnoreCase))
        {
            mode = SshAuthenticationMode.PrivateKey;
            return true;
        }

        if (string.Equals(value, "SshAgent", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "SSH Agent", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, SshAgentAuthenticationOption, StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "SSH Agent锛堟殏鏈帴鍏ワ級", StringComparison.OrdinalIgnoreCase))
        {
            mode = SshAuthenticationMode.SshAgent;
            return true;
        }

        if (string.Equals(value, "SessionPassword", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, SessionPasswordAuthenticationOption, StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "浼氳瘽瀵嗙爜", StringComparison.OrdinalIgnoreCase))
        {
            mode = SshAuthenticationMode.SessionPassword;
            return true;
        }

        mode = SshAuthenticationMode.SessionPassword;
        return false;
    }

    public static string CombineRemotePath(string directory, string fileName)
    {
        var normalizedDirectory = NormalizeRemotePath(directory);
        var normalizedFileName = string.IsNullOrWhiteSpace(fileName) ? DefaultRemoteCoreFileName : fileName.Trim();
        return string.Equals(normalizedDirectory, "/", StringComparison.Ordinal)
            ? $"/{normalizedFileName}"
            : $"{normalizedDirectory.TrimEnd('/')}/{normalizedFileName}";
    }

    public static string NormalizeRemotePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        var normalized = path.Trim().Replace('\\', '/');
        while (normalized.Contains("//", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("//", "/", StringComparison.Ordinal);
        }

        if (!normalized.StartsWith("/", StringComparison.Ordinal))
        {
            normalized = "/" + normalized;
        }

        return normalized.Length > 1 ? normalized.TrimEnd('/') : normalized;
    }

    public static string ResolveServerConfigPathFromRemoteDirectory(string directory)
    {
        return CombineRemotePath(
            string.IsNullOrWhiteSpace(directory) ? DefaultRemoteCoreDirectory : directory,
            DefaultServerConfigFileName);
    }

    public static string ResolveFrpDirectoryFromConfigPath(string configPath)
    {
        if (string.IsNullOrWhiteSpace(configPath))
        {
            return DefaultRemoteCoreDirectory;
        }

        var normalized = NormalizeRemotePath(configPath);
        var lastSlash = normalized.LastIndexOf('/');
        return lastSlash <= 0 ? DefaultRemoteCoreDirectory : normalized[..lastSlash];
    }

    public static string GetLocalFileName(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFileName(path.Trim());
        }
        catch
        {
            var normalized = path.Trim().Replace('\\', '/');
            var lastSlash = normalized.LastIndexOf('/');
            return lastSlash >= 0 ? normalized[(lastSlash + 1)..] : normalized;
        }
    }

    public static bool IsFrpsFileName(string fileName)
    {
        return string.Equals(fileName, "frps", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "frps.exe", StringComparison.OrdinalIgnoreCase);
    }

    public static string ValidateRemoteCoreDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return "远程目录不能为空，例如 /opt/frp。";
        }

        var trimmed = directory.Trim();
        if (!trimmed.StartsWith("/", StringComparison.Ordinal))
        {
            return "远程目录必须是 Linux 绝对路径，例如 /opt/frp。";
        }

        if (trimmed.Contains('\0'))
        {
            return "远程目录不能包含空字符。";
        }

        return string.Empty;
    }

    public static string ValidateRemoteCoreFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "远程文件名不能为空，例如 frps。";
        }

        if (fileName.Contains('/', StringComparison.Ordinal)
            || fileName.Contains('\\', StringComparison.Ordinal)
            || fileName.Contains('\0'))
        {
            return "远程文件名不能包含路径分隔符，请只填写文件名 frps。";
        }

        return IsFrpsFileName(fileName) ? string.Empty : "远程 VPS 节点只支持上传 frps。";
    }
}
