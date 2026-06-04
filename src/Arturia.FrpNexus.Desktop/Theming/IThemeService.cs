using System.Threading;
using System.Threading.Tasks;

namespace Arturia.FrpNexus.Desktop.Theming;

public interface IThemeService
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    void ApplyTheme(string theme);
}
