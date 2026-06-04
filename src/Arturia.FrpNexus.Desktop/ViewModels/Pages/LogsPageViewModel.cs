using System.Collections.ObjectModel;
using Arturia.FrpNexus.Core.Models;

namespace Arturia.FrpNexus.Desktop.ViewModels.Pages;

public sealed class LogsPageViewModel : PageViewModel
{
    public LogsPageViewModel()
        : base("日志", "筛选、搜索并查看远程 FRP 日志输出")
    {
        Logs =
        [
            new("2026-06-04 14:32:01.102", "INFO", "US-West-01", "frpc", "FrpNexus client daemon started successfully. Version: V2.4.0", FrpNexusStatus.Ready),
            new("2026-06-04 14:32:01.150", "INFO", "US-West-01", "frpc", "Reading configuration from C:\\ProgramData\\FrpNexus\\frpc.toml", FrpNexusStatus.Ready),
            new("2026-06-04 14:32:02.045", "INFO", "US-West-01", "frpc", "Connection to control server established.", FrpNexusStatus.Online),
            new("2026-06-04 14:35:12.880", "WARN", "DB-Node-SH", "frpc", "Proxy [db_backup_sync] connection timeout. Retrying in 5 seconds...", FrpNexusStatus.Warning),
            new("2026-06-04 14:35:28.105", "ERROR", "DB-Node-SH", "frpc", "Failed to establish proxy [db_backup_sync]. Reason: remote server closed connection unexpectedly. EOF.", FrpNexusStatus.Error),
            new("2026-06-04 14:36:00.001", "INFO", "US-West-01", "frpc", "Heartbeat sent to control server. Latency: 42ms.", FrpNexusStatus.Ready)
        ];
    }

    public ObservableCollection<LogEntry> Logs { get; }
}
