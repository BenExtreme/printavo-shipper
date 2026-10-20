using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrintavoShipper.App.Services;
using PrintavoShipper.Core.Interfaces;
using PrintavoShipper.Printavo;
using PrintavoShipper.Printavo.Dto;

namespace PrintavoShipper.App.ViewModels;

public partial class OrdersViewModel : ObservableObject
{
    private readonly IPrintavoClient _printavoClient;
    private readonly INavigationService _navigationService;
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string visualIdText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<PrintavoOrderSummary> orders = new();

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string? errorMessage;

    /// <summary>Whether the last search hit the 25-row display cap, so more orders may exist than are shown — surfaced under the grid rather than left implicit.</summary>
    [ObservableProperty]
    private bool resultsTruncated;

    private const int TargetVisibleOrders = 25;

    private CancellationTokenSource? _searchCts;

    public OrdersViewModel(IPrintavoClient printavoClient, INavigationService navigationService, ISettingsService settingsService)
    {
        _printavoClient = printavoClient;
        _navigationService = navigationService;
        _settingsService = settingsService;

        // OrdersViewModel is a session-long Scoped instance reused across
        // every visit to the Orders screen (see App.xaml.cs), and
        // NavigateToCore no-ops on reference-equal re-navigation without
        // firing this event — so the only time it fires for this instance
        // becoming active is a genuine navigation TO Orders from somewhere
        // else (including "back" after shipping something), which is
        // exactly when the list might be stale.
        _navigationService.CurrentViewModelChanged += () =>
        {
            if (ReferenceEquals(_navigationService.CurrentViewModel, this))
            {
                _ = SearchAsync();
            }
        };
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        _searchCts?.Cancel();
        var cts = new CancellationTokenSource();
        _searchCts = cts;

        IsLoading = true;
        ErrorMessage = null;
        try
        {
            // Every delivery method is listed (Pick-up included — staff print
            // box labels for those), so one server page is exactly one
            // visible page; no client-side filtering or backfill paging.
            //
            // Statuses that represent pre-order (quoting) or already-fulfilled/
            // inactive orders (Settings > Printavo Workflow, "Hide") are
            // excluded server-side (via `invoices(excludeStatusIds:)`) so staff
            // only see orders that still need shipping action, and so the
            // 25-row page cap doesn't fill up with orders that would just get
            // hidden anyway.
            var settings = await _settingsService.GetSettingsAsync(cts.Token);
            var hiddenStatusIds = settings.HiddenOrderStatuses.Select(s => s.Id).ToList();
            var query = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText;
            var result = await _printavoClient.SearchOrdersAsync(query, hiddenStatusIds, first: TargetVisibleOrders, ct: cts.Token);

            if (cts.Token.IsCancellationRequested)
            {
                return;
            }

            ResultsTruncated = result.HasNextPage;
            Orders = new ObservableCollection<PrintavoOrderSummary>(result.Orders);
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer search — the newer call owns IsLoading/ErrorMessage now.
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Search failed: {ex.Message}";
        }
        finally
        {
            if (!cts.Token.IsCancellationRequested)
            {
                IsLoading = false;
            }
        }
    }

    [RelayCommand]
    private async Task LookupByVisualIdAsync()
    {
        if (string.IsNullOrWhiteSpace(VisualIdText))
        {
            return;
        }

        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var order = await _printavoClient.GetOrderByVisualIdAsync(VisualIdText.Trim());
            if (order is null)
            {
                ErrorMessage = $"No order found with number \"{VisualIdText}\".";
                return;
            }

            // The lookup already fetched the full order detail — hand it over
            // so the order screen doesn't fetch the same order again.
            OpenOrder(order.Id, order);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Lookup failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void SelectOrder(PrintavoOrderSummary? order)
    {
        if (order is not null)
        {
            OpenOrder(order.Id);
        }
    }

    private void OpenOrder(string orderId, PrintavoOrderDetail? prefetched = null)
    {
        _navigationService.NavigateTo<OrderDetailViewModel>(vm => _ = vm.LoadAsync(orderId, prefetched));
    }
}
