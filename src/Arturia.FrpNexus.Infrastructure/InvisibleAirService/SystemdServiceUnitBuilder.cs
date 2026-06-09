using System.Text;
using Arturia.FrpNexus.Core.InvisibleAirService;

namespace Arturia.FrpNexus.Infrastructure.InvisibleAirService;

public sealed class SystemdServiceUnitBuilder
{
    public SystemdServiceUnitPreview BuildUserServiceUnit(SystemdServiceUnitRequest request)
    {
        var errors = Validate(request);
        var unitName = string.IsNullOrWhiteSpace(request.ProfileId)
            ? "frpnexus@<profile>.service"
            : $"frpnexus@{SanitizeUnitName(request.ProfileId)}.service";

        if (errors.Count > 0)
        {
            return new SystemdServiceUnitPreview(
                false,
                unitName,
                string.Empty,
                errors,
                GetSafetyNotes());
        }

        var content = new StringBuilder()
            .AppendLine("[Unit]")
            .AppendLine($"Description=FrpNexus user service preview for {request.ProfileId}")
            .AppendLine("After=network-online.target")
            .AppendLine("Wants=network-online.target")
            .AppendLine()
            .AppendLine("[Service]")
            .AppendLine("Type=simple")
            .AppendLine($"ExecStart={QuoteSystemdArgument(request.FrpNexusPath)} run {QuoteSystemdArgument(request.ProfileId)} --frpc-path {QuoteSystemdArgument(request.FrpcPath)}")
            .AppendLine("Restart=on-failure")
            .AppendLine("RestartSec=5s")
            .AppendLine()
            .AppendLine("[Install]")
            .AppendLine("WantedBy=default.target")
            .ToString();

        return new SystemdServiceUnitPreview(
            true,
            unitName,
            content,
            Array.Empty<string>(),
            GetSafetyNotes());
    }

    private static List<string> Validate(SystemdServiceUnitRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.ProfileId))
        {
            errors.Add("profileId 不能为空。");
        }

        if (string.IsNullOrWhiteSpace(request.FrpNexusPath))
        {
            errors.Add("--frpnexus-path 不能为空，Phase 7A 不假设 frpnexus 在 PATH 中。");
        }

        if (string.IsNullOrWhiteSpace(request.FrpcPath))
        {
            errors.Add("--frpc-path 不能为空，Phase 7A 不假设 frpc 在 PATH 中。");
        }

        if (ContainsControlCharacter(request.ProfileId))
        {
            errors.Add("profileId 不能包含控制字符。");
        }

        if (ContainsControlCharacter(request.FrpNexusPath))
        {
            errors.Add("--frpnexus-path 不能包含控制字符。");
        }

        if (ContainsControlCharacter(request.FrpcPath))
        {
            errors.Add("--frpc-path 不能包含控制字符。");
        }

        return errors;
    }

    private static bool ContainsControlCharacter(string? value)
    {
        return !string.IsNullOrEmpty(value) && value.Any(char.IsControl);
    }

    private static string SanitizeUnitName(string value)
    {
        var sanitized = new string(value.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' ? ch : '-').ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "profile" : sanitized;
    }

    private static string QuoteSystemdArgument(string value)
    {
        return $"\"{value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }

    private static IReadOnlyList<string> GetSafetyNotes()
    {
        return new[]
        {
            "Phase 7A 只输出 Linux user-level systemd unit preview。",
            "未安装 service，未写入 unit 文件。",
            "未执行 systemctl，未启用自启动，未启动后台服务。",
            "未调用 sudo、pkexec、UAC 或任何提权操作。",
            "未搜索 PATH，ExecStart 使用显式 frpnexus path 和 frpc path。"
        };
    }
}
