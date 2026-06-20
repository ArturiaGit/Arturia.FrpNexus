using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Desktop.Services;

namespace Arturia.FrpNexus.Tests.Desktop;

public sealed class LocalApplicationLogServiceTests : IDisposable
{
    private readonly string _logDirectory;

    public LocalApplicationLogServiceTests()
    {
        _logDirectory = Path.Combine(Path.GetTempPath(), "FrpNexusLogTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_logDirectory);
    }

    [Fact]
    public async Task ReadRecentLogsAsync_ShouldReturnEmptyWhenDirectoryDoesNotExist()
    {
        var missingDirectory = Path.Combine(_logDirectory, "missing");
        var service = new LocalApplicationLogService(missingDirectory);

        var logs = await service.ReadRecentLogsAsync();

        Assert.Empty(logs);
    }

    [Fact]
    public async Task ReadRecentLogsAsync_ShouldParseSerilogWarningAndErrorLines()
    {
        File.WriteAllLines(
            Path.Combine(_logDirectory, "frpnexus-20260615.log"),
            [
                "[2026-06-15 10:00:00.000 +08:00 WRN] 节点连接超时",
                "[2026-06-15 10:00:01.000 +08:00 ERR] 本地 frpc 启动失败"
            ]);
        var service = new LocalApplicationLogService(_logDirectory);

        var logs = await service.ReadRecentLogsAsync();

        Assert.Equal(2, logs.Count);
        Assert.All(logs, log =>
        {
            Assert.Equal("客户端", log.NodeName);
            Assert.Equal("FrpNexus", log.ProcessName);
        });
        Assert.Contains(logs, log => log.Level == "WARN" && log.Status == FrpNexusStatus.Warning && log.Message == "节点连接超时");
        Assert.Contains(logs, log => log.Level == "ERROR" && log.Status == FrpNexusStatus.Error && log.Message == "本地 frpc 启动失败");
    }

    [Fact]
    public async Task ReadRecentLogsAsync_ShouldUseOriginalSerilogTimestampInsteadOfReadTime()
    {
        File.WriteAllLines(
            Path.Combine(_logDirectory, "frpnexus-20260615.log"),
            [
                "[2026-06-15 10:00:00.123 +08:00 ERR] local frpc failed",
                "   at System.Diagnostics.Process.Start(ProcessStartInfo startInfo)"
            ]);
        var service = new LocalApplicationLogService(_logDirectory);

        var logs = await service.ReadRecentLogsAsync();

        Assert.Equal(2, logs.Count);
        Assert.Equal("2026-06-15 10:00:00.123 +08:00", logs[0].Timestamp);
        Assert.Equal("未知时间", logs[1].Timestamp);
    }

    [Fact]
    public async Task ReadRecentLogsAsync_ShouldStripAnsiControlSequences()
    {
        File.WriteAllLines(
            Path.Combine(_logDirectory, "frpnexus-20260615.log"),
            [
                "[2026-06-15 10:00:00.000 +08:00 ERR] \u001b[31mcreate server listener error, listen tcp 0.0.0.0:7000: bind: address already in use\u001b[0m"
            ]);
        var service = new LocalApplicationLogService(_logDirectory);

        var logs = await service.ReadRecentLogsAsync();

        var log = Assert.Single(logs);
        Assert.DoesNotContain("\u001b", log.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("[0m", log.Message, StringComparison.Ordinal);
        Assert.Equal("create server listener error, listen tcp 0.0.0.0:7000: bind: address already in use", log.Message);
    }

    [Fact]
    public async Task ReadRecentLogsAsync_ShouldReadMostRecentLinesAcrossRollingFiles()
    {
        var oldFile = Path.Combine(_logDirectory, "frpnexus-20260614.log");
        var newFile = Path.Combine(_logDirectory, "frpnexus-20260615.log");
        File.WriteAllLines(oldFile, ["[2026-06-14 23:59:58.000 +08:00 WRN] old one"]);
        File.WriteAllLines(newFile, ["[2026-06-15 10:00:00.000 +08:00 WRN] new one", "[2026-06-15 10:00:01.000 +08:00 ERR] new two"]);
        File.SetLastWriteTimeUtc(oldFile, DateTime.UtcNow.AddMinutes(-10));
        File.SetLastWriteTimeUtc(newFile, DateTime.UtcNow);
        var service = new LocalApplicationLogService(_logDirectory);

        var logs = await service.ReadRecentLogsAsync(lineCount: 2);

        Assert.Equal(2, logs.Count);
        Assert.DoesNotContain(logs, log => log.Message == "old one");
        Assert.Equal(["new one", "new two"], logs.Select(log => log.Message).ToArray());
    }

    public void Dispose()
    {
        if (Directory.Exists(_logDirectory))
        {
            Directory.Delete(_logDirectory, recursive: true);
        }
    }
}
