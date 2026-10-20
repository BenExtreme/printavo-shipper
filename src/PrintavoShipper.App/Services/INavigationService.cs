using CommunityToolkit.Mvvm.ComponentModel;

namespace PrintavoShipper.App.Services;

public interface INavigationService
{
    object? CurrentViewModel { get; }

    bool CanGoBack { get; }

    event Action? CurrentViewModelChanged;

    void NavigateTo<TViewModel>() where TViewModel : ObservableObject;

    void NavigateTo<TViewModel>(Action<TViewModel> initialize) where TViewModel : ObservableObject;

    void GoBack();
}
