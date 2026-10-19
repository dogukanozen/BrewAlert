namespace BrewAlert.UI.Services;

using BrewAlert.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// DI-backed navigation service. Resolves each ViewModel in its own scope,
/// disposes the previous view and its scope when navigating away,
/// and notifies subscribers (MainWindowViewModel) of view changes.
/// The scope matters: transient IDisposable ViewModels resolved from the root provider
/// are tracked by the root until shutdown, so every navigation would otherwise leak one.
/// </summary>
public sealed class NavigationService(IServiceProvider serviceProvider) : INavigationService, IDisposable
{
    private IServiceScope? _currentScope;

    public ViewModelBase CurrentView { get; private set; } = null!;

    public event Action<ViewModelBase>? CurrentViewChanged;

    public void NavigateTo<T>() where T : ViewModelBase
    {
        var scope = serviceProvider.CreateScope();
        Show(scope.ServiceProvider.GetRequiredService<T>(), scope);
    }

    public void NavigateTo(ViewModelBase viewModel) => Show(viewModel, null);

    private void Show(ViewModelBase viewModel, IServiceScope? scope)
    {
        if (!ReferenceEquals(CurrentView, viewModel))
        {
            if (CurrentView is IDisposable disposable)
                disposable.Dispose();
            _currentScope?.Dispose();
            _currentScope = scope;
        }

        CurrentView = viewModel;
        CurrentViewChanged?.Invoke(viewModel);
    }

    public void Dispose() => _currentScope?.Dispose();
}
