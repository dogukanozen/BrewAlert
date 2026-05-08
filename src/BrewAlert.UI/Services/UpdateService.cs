using System;
using System.Reflection;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;
using Microsoft.Extensions.Logging;

namespace BrewAlert.UI.Services;

public class UpdateService : IUpdateService
{
    private readonly ILogger<UpdateService> _logger;
    private UpdateInfo? _updateInfo;
    private const string RepoUrl = "https://github.com/dogukanozen/brewalert";

    public event Action? UpdateAvailable;
    public bool IsUpdateAvailable => _updateInfo != null;
    public string CurrentVersion => Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";

    public UpdateService(ILogger<UpdateService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> CheckForUpdatesAsync()
    {
        try
        {
            var mgr = new UpdateManager(new GithubSource(RepoUrl, null, false));
            _updateInfo = await mgr.CheckForUpdatesAsync();
            if (_updateInfo != null)
                UpdateAvailable?.Invoke();
            return _updateInfo != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for updates from {RepoUrl}", RepoUrl);
            return false;
        }
    }

    public async Task DownloadAndInstallUpdatesAsync()
    {
        if (_updateInfo == null) return;

        try
        {
            var mgr = new UpdateManager(new GithubSource(RepoUrl, null, false));
            
            // Güncellemeyi indir
            await mgr.DownloadUpdatesAsync(_updateInfo);

            // systemd servisi ana PID çıkınca yeniden başlatır; Velopack'in
            // kendi restart mekanizması systemd ile çakışır, bu yüzden Exit kullanılır.
            mgr.ApplyUpdatesAndExit(_updateInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading or applying updates");
            throw;
        }
    }
}
