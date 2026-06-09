using Arturia.FrpNexus.Core.InvisibleAirService;
using Arturia.FrpNexus.Infrastructure.InvisibleAirService;

namespace Arturia.FrpNexus.Tests.InvisibleAirService;

public sealed class SystemdServiceUnitBuilderTests
{
    private readonly SystemdServiceUnitBuilder builder = new();

    [Fact]
    public void BuildUserServiceUnit_WithValidRequest_GeneratesExpectedUnitName()
    {
        var preview = builder.BuildUserServiceUnit(CreateRequest());

        Assert.True(preview.IsValid);
        Assert.Equal("frpnexus@my-server.service", preview.UnitName);
    }

    [Fact]
    public void BuildUserServiceUnit_WithValidRequest_IncludesExplicitExecStartArguments()
    {
        var preview = builder.BuildUserServiceUnit(CreateRequest());

        Assert.Contains("ExecStart=\"/opt/frpnexus/frpnexus\" run \"my-server\" --frpc-path \"/opt/frp/frpc\"", preview.UnitContent);
    }

    [Fact]
    public void BuildUserServiceUnit_WithUnsafeProfileId_SanitizesUnitName()
    {
        var preview = builder.BuildUserServiceUnit(CreateRequest(profileId: "my server:prod"));

        Assert.True(preview.IsValid);
        Assert.Equal("frpnexus@my-server-prod.service", preview.UnitName);
        Assert.Contains("run \"my server:prod\"", preview.UnitContent);
    }

    [Fact]
    public void BuildUserServiceUnit_WithEmptyFields_ReturnsInvalidPreview()
    {
        var preview = builder.BuildUserServiceUnit(new SystemdServiceUnitRequest(" ", string.Empty, " "));

        Assert.False(preview.IsValid);
        Assert.Empty(preview.UnitContent);
        Assert.Contains(preview.Errors, error => error.Contains("profileId", StringComparison.Ordinal));
        Assert.Contains(preview.Errors, error => error.Contains("frpnexus-path", StringComparison.Ordinal));
        Assert.Contains(preview.Errors, error => error.Contains("frpc-path", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildUserServiceUnit_WithControlCharacters_ReturnsInvalidPreview()
    {
        var preview = builder.BuildUserServiceUnit(new SystemdServiceUnitRequest("my\nserver", "/opt/frpnexus/frpnexus\r", "/opt/frp/frpc\t"));

        Assert.False(preview.IsValid);
        Assert.Contains(preview.Errors, error => error.Contains("profileId", StringComparison.Ordinal) && error.Contains("控制字符", StringComparison.Ordinal));
        Assert.Contains(preview.Errors, error => error.Contains("frpnexus-path", StringComparison.Ordinal) && error.Contains("控制字符", StringComparison.Ordinal));
        Assert.Contains(preview.Errors, error => error.Contains("frpc-path", StringComparison.Ordinal) && error.Contains("控制字符", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildUserServiceUnit_IncludesSafetyNotesWithoutInstallStartEnableCommands()
    {
        var preview = builder.BuildUserServiceUnit(CreateRequest());

        Assert.Contains(preview.SafetyNotes, note => note.Contains("只输出", StringComparison.Ordinal));
        Assert.Contains(preview.SafetyNotes, note => note.Contains("未执行 systemctl", StringComparison.Ordinal));
        Assert.DoesNotContain("systemctl", preview.UnitContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("daemon-reload", preview.UnitContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("systemctl enable", preview.UnitContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("systemctl start", preview.UnitContent, StringComparison.OrdinalIgnoreCase);
    }

    private static SystemdServiceUnitRequest CreateRequest(string profileId = "my-server")
    {
        return new SystemdServiceUnitRequest(profileId, "/opt/frpnexus/frpnexus", "/opt/frp/frpc");
    }
}
