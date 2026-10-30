using System.ComponentModel;
namespace BrewAlert.UI.Services;

public interface IAppearanceService : INotifyPropertyChanged
{
    BrewTheme CurrentTheme { get; }
    bool IsPorcelain { get; }
    bool IsSumi { get; }
    bool IsIbre { get; }
    bool IsAizome { get; }
    AppearanceOptions Options { get; }
    void Refresh(bool hasActiveBrews, bool isHome);
    Task SaveAsync(AppearanceOptions options, CancellationToken ct = default);
}
