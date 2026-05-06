using System.Threading.Tasks;

namespace BrewAlert.UI.Services;

public interface IUpdateService
{
    Task<bool> CheckForUpdatesAsync();
    Task DownloadAndInstallUpdatesAsync();
    bool IsUpdateAvailable { get; }
    string CurrentVersion { get; }
}
