using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PrintavoShipper.App.Services;
using PrintavoShipper.Core.Interfaces;
using PrintavoShipper.Core.Models;

namespace PrintavoShipper.App.ViewModels;

/// <summary>
/// Phase 5: browse/search every UPS shipment this app has ever created,
/// reprint a label, void a live one, retry a stuck Printavo write-back, or
/// jump back to the source order — without having to find and reopen each
/// order individually.
/// </summary>
public partial class ShipmentHistoryViewModel : ObservableObject
{
    private readonly IShipmentRepository _shipmentRepository;
    private readonly IShipmentArchiveService _shipmentArchiveService;
    private readonly IShippingWorkflowService _workflowService;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private readonly ILogger<ShipmentHistoryViewModel> _logger;

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private bool needsSyncOnly;

    [ObservableProperty]
    private ObservableCollection<ShipmentRecord> shipments = new();

    [ObservableProperty]
    private ICollectionView? shipmentsView;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private string? statusMessage;

    // Full result set for the current SearchText, before the NeedsSyncOnly
    // filter — kept around so toggling NeedsSyncOnly can just re-filter in
    // memory instead of re-querying the DB for data it already has.
    private List<ShipmentRecord> _lastSearchResults = new();

    public ShipmentHistoryViewModel(
        IShipmentRepository shipmentRepository,
        IShipmentArchiveService shipmentArchiveService,
        IShippingWorkflowService workflowService,
        INavigationService navigationService,
        IDialogService dialogService,
        ILogger<ShipmentHistoryViewModel> logger)
    {
        _shipmentRepository = shipmentRepository;
        _shipmentArchiveService = shipmentArchiveService;
        _workflowService = workflowService;
        _navigationService = navigationService;
        _dialogService = dialogService;
        _logger = logger;
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await RunArchiveSweepAsync();
        await SearchAsync();
    }

    /// <summary>
    /// Best-effort maintenance sweep, checked once per History-screen visit
    /// but only actually runs (and exports/deletes anything) once it's due
    /// per AppSettings.ShipmentArchiveIntervalWeeks. Any failure here (bad
    /// settings row, DB locked, etc.) is logged and swallowed — it must
    /// never block the History screen from loading.
    /// </summary>
    private async Task RunArchiveSweepAsync()
    {
        try
        {
            var result = await _shipmentArchiveService.RunScheduledSweepAsync();
            if (result.ArchivedCount > 0)
            {
                _logger.LogInformation("Archived {Count} shipment(s) to {FilePath}.", result.ArchivedCount, result.FilePath);
                StatusMessage = $"Archived {result.ArchivedCount} shipment(s) to {Path.GetFileName(result.FilePath)}.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Shipment archive sweep failed; continuing without archiving.");
        }
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var query = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim();
            _lastSearchResults = await _shipmentRepository.SearchAsync(query);
            ApplyNeedsSyncFilter();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load shipment history: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyNeedsSyncFilter() =>
        Shipments = new ObservableCollection<ShipmentRecord>(
            NeedsSyncOnly ? _lastSearchResults.Where(r => r.NeedsPrintavoWriteBack) : _lastSearchResults);

    partial void OnShipmentsChanged(ObservableCollection<ShipmentRecord> value)
    {
        var view = CollectionViewSource.GetDefaultView(value);
        view.SortDescriptions.Add(new SortDescription(nameof(ShipmentRecord.ShippedDateGroupKey), ListSortDirection.Descending));
        view.SortDescriptions.Add(new SortDescription(nameof(ShipmentRecord.Id), ListSortDirection.Descending));
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ShipmentRecord.ShippedDateGroupKey)));
        ShipmentsView = view;
    }

    partial void OnNeedsSyncOnlyChanged(bool value) => ApplyNeedsSyncFilter();

    [RelayCommand]
    private void OpenOrder(ShipmentRecord? record)
    {
        if (record is null)
        {
            return;
        }

        var orderId = record.PrintavoOrderId;
        _navigationService.NavigateTo<OrderDetailViewModel>(vm => _ = vm.LoadAsync(orderId));
    }

    [RelayCommand]
    private async Task ReprintAsync(ShipmentRecord? record)
    {
        if (record is null)
        {
            return;
        }

        StatusMessage = null;
        ErrorMessage = null;
        try
        {
            await _workflowService.ReprintAsync(record);
            StatusMessage = $"Reprinted order #{record.PrintavoVisualId}.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Reprint failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task VoidAsync(ShipmentRecord? record)
    {
        if (record is null || record.Status != ShipmentStatus.Purchased)
        {
            return;
        }

        if (!await _dialogService.ConfirmAsync("Void Shipment", "Void this shipment? This cannot be undone."))
        {
            return;
        }

        StatusMessage = null;
        ErrorMessage = null;
        try
        {
            var index = Shipments.IndexOf(record);
            var (updated, warning) = await _workflowService.VoidShipmentAsync(record);
            if (index >= 0)
            {
                // Index-set (not a plain property set) always raises
                // CollectionChanged regardless of reference equality, so
                // this refreshes the row even though VoidShipmentAsync
                // mutates and hands back the same ShipmentRecord instance.
                Shipments[index] = updated;
            }

            if (warning is null)
            {
                StatusMessage = $"Voided order #{record.PrintavoVisualId}.";
            }
            else
            {
                ErrorMessage = warning;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Void failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RetryPrintavoSyncAsync(ShipmentRecord? record)
    {
        if (record is null)
        {
            return;
        }

        StatusMessage = null;
        ErrorMessage = null;
        try
        {
            var index = Shipments.IndexOf(record);
            var (updated, warning) = await _workflowService.RetryPrintavoWriteBackAsync(record);
            if (index >= 0)
            {
                Shipments[index] = updated;
            }

            if (warning is null)
            {
                StatusMessage = $"Printavo sync complete for order #{record.PrintavoVisualId}.";
            }
            else
            {
                ErrorMessage = warning;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Retry failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void CopyMasterTrackingNumber(ShipmentRecord? record)
    {
        if (record?.MasterTrackingNumber is not { } tracking)
        {
            return;
        }

        CopyToClipboard(tracking, $"Copied master tracking number for order #{record.PrintavoVisualId}.");
    }

    [RelayCommand]
    private void CopyAllTrackingNumbers(ShipmentRecord? record)
    {
        if (record is null || !record.HasTrackingNumber)
        {
            return;
        }

        var count = record.TrackingNumbers.Count;
        CopyToClipboard(
            string.Join(",", record.TrackingNumbers),
            $"Copied {count} tracking number{(count == 1 ? "" : "s")} for order #{record.PrintavoVisualId}.");
    }

    [RelayCommand]
    private void CopyTrackingNumber(string? tracking)
    {
        if (string.IsNullOrWhiteSpace(tracking))
        {
            return;
        }

        CopyToClipboard(tracking, $"Copied tracking number {tracking}.");
    }

    private void CopyToClipboard(string text, string successMessage)
    {
        StatusMessage = null;
        ErrorMessage = null;
        try
        {
            Clipboard.SetText(text);
            StatusMessage = successMessage;
        }
        catch (COMException ex)
        {
            // Another app holding the clipboard open (CLIPBRD_E_CANT_OPEN) is
            // the realistic failure here — transient, so just say so.
            ErrorMessage = $"Couldn't copy to the clipboard (another app may be using it): {ex.Message}";
        }
    }
}
