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
            var info = await mgr.CheckForUpdatesAsync();
            if (info != null)
            {
                await mgr.DownloadUpdatesAsync(info);
                _updateInfo = info;
                UpdateAvailable?.Invoke();
            }
            return _updateInfo != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking or downloading updates from {RepoUrl}", RepoUrl);
            return false;
        }
    }

    public Task DownloadAndInstallUpdatesAsync()
    {
        if (_updateInfo == null) return Task.CompletedTask;

        try
        {
            var mgr = new UpdateManager(new GithubSource(RepoUrl, null, false));
            // Update is already downloaded; apply and exit.
            // systemd restarts the process, so we use Exit instead of Velopack's own restart.
            mgr.ApplyUpdatesAndExit(_updateInfo);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying updates");
            throw;
        }
    }
}
