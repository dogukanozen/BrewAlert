namespace BrewAlert.UI.Services;

using BrewAlert.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// DI-backed navigation service. Resolves ViewModels from the container
/// and notifies subscribers (MainWindowViewModel) of view changes.
/// </summary>
public sealed class NavigationService(IServiceProvider serviceProvider) : INavigationService
{
    public ViewModelBase CurrentView { get; private set; } = null!;

    public event Action<ViewModelBase>? CurrentViewChanged;

    public void NavigateTo<T>() where T : ViewModelBase
    {
        var viewModel = serviceProvider.GetRequiredService<T>();
        NavigateTo(viewModel);
    }

    public void NavigateTo(ViewModelBase viewModel)
    {
        CurrentView = viewModel;
        CurrentViewChanged?.Invoke(viewModel);
    }
}
