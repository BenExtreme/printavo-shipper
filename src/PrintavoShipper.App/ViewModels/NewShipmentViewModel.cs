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
/// A shipment not sourced from any particular Printavo order screen: staff
/// free-type a ship-to address, pick UPS or FedEx by hand (there's no
/// delivery-method string to auto-resolve a carrier from here), and
/// optionally link the shipment to an order by number. Linking swaps the
/// Printavo write-back from the normal customerNote-append behavior to a
/// single combined tracking+cost Task line (see
/// ShipmentCardViewModel/FedExShipmentCardViewModel's
/// useCombinedTaskWriteBack constructor parameter and
/// ShippingWorkflowService's branch on ShipmentRecord.UseCombinedTaskWriteBack)
/// — Orders-screen shipments are entirely unaffected. Otherwise this reuses
/// the same shipment-card ViewModels and ship-from/billing/package/rate
/// flow as OrderDetailViewModel, just seeded with a hand-typed address
/// instead of one read off an order.
/// </summary>
public partial class NewShipmentViewModel : ObservableObject
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

    private List<OwnUpsAccount> _ownAccounts = new();
    private List<OwnFedExAccount> _ownFedExAccounts = new();
    private ContractorShipFromAddress _defaultShipFrom = new();
    private Contractor? _linkedContractor;

    [ObservableProperty]
    private bool isInitializing;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private ObservableCollection<object> shipmentCards = new();

    /// <summary>Raised after "Add Shipment" adds a card, so the view can scroll it into view — mirrors OrderDetailViewModel.ShipmentCardFocusRequested.</summary>
    public event Action<object>? ShipmentCardFocusRequested;

    // ---- Carrier ----

    [ObservableProperty]
    private ShipmentCarrier selectedCarrier = ShipmentCarrier.Ups;

    // ---- Link to Printavo order (optional) ----

    [ObservableProperty]
    private string linkedOrderVisualIdText = string.Empty;

    [ObservableProperty]
    private PrintavoOrderDetail? linkedOrder;

    [ObservableProperty]
    private bool isLookingUpOrder;

    [ObservableProperty]
    private string? orderLookupErrorMessage;

    // ---- Free-type ship-to address ----

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddShipmentCommand))]
    private string shipToName = string.Empty;

    [ObservableProperty]
    private string shipToAttentionName = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddShipmentCommand))]
    private string shipToAddress1 = string.Empty;

    [ObservableProperty]
    private string shipToAddress2 = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddShipmentCommand))]
    private string shipToCity = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddShipmentCommand))]
    private string shipToState = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddShipmentCommand))]
    private string shipToPostalCode = string.Empty;

    [ObservableProperty]
    private string shipToCountryCode = "US";

    [ObservableProperty]
    private string shipToPhoneNumber = string.Empty;

    public NewShipmentViewModel(
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
        IDialogService dialogService)
    {
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

        // Refresh own-account/ship-from-default settings every time staff
        // navigate back to this screen (not just once), same pattern as
        // OrdersViewModel — Settings could change mid-session.
        _navigationService.CurrentViewModelChanged += () =>
        {
            if (ReferenceEquals(_navigationService.CurrentViewModel, this))
            {
                _ = InitializeAsync();
            }
        };

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        IsInitializing = true;
        ErrorMessage = null;
        try
        {
            _ownAccounts = await _upsAccountRepository.GetAllAsync();
            _ownFedExAccounts = await _fedExAccountRepository.GetAllAsync();
            var settings = await _settingsService.GetSettingsAsync();

            _defaultShipFrom = new ContractorShipFromAddress
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
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load shipping settings: {ex.Message}";
        }
        finally
        {
            IsInitializing = false;
        }
    }

    [RelayCommand]
    private async Task LookupOrderAsync()
    {
        if (string.IsNullOrWhiteSpace(LinkedOrderVisualIdText))
        {
            return;
        }

        IsLookingUpOrder = true;
        OrderLookupErrorMessage = null;
        try
        {
            var order = await _printavoClient.GetOrderByVisualIdAsync(LinkedOrderVisualIdText.Trim());
            if (order is null)
            {
                OrderLookupErrorMessage = $"No order found with number \"{LinkedOrderVisualIdText}\".";
                LinkedOrder = null;
                return;
            }

            LinkedOrder = order;

            // Opt-in only, same read-only lookup as OrderDetailViewModel.LoadAsync
            // — never creates or revives a Contractor just from looking up an order.
            _linkedContractor = null;
            if (!string.IsNullOrWhiteSpace(order.CustomerId))
            {
                var existing = await _contractorRepository.GetByIdAsync(order.CustomerId);
                _linkedContractor = existing is { IsArchived: false } ? existing : null;
            }

            // Auto-fill, not lock — pre-fills the free-type fields from the
            // order's shipping address so staff don't have to retype the
            // common case, but every field stays editable afterward.
            if (order.ShippingAddress is { } addr)
            {
                var preferCompanyName = !string.IsNullOrWhiteSpace(addr.CompanyName);
                ShipToName = (preferCompanyName ? addr.CompanyName : addr.CustomerName) ?? string.Empty;
                ShipToAttentionName = preferCompanyName ? addr.CustomerName ?? string.Empty : string.Empty;
                ShipToAddress1 = addr.Address1;
                ShipToAddress2 = addr.Address2 ?? string.Empty;
                ShipToCity = addr.City;
                ShipToState = !string.IsNullOrWhiteSpace(addr.StateIso) ? addr.StateIso : addr.State;
                ShipToPostalCode = addr.ZipCode;
                ShipToCountryCode = !string.IsNullOrWhiteSpace(addr.CountryIso) ? addr.CountryIso : "US";
                ShipToPhoneNumber = order.ContactPhone ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            OrderLookupErrorMessage = $"Lookup failed: {ex.Message}";
        }
        finally
        {
            IsLookingUpOrder = false;
        }
    }

    [RelayCommand]
    private void ClearLinkedOrder()
    {
        LinkedOrder = null;
        _linkedContractor = null;
        LinkedOrderVisualIdText = string.Empty;
        OrderLookupErrorMessage = null;
        // Ship-to fields deliberately left as-is — staff may have already
        // edited them, and unlinking shouldn't wipe typed work.
    }

    private bool CanAddShipment() =>
        !string.IsNullOrWhiteSpace(ShipToName)
        && !string.IsNullOrWhiteSpace(ShipToAddress1)
        && !string.IsNullOrWhiteSpace(ShipToCity)
        && !string.IsNullOrWhiteSpace(ShipToState)
        && !string.IsNullOrWhiteSpace(ShipToPostalCode);

    [RelayCommand(CanExecute = nameof(CanAddShipment))]
    private void AddShipment()
    {
        var label = LinkedOrder is not null ? $"Order #{LinkedOrder.VisualId}" : "New shipment";
        var isLinked = LinkedOrder is not null;

        if (SelectedCarrier == ShipmentCarrier.FedEx)
        {
            var shipTo = new FedExPartyInput(
                UpsAddressLimits.Truncate(ShipToName, UpsAddressLimits.NameMaxLength) ?? "Customer",
                UpsAddressLimits.Truncate(ShipToAttentionName, UpsAddressLimits.AttentionNameMaxLength),
                UpsAddressLimits.Truncate(ShipToAddress1, UpsAddressLimits.AddressLineMaxLength) ?? string.Empty,
                UpsAddressLimits.Truncate(ShipToAddress2, UpsAddressLimits.AddressLineMaxLength),
                UpsAddressLimits.Truncate(ShipToCity, UpsAddressLimits.CityMaxLength) ?? string.Empty,
                UpsAddressLimits.Truncate(ShipToState, UpsAddressLimits.StateProvinceCodeMaxLength) ?? string.Empty,
                UpsAddressLimits.Truncate(ShipToPostalCode, UpsAddressLimits.PostalCodeMaxLength) ?? string.Empty,
                UpsAddressLimits.Truncate(ShipToCountryCode, UpsAddressLimits.CountryCodeLength) ?? "US",
                UpsAddressLimits.Truncate(ShipToPhoneNumber, UpsAddressLimits.PhoneNumberMaxLength));

            var card = new FedExShipmentCardViewModel(
                LinkedOrder?.Id ?? string.Empty,
                LinkedOrder?.VisualId ?? string.Empty,
                LinkedOrder?.PoNumber,
                hasMultipleShipToAddresses: false,
                LinkedOrder?.CustomerId ?? string.Empty,
                LinkedOrder?.CustomerCompanyName ?? string.Empty,
                _linkedContractor,
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
                onContractorProvisioned: c => _linkedContractor = c,
                useCombinedTaskWriteBack: isLinked);

            ShipmentCards.Add(card);
            ShipmentCardFocusRequested?.Invoke(card);
        }
        else
        {
            var shipTo = new UpsPartyInput(
                UpsAddressLimits.Truncate(ShipToName, UpsAddressLimits.NameMaxLength) ?? "Customer",
                UpsAddressLimits.Truncate(ShipToAttentionName, UpsAddressLimits.AttentionNameMaxLength),
                UpsAddressLimits.Truncate(ShipToAddress1, UpsAddressLimits.AddressLineMaxLength) ?? string.Empty,
                UpsAddressLimits.Truncate(ShipToAddress2, UpsAddressLimits.AddressLineMaxLength),
                UpsAddressLimits.Truncate(ShipToCity, UpsAddressLimits.CityMaxLength) ?? string.Empty,
                UpsAddressLimits.Truncate(ShipToState, UpsAddressLimits.StateProvinceCodeMaxLength) ?? string.Empty,
                UpsAddressLimits.Truncate(ShipToPostalCode, UpsAddressLimits.PostalCodeMaxLength) ?? string.Empty,
                UpsAddressLimits.Truncate(ShipToCountryCode, UpsAddressLimits.CountryCodeLength) ?? "US",
                UpsAddressLimits.Truncate(ShipToPhoneNumber, UpsAddressLimits.PhoneNumberMaxLength));

            var card = new ShipmentCardViewModel(
                LinkedOrder?.Id ?? string.Empty,
                LinkedOrder?.VisualId ?? string.Empty,
                LinkedOrder?.PoNumber,
                LinkedOrder?.ContactEmail,
                hasMultipleShipToAddresses: false,
                LinkedOrder?.CustomerId ?? string.Empty,
                LinkedOrder?.CustomerCompanyName ?? string.Empty,
                _linkedContractor,
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
                onContractorProvisioned: c => _linkedContractor = c,
                useCombinedTaskWriteBack: isLinked);

            ShipmentCards.Add(card);
            ShipmentCardFocusRequested?.Invoke(card);
        }
    }
}
