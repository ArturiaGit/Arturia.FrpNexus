using System;
using System.Threading;
using System.Threading.Tasks;
using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Desktop.ViewModels;

namespace Arturia.FrpNexus.Desktop.ViewModels.Nodes;

public interface INodeCredentialWorkflow
{
    Task<NodeCredentialResult> CreateCredentialAsync(
        NodeCredentialInput input,
        CancellationToken cancellationToken = default);

    Task<SavedSessionPasswordState> SaveRememberedSessionPasswordIfNeededAsync(
        string nodeName,
        bool isSessionPasswordMode,
        bool rememberSessionPassword,
        string? typedSessionPassword,
        CancellationToken cancellationToken = default);

    Task<SavedSessionPasswordState> GetSavedSessionPasswordStateAsync(
        string? nodeName,
        CancellationToken cancellationToken = default);

    Task DeleteSessionPasswordAsync(
        string nodeName,
        CancellationToken cancellationToken = default);
}

public sealed class NodeCredentialWorkflow(INodeCredentialSecretService nodeCredentialSecretService) : INodeCredentialWorkflow
{
    public async Task<NodeCredentialResult> CreateCredentialAsync(
        NodeCredentialInput input,
        CancellationToken cancellationToken = default)
    {
        var resolvedSessionPassword = input.SessionPassword;
        if (NodeConnectionWorkflowHelpers.TryParseAuthenticationMode(input.SelectedSshAuthenticationMode, out var mode)
            && mode == SshAuthenticationMode.SessionPassword
            && string.IsNullOrWhiteSpace(resolvedSessionPassword)
            && !string.IsNullOrWhiteSpace(input.NodeName))
        {
            try
            {
                resolvedSessionPassword = await nodeCredentialSecretService.GetSessionPasswordAsync(
                    input.NodeName,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return NodeCredentialResult.Failed(
                    "读取保存密码失败",
                    ViewModelErrorText.ForUser("保存密码读取", ex),
                    "error");
            }
        }

        return CreateCredential(input, resolvedSessionPassword);
    }

    public async Task<SavedSessionPasswordState> SaveRememberedSessionPasswordIfNeededAsync(
        string nodeName,
        bool isSessionPasswordMode,
        bool rememberSessionPassword,
        string? typedSessionPassword,
        CancellationToken cancellationToken = default)
    {
        if (!isSessionPasswordMode
            || !rememberSessionPassword
            || string.IsNullOrWhiteSpace(typedSessionPassword))
        {
            return new SavedSessionPasswordState(false, "未保存会话密码。", false);
        }

        await nodeCredentialSecretService.SaveSessionPasswordAsync(
            nodeName,
            typedSessionPassword,
            cancellationToken);
        return new SavedSessionPasswordState(true, "已保存会话密码，可直接连接。", false);
    }

    public async Task<SavedSessionPasswordState> GetSavedSessionPasswordStateAsync(
        string? nodeName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(nodeName))
        {
            return new SavedSessionPasswordState(false, "未保存会话密码。", true);
        }

        try
        {
            var hasSavedPassword = await nodeCredentialSecretService.HasSessionPasswordAsync(
                nodeName,
                cancellationToken);
            return hasSavedPassword
                ? new SavedSessionPasswordState(true, "已保存会话密码，可直接连接。", false)
                : new SavedSessionPasswordState(false, "未保存会话密码。", true);
        }
        catch
        {
            return new SavedSessionPasswordState(false, "保存密码状态读取失败。", false);
        }
    }

    public Task DeleteSessionPasswordAsync(
        string nodeName,
        CancellationToken cancellationToken = default)
    {
        return nodeCredentialSecretService.DeleteSessionPasswordAsync(nodeName, cancellationToken);
    }

    private static NodeCredentialResult CreateCredential(
        NodeCredentialInput input,
        string? resolvedSessionPassword)
    {
        if (!NodeConnectionWorkflowHelpers.TryParseAuthenticationMode(input.SelectedSshAuthenticationMode, out var mode))
        {
            return NodeCredentialResult.Failed(
                "请选择认证方式",
                "请选择有效的 SSH 认证方式。",
                "warning");
        }

        if (mode == SshAuthenticationMode.SessionPassword && string.IsNullOrWhiteSpace(resolvedSessionPassword))
        {
            return NodeCredentialResult.Failed(
                "缺少会话密码",
                "请输入本次会话使用的 SSH 密码，或使用已保存的会话密码。",
                "warning");
        }

        if (mode == SshAuthenticationMode.PrivateKey && string.IsNullOrWhiteSpace(input.PrivateKeyPath))
        {
            return NodeCredentialResult.Failed(
                "缺少私钥路径",
                "请输入私钥文件路径，私钥内容和 passphrase 不会保存到 SQLite。",
                "warning");
        }

        return NodeCredentialResult.Succeeded(new SshCredentialReference(
            mode,
            string.IsNullOrWhiteSpace(input.PrivateKeyPath) ? null : input.PrivateKeyPath.Trim(),
            string.IsNullOrWhiteSpace(resolvedSessionPassword) ? null : resolvedSessionPassword,
            string.IsNullOrWhiteSpace(input.PrivateKeyPassphrase) ? null : input.PrivateKeyPassphrase));
    }
}

public sealed record NodeCredentialInput(
    string? NodeName,
    string SelectedSshAuthenticationMode,
    string? SessionPassword,
    string? PrivateKeyPath,
    string? PrivateKeyPassphrase);

public sealed record NodeCredentialResult(
    bool Success,
    SshCredentialReference Credential,
    string StatusTitle,
    string StatusMessage,
    string StatusSeverity)
{
    public static NodeCredentialResult Succeeded(SshCredentialReference credential)
    {
        return new NodeCredentialResult(true, credential, string.Empty, string.Empty, string.Empty);
    }

    public static NodeCredentialResult Failed(string title, string message, string severity)
    {
        return new NodeCredentialResult(
            false,
            new SshCredentialReference(SshAuthenticationMode.SessionPassword),
            title,
            message,
            severity);
    }
}

public sealed record SavedSessionPasswordState(
    bool HasSavedSessionPassword,
    string Text,
    bool ShouldClearRememberSessionPassword);
