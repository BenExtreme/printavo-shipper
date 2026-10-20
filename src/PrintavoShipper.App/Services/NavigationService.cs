using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace PrintavoShipper.App.Services;

/// <summary>
/// Resolves the requested ViewModel from the DI container (each nav target
/// is registered Scoped/Transient in App.xaml.cs) and swaps it into the
/// shell's ContentControl, which selects the matching View via a
/// DataTemplate keyed on the ViewModel's type (see App.xaml).
/// </summary>
public class NavigationService : INavigationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Stack<object> _backStack = new();

    public NavigationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public object? CurrentViewModel { get; private set; }

    public bool CanGoBack => _backStack.Count > 0;

    public event Action? CurrentViewModelChanged;

    public void NavigateTo<TViewModel>() where TViewModel : ObservableObject =>
        NavigateToCore(_serviceProvider.GetRequiredService<TViewModel>());

    public void NavigateTo<TViewModel>(Action<TViewModel> initialize) where TViewModel : ObservableObject
    {
        var viewModel = _serviceProvider.GetRequiredService<TViewModel>();
        initialize(viewModel);
        NavigateToCore(viewModel);
    }

    public void GoBack()
    {
        if (_backStack.Count == 0)
        {
            return;
        }

        CurrentViewModel = _backStack.Pop();
        CurrentViewModelChanged?.Invoke();
    }

    private void NavigateToCore(object viewModel)
    {
        // Reference-equal re-navigation (e.g. re-clicking the already-active
        // pane item, which resolves the same Scoped-as-singleton instance)
        // is a no-op — pushing it would create a redundant back-stack entry
        // that just re-lands on the same screen.
        if (ReferenceEquals(CurrentViewModel, viewModel))
        {
            return;
        }

        if (CurrentViewModel is not null)
        {
            _backStack.Push(CurrentViewModel);
        }

        CurrentViewModel = viewModel;
        CurrentViewModelChanged?.Invoke();
    }
}
