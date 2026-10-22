using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrintavoShipper.App.Services;
using PrintavoShipper.Core.Interfaces;
using PrintavoShipper.Core.Models;
using PrintavoShipper.FedEx;
using PrintavoShipper.Printavo;
using PrintavoShipper.Printavo.Dto;
using PrintavoShipper.Ups;

namespace PrintavoShipper.App.ViewModels;

/// <summary>
/// Order detail: production/customer notes, contractor, the ship-to
/// address(es) available on the order, and the shipment-builder cards
/// (ship-from/billing/service/package selection, "Ship &amp; Print Label")
/// spun up per address staff choose to ship. Printavo write-back (Task
/// creation, customerNote append) is Phase 4 — not wired in yet.
/// </summary>
public partial class OrderDetailViewModel : ObservableObject
{
    private readonly IPrintavoClient _printavoClient;
    private readonly IContractorRepository _contractorRepository;
    private readonly IContractorProvisioningService _contractorProvisioningService;
    private readonly INavigationService _navigationService;
    private readonly IShippingWorkflowService _shippingWorkflowService;
    private readonly IUpsClient _upsClient;
    private readonly IUpsAccountRepository _upsAccountRepository;
    private readonly IFedExClient _fedExClient;
    private readonly IFedExAccountRepository _fedExAccountRepository;
    private readonly ISettingsService _settingsService;
    private readonly IDialogService _dialogService;
    private readonly IBoxLabelService _boxLabelService;
    private readonly IShipmentRepository _shipmentRepository;
    private List<Core.Models.OwnUpsAccount> _ownAccounts = new();
    private List<Core.Models.OwnFedExAccount> _ownFedExAccounts = new();
    private Core.Models.ContractorShipFromAddress _defaultShipFrom = new();
    private ShipmentCarrier _carrier = ShipmentCarrier.Ups;

    /// <summary>The order owner's shop email, preselected as each UPS card's Quantum View "also notify on delivery" contact. None until the owner lookup finishes (or if it fails).</summary>
    private StaffContact _ownerContact = StaffContacts.None;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private PrintavoOrderDetail? order;

    [ObservableProperty]
    private Core.Models.Contractor? contractor;

    [ObservableProperty]
    private bool showContractorSetupBanner;

    [ObservableProperty]
    private ObservableCollection<PrintavoCustomAddress> customAddresses = new();

    [ObservableProperty]
    private ObservableCollection<object> shipmentCards = new();

    /// <summary>True when this order's Printavo delivery method isn't one of the names configured in Settings > Printavo Workflow — the order still ships via the default carrier, but staff should notice and double-check the Printavo order.</summary>
    [ObservableProperty]
    private bool showUnrecognizedDeliveryMethodWarning;

    /// <summary>"UPS"/"FedEx" — what an unrecognized delivery method falls back to, for the warning text.</summary>
    [ObservableProperty]
    private string defaultCarrierName = "UPS";

    [ObservableProperty]
    private string? deliveryMethodName;

    /// <summary>The shop's own name from Settings (default ship-from company), for "shipping from ... by default" text.</summary>
    [ObservableProperty]
    private string shopName = string.Empty;

    /// <summary>The shop's default ship-from address in Settings is missing required fields — direct-customer shipments would fail at the carrier.</summary>
    [ObservableProperty]
    private bool showShipFromNotConfiguredWarning;

    /// <summary>Pick-up orders don't ship — the order screen shows box labels instead of ship-to addresses/shipment cards, and the Pick-up statuses picked in Settings instead of the shipping ones.</summary>
    [ObservableProperty]
    private bool isPickup;

    [ObservableProperty]
    private IReadOnlyList<PrintavoOrderStatusOption> availableStatuses = Array.Empty<PrintavoOrderStatusOption>();

    [ObservableProperty]
    private PrintavoOrderStatusOption? selectedStatus;

    [ObservableProperty]
    private bool isUpdatingStatus;

    [ObservableProperty]
    private string? statusUpdateMessage;

    // ---- Add Task (e.g. telling the order's CSR about an overage/shortage) ----

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddTaskCommand))]
    private string taskDetails = string.Empty;

    [ObservableProperty]
    private ObservableCollection<PrintavoUser> taskAssigneeOptions = new();

    [ObservableProperty]
    private PrintavoUser? selectedTaskAssignee;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddTaskCommand))]
    private bool isAddingTask;

    [ObservableProperty]
    private string? taskStatusMessage;

    // ---- Box labels (Pick-up orders) ----

    private const int MaxBoxCount = 99;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PrintBoxLabelsCommand))]
    private int boxCount = 1;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PrintBoxLabelsCommand))]
    private bool isPrintingBoxLabels;

    [ObservableProperty]
    private string? boxLabelMessage;

    /// <summary>Whose logo the box labels will carry, so staff know before printing.</summary>
    [ObservableProperty]
    private string? boxLabelLogoSource;

    public OrderDetailViewModel(
        IPrintavoClient printavoClient,
        IContractorRepository contractorRepository,
        IContractorProvisioningService contractorProvisioningService,
        INavigationService navigationService,
        IShippingWorkflowService shippingWorkflowService,
        IUpsClient upsClient,
        IUpsAccountRepository upsAccountRepository,
        IFedExClient fedExClient,
        IFedExAccountRepository fedExAccountRepository,
        ISettingsService settingsService,
        IDialogService dialogService,
        IBoxLabelService boxLabelService,
        IShipmentRepository shipmentRepository)
    {
        _boxLabelService = boxLabelService;
        _shipmentRepository = shipmentRepository;
        _printavoClient = printavoClient;
        _contractorRepository = contractorRepository;
        _contractorProvisioningService = contractorProvisioningService;
        _navigationService = navigationService;
        _shippingWorkflowService = shippingWorkflowService;
        _upsClient = upsClient;
        _upsAccountRepository = upsAccountRepository;
        _fedExClient = fedExClient;
        _fedExAccountRepository = fedExAccountRepository;
        _settingsService = settingsService;
        _dialogService = dialogService;
    }

    /// <param name="prefetched">The order's full detail if the caller already has it (e.g. the Orders screen's order-number lookup fetches exactly this), so it isn't fetched a second time.</param>
    public async Task LoadAsync(string orderId, PrintavoOrderDetail? prefetched = null)
    {
        IsLoading = true;
        ErrorMessage = null;

        // Fire-and-forget with its own error handling — the assignee list
        // failing to load must never block the order itself. Started first
        // since it only needs the id, so it's in flight alongside the order.
        _ = LoadTaskAssigneesAsync(orderId);

        try
        {
            // Start the network fetch first and do the local reads while it's
            // in flight, rather than one after the other.
            var orderTask = prefetched is not null ? Task.FromResult<PrintavoOrderDetail?>(prefetched) : _printavoClient.GetOrderByIdAsync(orderId);
            _ownAccounts = await _upsAccountRepository.GetAllAsync();
            _ownFedExAccounts = await _fedExAccountRepository.GetAllAsync();
            var settings = await _settingsService.GetSettingsAsync();
            var pastShipments = await _shipmentRepository.GetByOrderIdAsync(orderId);

            Order = await orderTask;
            if (Order is null)
            {
                ErrorMessage = "Order not found.";
                return;
            }

            var delivery = CarrierResolver.Resolve(Order.DeliveryMethodName, settings);
            _carrier = delivery.Carrier;
            DeliveryMethodName = Order.DeliveryMethodName;
            ShowUnrecognizedDeliveryMethodWarning = delivery.WasUnrecognized;
            DefaultCarrierName = settings.DefaultCarrier == ShipmentCarrier.FedEx ? "FedEx" : "UPS";
            IsPickup = delivery.IsPickup;
            ShopName = string.IsNullOrWhiteSpace(settings.DefaultShipFromCompanyName) ? "the shop's default address" : settings.DefaultShipFromCompanyName;
            ShowShipFromNotConfiguredWarning = !IsPickup && !settings.IsDefaultShipFromConfigured;
            BoxLabelLogoSource = !string.IsNullOrWhiteSpace(Order.ContractorLogoUrl)
                ? $"{Order.ContractorProfileName ?? "Contractor"} logo (from the order's Printavo contractor profile)"
                : $"{settings.DefaultShipFromCompanyName} logo (no contractor profile on this order)";

            CustomAddresses = new ObservableCollection<PrintavoCustomAddress>(Order.CustomAddresses);
            ShipmentCards = new ObservableCollection<object>();

            _defaultShipFrom = new Core.Models.ContractorShipFromAddress
            {
                Label = string.IsNullOrWhiteSpace(settings.DefaultShipFromCompanyName) ? "Shop default" : settings.DefaultShipFromCompanyName,
                CompanyName = settings.DefaultShipFromCompanyName,
                AttentionName = settings.DefaultShipFromAttentionName,
                Address1 = settings.DefaultShipFromAddress1,
                Address2 = settings.DefaultShipFromAddress2,
                City = settings.DefaultShipFromCity,
                State = settings.DefaultShipFromState,
                PostalCode = settings.DefaultShipFromPostalCode,
                CountryCode = settings.DefaultShipFromCountryCode,
                PhoneNumber = settings.DefaultShipFromPhoneNumber,
                IsDefault = true
            };

            // Read-only lookup — opening an order never creates or revives
            // a Contractor. A customer that was never explicitly imported
            // via Contractors > "Import from Printavo", or whose contractor
            // profile is archived, is treated as a direct B2C customer:
            // Contractor stays null and ShipmentCardViewModel falls back to
            // the shop's own default ship-from address.
            Contractor = null;
            if (!string.IsNullOrWhiteSpace(Order.CustomerId))
            {
                var existing = await _contractorRepository.GetByIdAsync(Order.CustomerId);
                Contractor = existing is { IsArchived: false } ? existing : null;
            }

            // Shipments already made for this order come back as finished
            // cards (Reprint/Void/Retry still work), oldest first so new
            // cards keep appending in chronological order below them. Only
            // ones that actually produced a label — Draft/Error rows have
            // nothing to show or act on.
            foreach (var record in pastShipments
                         .Where(r => r.Status is ShipmentStatus.Purchased or ShipmentStatus.Voided)
                         .OrderBy(r => r.Id))
            {
                RestoreShipmentCard(record);
            }

            // Preselect the order's current status so "Update Status" starts
            // from where the order actually is, instead of an empty combo
            // box staff had to cross-reference against the header text and
            // pick by hand every time. The list must be swapped first, or
            // the preselection is looked up against the wrong statuses.
            AvailableStatuses = (IsPickup ? settings.PickupStatuses : settings.ShippingStatuses)
                .Select(s => new PrintavoOrderStatusOption(s.Id, s.Name))
                .ToList();
            SelectedStatus = AvailableStatuses.FirstOrDefault(s => s.Id == Order.StatusId);
            AddTaskCommand.NotifyCanExecuteChanged();
            PrintBoxLabelsCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load order: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnContractorChanged(Core.Models.Contractor? value) =>
        // Only nudge staff to set up a contractor that's actually been
        // imported as one and just lacks a ship-from address — a direct
        // (never-imported) customer isn't missing anything, it's B2C by
        // design and already has a working default.
        ShowContractorSetupBanner = value is not null && !value.IsConfigured;

    [RelayCommand]
    private void ShipDefaultAddress()
    {
        if (Order?.ShippingAddress is null)
        {
            return;
        }

        const string label = "Default (from Printavo order)";
        if (_carrier == ShipmentCarrier.FedEx)
        {
            AddFedExShipmentCard(label, FedExPartyMapper.FromPrintavoAddress(Order.ShippingAddress, Order.ContactPhone));
        }
        else
        {
            AddUpsShipmentCard(label, UpsPartyMapper.FromPrintavoAddress(Order.ShippingAddress, Order.ContactPhone));
        }
    }

    [RelayCommand]
    private void ShipCustomAddress(PrintavoCustomAddress address)
    {
        if (Order is null)
        {
            return;
        }

        var label = address.Name ?? "Additional address";
        if (_carrier == ShipmentCarrier.FedEx)
        {
            AddFedExShipmentCard(label, FedExPartyMapper.FromCustomAddress(address, Order.ContactPhone));
        }
        else
        {
            AddUpsShipmentCard(label, UpsPartyMapper.FromCustomAddress(address, Order.ContactPhone));
        }
    }

    /// <summary>The unshipped shipment card for this label, if any — used to block a double-click (or forgetful second click) from stacking two identical unshipped forms for the same address. A second real shipment for an already-shipped address is legitimate (e.g. a reshipment), so this only guards unshipped duplicates.</summary>
    private object? FindUnshippedCardForLabel(string label) => ShipmentCards.FirstOrDefault(c => c switch
    {
        ShipmentCardViewModel ups => ups.ShipToLabel == label && ups.Result is null,
        FedExShipmentCardViewModel fedEx => fedEx.ShipToLabel == label && fedEx.Result is null,
        _ => false
    });

    /// <summary>Raised with a shipment card after "Ship this address" adds it (or re-clicks an address that already has an unshipped card), so the view can scroll it into view.</summary>
    public event Action<object>? ShipmentCardFocusRequested;

    /// <summary>True (after scrolling to the existing card) if this label already has an unshipped card.</summary>
    private bool FocusExistingUnshippedCard(string label)
    {
        var existing = FindUnshippedCardForLabel(label);
        if (existing is not null)
        {
            ShipmentCardFocusRequested?.Invoke(existing);
        }

        return existing is not null;
    }

    /// <summary>Rebuilds a finished shipment card from its saved record, using the record's own ship-to snapshot rather than the order's current address (which may have been edited since).</summary>
    private void RestoreShipmentCard(ShipmentRecord record)
    {
        var label = record.ShipToLabel ?? "Shipment";
        if (record.Carrier == ShipmentCarrier.FedEx)
        {
            var shipTo = new FedExPartyInput(record.ShipToCompanyName ?? string.Empty, record.ShipToAttentionName, record.ShipToAddress1,
                record.ShipToAddress2, record.ShipToCity, record.ShipToState, record.ShipToPostalCode, record.ShipToCountryCode, null);
            ShipmentCards.Add(CreateFedExShipmentCard(label, shipTo, record));
        }
        else
        {
            var shipTo = new UpsPartyInput(record.ShipToCompanyName ?? string.Empty, record.ShipToAttentionName, record.ShipToAddress1,
                record.ShipToAddress2, record.ShipToCity, record.ShipToState, record.ShipToPostalCode, record.ShipToCountryCode, null);
            ShipmentCards.Add(CreateUpsShipmentCard(label, shipTo, record));
        }
    }

    /// <summary>
    /// Re-reads the customer note after a shipment's Printavo write-back so
    /// the tracking bullet it just added shows up here without reopening the
    /// order. Best-effort: on failure the note just stays as it was. Checks
    /// the order is still the one on screen, since this lands after an await.
    /// </summary>
    private async Task RefreshCustomerNoteAsync()
    {
        if (Order is null)
        {
            return;
        }

        var orderId = Order.Id;
        try
        {
            var note = await _printavoClient.GetCustomerNoteAsync(orderId);
            if (Order?.Id == orderId)
            {
                Order = Order with { CustomerNoteHtml = note };
            }
        }
        catch
        {
            // Leave the note as loaded.
        }
    }

    private void AddUpsShipmentCard(string label, UpsPartyInput shipTo)
    {
        if (Order is null || FocusExistingUnshippedCard(label))
        {
            return;
        }

        var card = CreateUpsShipmentCard(label, shipTo, existingShipment: null);
        ShipmentCards.Add(card);
        ShipmentCardFocusRequested?.Invoke(card);
    }

    private ShipmentCardViewModel CreateUpsShipmentCard(string label, UpsPartyInput shipTo, ShipmentRecord? existingShipment)
    {
        var card = new ShipmentCardViewModel(
            Order!.Id,
            Order.VisualId,
            Order.PoNumber,
            Order.ContactEmail,
            Order.CustomAddresses.Count > 0,
            Order.CustomerId,
            Order.CustomerCompanyName,
            Contractor,
            label,
            shipTo,
            _defaultShipFrom,
            _ownAccounts,
            _shippingWorkflowService,
            _upsClient,
            _contractorRepository,
            _contractorProvisioningService,
            _dialogService,
            onRemove: c => ShipmentCards.Remove(c),
            onContractorProvisioned: c => Contractor = c,
            existingShipment: existingShipment,
            onPrintavoWriteBack: () => _ = RefreshCustomerNoteAsync());
        card.ApplyDefaultSecondaryContact(_ownerContact);
        return card;
    }

    private void AddFedExShipmentCard(string label, FedExPartyInput shipTo)
    {
        if (Order is null || FocusExistingUnshippedCard(label))
        {
            return;
        }

        var card = CreateFedExShipmentCard(label, shipTo, existingShipment: null);
        ShipmentCards.Add(card);
        ShipmentCardFocusRequested?.Invoke(card);
    }

    private FedExShipmentCardViewModel CreateFedExShipmentCard(string label, FedExPartyInput shipTo, ShipmentRecord? existingShipment) =>
        new(
            Order!.Id,
            Order.VisualId,
            Order.PoNumber,
            Order.CustomAddresses.Count > 0,
            Order.CustomerId,
            Order.CustomerCompanyName,
            Contractor,
            label,
            shipTo,
            _defaultShipFrom,
            _ownFedExAccounts,
            _shippingWorkflowService,
            _fedExClient,
            _contractorRepository,
            _contractorProvisioningService,
            _dialogService,
            onRemove: c => ShipmentCards.Remove(c),
            onContractorProvisioned: c => Contractor = c,
            existingShipment: existingShipment,
            onPrintavoWriteBack: () => _ = RefreshCustomerNoteAsync());

    [RelayCommand]
    private void ConfigureContractor()
    {
        if (Contractor is null)
        {
            return;
        }

        var contractorId = Contractor.Id;
        _navigationService.NavigateTo<ContractorsViewModel>(vm => vm.SelectContractorById(contractorId));
    }

    private bool CanUpdateStatus() => !IsUpdatingStatus && Order is not null && SelectedStatus is not null;

    [RelayCommand(CanExecute = nameof(CanUpdateStatus))]
    private async Task UpdateStatusAsync()
    {
        if (Order is null || SelectedStatus is null)
        {
            return;
        }

        IsUpdatingStatus = true;
        StatusUpdateMessage = null;
        try
        {
            var newStatusName = await _printavoClient.UpdateStatusAsync(Order.Id, SelectedStatus.Id);
            Order = Order with { StatusId = SelectedStatus.Id, StatusName = newStatusName };
            StatusUpdateMessage = $"Status updated to \"{newStatusName}\".";
        }
        catch (Exception ex)
        {
            StatusUpdateMessage = $"Couldn't update status: {ex.Message}";
        }
        finally
        {
            IsUpdatingStatus = false;
            UpdateStatusCommand.NotifyCanExecuteChanged();
        }
    }

    partial void OnSelectedStatusChanged(PrintavoOrderStatusOption? value) => UpdateStatusCommand.NotifyCanExecuteChanged();

    private bool CanPrintBoxLabels() => !IsPrintingBoxLabels && Order is not null && BoxCount is >= 1 and <= MaxBoxCount;

    [RelayCommand(CanExecute = nameof(CanPrintBoxLabels))]
    private async Task PrintBoxLabelsAsync()
    {
        if (Order is null)
        {
            return;
        }

        var count = BoxCount;
        IsPrintingBoxLabels = true;
        BoxLabelMessage = null;
        try
        {
            var warning = await _boxLabelService.PrintBoxLabelsAsync(Order, count);
            var printed = count == 1 ? "Printed 1 box label." : $"Printed {count} box labels (Box 1 of {count} – Box {count} of {count}).";
            BoxLabelMessage = warning is null ? printed : $"{printed} {warning}";
        }
        catch (Exception ex)
        {
            BoxLabelMessage = $"Couldn't print box labels: {ex.Message}";
        }
        finally
        {
            IsPrintingBoxLabels = false;
        }
    }

    /// <summary>
    /// Fills the "Assign to" picker with the staff picked in Settings >
    /// Printavo Workflow (by id, so a renamed Printavo user still matches —
    /// shown under their current Printavo name) and preselects the order's
    /// owner if it's one of them. The owner also becomes the UPS cards'
    /// default Quantum View "also notify on delivery" contact.
    /// </summary>
    private async Task LoadTaskAssigneesAsync(string orderId)
    {
        _ownerContact = StaffContacts.None;

        // Two separate requests (see GetOrderOwnerIdAsync for why the owner
        // isn't folded into another query), sent at the same time.
        var usersTask = _printavoClient.ListUsersAsync();
        var ownerIdTask = TryGetOrderOwnerIdAsync(orderId);

        IReadOnlyList<PrintavoUser> users;
        try
        {
            var settings = await _settingsService.GetSettingsAsync();
            users = await usersTask;
            TaskAssigneeOptions = new ObservableCollection<PrintavoUser>(
                settings.StaffMembers.Select(s => users.FirstOrDefault(u => u.Id == s.PrintavoUserId)).OfType<PrintavoUser>());
        }
        catch (Exception ex)
        {
            TaskStatusMessage = $"Couldn't load Printavo users: {ex.Message}";
            return;
        }

        var ownerId = await ownerIdTask;
        SelectedTaskAssignee = TaskAssigneeOptions.FirstOrDefault(u => u.Id == ownerId);

        _ownerContact = StaffContacts.ForOrderOwner(ownerId);
        // Cards opened before the lookup finished (e.g. a quick "Ship this
        // address" click) pick it up now; later cards get it at creation.
        foreach (var card in ShipmentCards.OfType<ShipmentCardViewModel>())
        {
            card.ApplyDefaultSecondaryContact(_ownerContact);
        }
    }

    /// <summary>Null if the lookup fails — just no default assignee; staff can still pick one.</summary>
    private async Task<string?> TryGetOrderOwnerIdAsync(string orderId)
    {
        try
        {
            return await _printavoClient.GetOrderOwnerIdAsync(orderId);
        }
        catch
        {
            return null;
        }
    }

    private bool CanAddTask() => !IsAddingTask && Order is not null && !string.IsNullOrWhiteSpace(TaskDetails);

    [RelayCommand(CanExecute = nameof(CanAddTask))]
    private async Task AddTaskAsync()
    {
        if (Order is null)
        {
            return;
        }

        var name = TaskDetails.Trim();

        IsAddingTask = true;
        TaskStatusMessage = null;
        try
        {
            // Always due the day it's created — same as the shipping-cost task.
            await _printavoClient.CreateTaskAsync(name, DateTimeOffset.Now, SelectedTaskAssignee?.Id, Order.Id);
            TaskStatusMessage = SelectedTaskAssignee is null
                ? $"Task added: \"{name}\"."
                : $"Task added for {SelectedTaskAssignee.Name}: \"{name}\".";
            TaskDetails = string.Empty;
        }
        catch (Exception ex)
        {
            TaskStatusMessage = $"Couldn't add task: {ex.Message}";
        }
        finally
        {
            IsAddingTask = false;
        }
    }
}
