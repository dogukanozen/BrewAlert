using System.Threading.Tasks;

namespace BrewAlert.UI.Services;

public interface IUpdateService
{
    event Action? UpdateAvailable;
    Task<bool> CheckForUpdatesAsync();
    Task DownloadAndInstallUpdatesAsync();  // update must already be downloaded via CheckForUpdatesAsync
    bool IsUpdateAvailable { get; }
    string CurrentVersion { get; }
}
