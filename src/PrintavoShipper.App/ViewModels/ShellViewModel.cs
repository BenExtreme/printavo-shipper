using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrintavoShipper.App.Services;

namespace PrintavoShipper.App.ViewModels;

public partial class ShellViewModel : ObservableObject
{
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private object? currentViewModel;

    [ObservableProperty]
    private bool isBackEnabled;

    public ShellViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;
        _navigationService.CurrentViewModelChanged += () =>
        {
            CurrentViewModel = _navigationService.CurrentViewModel;
            IsBackEnabled = _navigationService.CanGoBack;
        };

        NavigateToOrders();
    }

    [RelayCommand]
    private void GoBack() => _navigationService.GoBack();

    [RelayCommand]
    private void NavigateToOrders() => _navigationService.NavigateTo<OrdersViewModel>();

    [RelayCommand]
    private void NavigateToNewShipment() => _navigationService.NavigateTo<NewShipmentViewModel>();

    [RelayCommand]
    private void NavigateToContractors() => _navigationService.NavigateTo<ContractorsViewModel>();

    [RelayCommand]
    private void NavigateToHistory() => _navigationService.NavigateTo<ShipmentHistoryViewModel>();

    [RelayCommand]
    private void NavigateToSettings() => _navigationService.NavigateTo<SettingsViewModel>();
}
