using System.Threading.Tasks;

namespace BrewAlert.UI.Services;

public interface IUpdateService
{
    event Action? UpdateAvailable;
    Task<bool> CheckForUpdatesAsync();
    Task DownloadAndInstallUpdatesAsync();
    bool IsUpdateAvailable { get; }
    string CurrentVersion { get; }
}
