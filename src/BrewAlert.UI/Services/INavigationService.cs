namespace BrewAlert.UI.Services;

using BrewAlert.UI.ViewModels;

/// <summary>
/// Abstracts view navigation so ViewModels never touch the DI container directly.
/// </summary>
public interface INavigationService
{
    /// <summary>The currently displayed ViewModel.</summary>
    ViewModelBase CurrentView { get; }

    /// <summary>Navigate to a new instance of the specified ViewModel type.</summary>
    void NavigateTo<T>() where T : ViewModelBase;

    /// <summary>Navigate to an existing ViewModel instance.</summary>
    void NavigateTo(ViewModelBase viewModel);

    /// <summary>Raised when the current view changes.</summary>
    event Action<ViewModelBase>? CurrentViewChanged;
}
