using System.Threading.Tasks;

namespace BrewAlert.Core.Interfaces;

public interface IPreferencesService
{
    Task<string?> GetNotificationProviderAsync();
    Task SaveNotificationProviderAsync(string provider);
}
