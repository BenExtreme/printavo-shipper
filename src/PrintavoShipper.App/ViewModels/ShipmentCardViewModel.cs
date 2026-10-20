using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrintavoShipper.App.Services;
using PrintavoShipper.Core.Interfaces;
using PrintavoShipper.Core.Models;
using PrintavoShipper.Ups;

namespace PrintavoShipper.App.ViewModels;

/// <summary>
/// One "shipment card": ship-from/billing/service/package selection for a
/// single ship-to address on the order, plus its own Ship/Void/Remove
/// actions and result area. OrderDetailViewModel hosts a collection of
/// these — one per address staff choose to ship separately.
/// </summary>
public partial class ShipmentCardViewModel : ObservableObject
{
    private readonly string _printavoOrderId;
    private readonly string _printavoVisualId;
    private readonly string? _poNumber;
    private readonly bool _hasMultipleShipToAddresses;
    private readonly string _customerId;
    private readonly string _customerCompanyName;
    private Contractor? _contractor;
    private UpsPartyInput _shipTo;

    /// <summary>
    /// The shop's Default Ship-From address. Get Rates always quotes from
    /// here, regardless of SelectedShipFrom: boxes physically leave the shop
    /// even when the label's return address is a contractor's, so that quote
    /// is the cost recorded for the shipment (History, Printavo task).
    /// </summary>
    private readonly UpsPartyInput _rateOrigin;

    /// <summary>CurrentRateKey() as of the last Get Rates — RateQuotes only count as current while it still matches.</summary>
    private string? _ratedKey;

    private readonly IShippingWorkflowService _workflowService;
    private readonly IUpsClient _upsClient;
    private readonly IContractorRepository _contractorRepository;
    private readonly IContractorProvisioningService _contractorProvisioningService;
    private readonly IDialogService _dialogService;
    private readonly Action<ShipmentCardViewModel> _onRemove;
    private readonly Action<Contractor>? _onContractorProvisioned;
    private readonly bool _useCombinedTaskWriteBack;
    private readonly Action? _onPrintavoWriteBack;

    /// <summary>Collapsed cards show just the header plus a one-line shipment summary — toggled by the chevron next to the ship-to label.</summary>
    [ObservableProperty]
    private bool isExpanded = true;

    [ObservableProperty]
    private string shipToLabel = string.Empty;

    [ObservableProperty]
    private string shipToSummary = string.Empty;

    [ObservableProperty]
    private ObservableCollection<ContractorShipFromAddress> shipFromOptions = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShipDisabledReason))]
    [NotifyCanExecuteChangedFor(nameof(ShipCommand))]
    private ContractorShipFromAddress? selectedShipFrom;

    [ObservableProperty]
    private ObservableCollection<OwnUpsAccount> ownAccountOptions = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShipDisabledReason))]
    [NotifyPropertyChangedFor(nameof(ShippingFeePreview))]
    [NotifyCanExecuteChangedFor(nameof(ShipCommand))]
    private OwnUpsAccount? selectedOwnAccount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShipDisabledReason))]
    [NotifyPropertyChangedFor(nameof(CanAddShippingToInvoice))]
    [NotifyPropertyChangedFor(nameof(ShippingFeePreview))]
    [NotifyCanExecuteChangedFor(nameof(ShipCommand))]
    private bool billThirdParty;

    /// <summary>Sentinel item appended to BillingAccountPickerOptions so "enter a different account" is a normal dropdown choice rather than a separate checkbox alongside the picker — selecting it (see OnSelectedBillingAccountChanged) reveals the ad hoc entry fields.</summary>
    private static readonly ContractorBillingAccount NewBillingAccountSentinel = new() { Id = -1, Label = "+ Enter a different account" };

    [ObservableProperty]
    private ObservableCollection<ContractorBillingAccount> billingAccountOptions = new();

    [ObservableProperty]
    private ObservableCollection<ContractorBillingAccount> billingAccountPickerOptions = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShipDisabledReason))]
    [NotifyCanExecuteChangedFor(nameof(ShipCommand))]
    private ContractorBillingAccount? selectedBillingAccount;

    [ObservableProperty]
    private bool useNewBillingAccount;

    [ObservableProperty]
    private string newBillingLabel = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShipDisabledReason))]
    [NotifyCanExecuteChangedFor(nameof(ShipCommand))]
    private string newBillingAccountNumber = string.Empty;

    [ObservableProperty]
    private string newBillingPostalCode = string.Empty;

    [ObservableProperty]
    private string newBillingCountryCode = "US";

    [ObservableProperty]
    private bool newBillingIsDefault;

    [ObservableProperty]
    private string newBillingNotes = string.Empty;

    [ObservableProperty]
    private bool saveBillingAccountToProfile;

    [ObservableProperty]
    private string? billingAccountValidationStatus;

    [ObservableProperty]
    private bool billingAccountValidationHasWarning;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ValidateBillingAccountCommand))]
    private bool isValidatingBillingAccount;

    /// <summary>Display name for the "Save this account to ___'s profile" checkbox — the contractor's name once one exists, otherwise the order's own customer name.</summary>
    public string BillingProfileDisplayName => _contractor?.CompanyName ?? _customerCompanyName;

    public bool HasSavedBillingAccounts => BillingAccountOptions.Count > 0;

    public bool ShowNewBillingAccountFields => !HasSavedBillingAccounts || UseNewBillingAccount;

    [ObservableProperty]
    private ObservableCollection<ServiceLevelOption> serviceLevels = new(
        UpsServiceLevels.All.Select(s => new ServiceLevelOption(s.Code, s.Name)));

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShipDisabledReason))]
    [NotifyPropertyChangedFor(nameof(ShippingFeePreview))]
    [NotifyCanExecuteChangedFor(nameof(ShipCommand))]
    private ServiceLevelOption selectedServiceLevel = new("03", "UPS Ground");

    [ObservableProperty]
    private ObservableCollection<PackageRowViewModel> packages = new() { new PackageRowViewModel() };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShipDisabledReason))]
    [NotifyPropertyChangedFor(nameof(ShippingFeePreview))]
    [NotifyCanExecuteChangedFor(nameof(ShipCommand))]
    private ObservableCollection<UpsRateQuote> rateQuotes = new();

    /// <summary>Caption next to Get Rates, so staff know quotes aren't from the selected ship-from.</summary>
    public string RateOriginSummary { get; }

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private string? printWarning;

    [ObservableProperty]
    private string? printavoWriteBackWarning;

    [ObservableProperty]
    private ShipmentRecord? result;

    [ObservableProperty]
    private string addressValidationStatus = "Validating address...";

    [ObservableProperty]
    private bool addressValidationHasWarning;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ValidateAddressCommand))]
    private bool isValidatingAddress;

    /// <summary>UPS's suggested corrections when it couldn't match the ship-to exactly — each one can be applied with one click (UseSuggestedAddressCommand).</summary>
    [ObservableProperty]
    private ObservableCollection<UpsAddressCandidate> addressSuggestions = new();

    [ObservableProperty]
    private bool canEditShipTo = true;

    [ObservableProperty]
    private bool isEditingShipTo;

    [ObservableProperty]
    private string editShipToName = string.Empty;

    [ObservableProperty]
    private string? editShipToAttentionName;

    [ObservableProperty]
    private string editShipToAddress1 = string.Empty;

    [ObservableProperty]
    private string? editShipToAddress2;

    [ObservableProperty]
    private string editShipToCity = string.Empty;

    [ObservableProperty]
    private string editShipToState = string.Empty;

    [ObservableProperty]
    private string editShipToPostalCode = string.Empty;

    [ObservableProperty]
    private string editShipToCountryCode = "US";

    [ObservableProperty]
    private string? editShipToPhoneNumber;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShipDisabledReason))]
    [NotifyCanExecuteChangedFor(nameof(ShipCommand))]
    private bool enableQuantumView;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShipDisabledReason))]
    [NotifyCanExecuteChangedFor(nameof(ShipCommand))]
    private string quantumViewPrimaryEmail = string.Empty;

    [ObservableProperty]
    private string? quantumViewSecondaryEmail;

    [ObservableProperty]
    private StaffContact selectedSecondaryContact = StaffContacts.None;

    public IReadOnlyList<StaffContact> StaffContactOptions { get; } = StaffContacts.All;

    [ObservableProperty]
    private string? notes;

    /// <summary>"Add Shipping Cost To Invoice": on Ship, also add a "Shipping" fee to the order's Printavo invoice, priced by ShippingFeeCalculator from the shop-origin Get Rates quote.</summary>
    [ObservableProperty]
    private bool addShippingToInvoice;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UseNegotiatedFeeRate))]
    [NotifyPropertyChangedFor(nameof(UsePublishedFeeRate))]
    [NotifyPropertyChangedFor(nameof(ShippingFeePreview))]
    private ShippingFeeRateBasis shippingFeeBasis = ShippingFeeRateBasis.Negotiated;

    /// <summary>RadioButton-friendly views of ShippingFeeBasis — only the checked button's true is acted on, so the other button unchecking never flips the basis back.</summary>
    public bool UseNegotiatedFeeRate
    {
        get => ShippingFeeBasis == ShippingFeeRateBasis.Negotiated;
        set
        {
            if (value)
            {
                ShippingFeeBasis = ShippingFeeRateBasis.Negotiated;
            }
        }
    }

    public bool UsePublishedFeeRate
    {
        get => ShippingFeeBasis == ShippingFeeRateBasis.Published;
        set
        {
            if (value)
            {
                ShippingFeeBasis = ShippingFeeRateBasis.Published;
            }
        }
    }

    /// <summary>Only own-account shipments on a card linked to a Printavo order — with third-party billing the customer already pays UPS directly, and an unlinked New Shipment has no invoice to add to.</summary>
    public bool CanAddShippingToInvoice => !BillThirdParty && !string.IsNullOrWhiteSpace(_printavoOrderId);

    /// <summary>What the invoice fee would be if shipped right now, from the current quote for the selected service + account.</summary>
    public string ShippingFeePreview
    {
        get
        {
            var quote = RatesAreCurrent ? SelectedRateQuote : null;
            if (quote?.TotalCost is not { } negotiated)
            {
                return "Get Rates to see the fee.";
            }

            var fee = ShippingFeeCalculator.Calculate(negotiated, quote.PublishedCost, ShippingFeeBasis);
            if (ShippingFeeCalculator.UsesPublishedRate(negotiated, quote.PublishedCost, ShippingFeeBasis))
            {
                return $"Invoice fee: {fee:C} (published rate)";
            }

            var noDiscountNote = ShippingFeeBasis == ShippingFeeRateBasis.Published ? "; published rate is no higher" : string.Empty;
            var markup = ShippingFeeCalculator.MarkupPercent == 0 ? string.Empty : $" + {ShippingFeeCalculator.MarkupPercent:0.##}%";
            return $"Invoice fee: {fee:C} (negotiated {negotiated:C}{markup}{noDiscountNote})";
        }
    }

    partial void OnSelectedSecondaryContactChanged(StaffContact value) => QuantumViewSecondaryEmail = value.Email;

    /// <summary>
    /// Preselects the order's owner as the "also notify on delivery" contact.
    /// The owner lookup can finish after the card already exists, so this is
    /// callable later too — it never overrides a pick staff already made, or
    /// touches a card that has already shipped.
    /// </summary>
    public void ApplyDefaultSecondaryContact(StaffContact contact)
    {
        if (Result is null && SelectedSecondaryContact == StaffContacts.None)
        {
            SelectedSecondaryContact = contact;
        }
    }

    /// <summary>Read-only display of what goes on the label as REF1/REF2 — the order's Printavo PO# and invoice (visualId) number.</summary>
    public string ReferenceNumbersDisplay => string.IsNullOrWhiteSpace(_poNumber)
        ? $"Ref 2: Invoice {_printavoVisualId} (no PO # on this order)"
        : $"Ref 1: PO {_poNumber} · Ref 2: Invoice {_printavoVisualId}";

    public ShipmentCardViewModel(
        string printavoOrderId,
        string printavoVisualId,
        string? poNumber,
        string? contactEmail,
        bool hasMultipleShipToAddresses,
        string customerId,
        string customerCompanyName,
        Contractor? contractor,
        string shipToLabelText,
        UpsPartyInput shipTo,
        ContractorShipFromAddress defaultShipFrom,
        IReadOnlyList<OwnUpsAccount> ownAccounts,
        IShippingWorkflowService workflowService,
        IUpsClient upsClient,
        IContractorRepository contractorRepository,
        IContractorProvisioningService contractorProvisioningService,
        IDialogService dialogService,
        Action<ShipmentCardViewModel> onRemove,
        Action<Contractor>? onContractorProvisioned = null,
        bool useCombinedTaskWriteBack = false,
        ShipmentRecord? existingShipment = null,
        Action? onPrintavoWriteBack = null)
    {
        _printavoOrderId = printavoOrderId;
        _printavoVisualId = printavoVisualId;
        _poNumber = poNumber;
        _hasMultipleShipToAddresses = hasMultipleShipToAddresses;
        _customerId = customerId;
        _customerCompanyName = customerCompanyName;
        _contractor = contractor;
        _shipTo = shipTo;
        _rateOrigin = UpsPartyMapper.FromShipFrom(defaultShipFrom);
        RateOriginSummary = $"Rates quoted from {defaultShipFrom.CompanyName}, {defaultShipFrom.City}, {defaultShipFrom.State}";
        _workflowService = workflowService;
        _upsClient = upsClient;
        _contractorRepository = contractorRepository;
        _contractorProvisioningService = contractorProvisioningService;
        _dialogService = dialogService;
        _onRemove = onRemove;
        _onContractorProvisioned = onContractorProvisioned;
        _useCombinedTaskWriteBack = useCombinedTaskWriteBack;
        _onPrintavoWriteBack = onPrintavoWriteBack;

        QuantumViewPrimaryEmail = contactEmail ?? string.Empty;

        ShipToLabel = shipToLabelText;
        UpdateShipToSummary();
        ResetShipToEditFields();

        // A customer with no contractor-specific ship-from address configured
        // ships as the shop itself (Settings > Default Ship-From Address)
        // rather than blocking on a contractor setup step. When the contractor
        // does have its own address(es), the shop's own address is still offered
        // as an extra option (e.g. an order the shop wants to ship under its own
        // name despite the contractor relationship) — added as a non-default copy
        // so it doesn't collide with the contractor's own default in the picker.
        ShipFromOptions = contractor is not null && contractor.ShipFromAddresses.Count > 0
            ? new ObservableCollection<ContractorShipFromAddress>(contractor.ShipFromAddresses)
                { new ContractorShipFromAddress
                    {
                        Label = defaultShipFrom.Label,
                        CompanyName = defaultShipFrom.CompanyName,
                        AttentionName = defaultShipFrom.AttentionName,
                        Address1 = defaultShipFrom.Address1,
                        Address2 = defaultShipFrom.Address2,
                        City = defaultShipFrom.City,
                        State = defaultShipFrom.State,
                        PostalCode = defaultShipFrom.PostalCode,
                        CountryCode = defaultShipFrom.CountryCode,
                        PhoneNumber = defaultShipFrom.PhoneNumber,
                        IsDefault = false
                    } }
            : new ObservableCollection<ContractorShipFromAddress> { defaultShipFrom };

        if (contractor is not null)
        {
            BillingAccountOptions = contractor.BillingAccounts;
        }

        RebuildBillingAccountPickerOptions();

        OwnAccountOptions = new ObservableCollection<OwnUpsAccount>(ownAccounts);
        ShowNoOwnAccountWarning = existingShipment is null && OwnAccountOptions.Count == 0;

        SelectedShipFrom = ShipFromOptions.FirstOrDefault(a => a.IsDefault) ?? ShipFromOptions.FirstOrDefault();
        SelectedOwnAccount = OwnAccountOptions.FirstOrDefault(a => a.IsDefault) ?? OwnAccountOptions.FirstOrDefault();

        // A contractor with a default third-party billing account should
        // ship on it automatically rather than making staff remember to
        // check the box every time — but only when there's a genuine
        // default (not just "any" account), since with several non-default
        // accounts on file there's no safe way to guess which one to bill.
        var defaultBillingAccount = BillingAccountOptions.FirstOrDefault(a => a.IsDefault);
        SelectedBillingAccount = defaultBillingAccount ?? BillingAccountOptions.FirstOrDefault();
        BillThirdParty = defaultBillingAccount is not null;

        SelectedServiceLevel = ServiceLevels.First(s => s.Code == "03");

        if (existingShipment is not null)
        {
            // A shipment made earlier, reloaded when its order is reopened:
            // straight to the result view, and no address re-validation — the
            // label already exists, so a fresh check would only add noise.
            Result = existingShipment;
            AddressValidationStatus = string.Empty;
        }
        else
        {
            // Fire-and-forget: applies the residential/commercial classification
            // to _shipTo as soon as it's known, so Get Rates/Ship & Print use it
            // even if the user acts before this finishes.
            _ = ValidateAddressAsync();
        }

        // Keeps ShipDisabledReason/ShipCommand current as package weights are
        // typed in — Packages is a plain ObservableCollection, so additions/
        // removals and each row's own property edits need to be watched
        // explicitly rather than picked up by [NotifyCanExecuteChangedFor].
        Packages.CollectionChanged += (_, e) =>
        {
            if (e.OldItems is not null)
            {
                foreach (PackageRowViewModel row in e.OldItems)
                {
                    row.PropertyChanged -= OnPackageRowChanged;
                }
            }
            if (e.NewItems is not null)
            {
                foreach (PackageRowViewModel row in e.NewItems)
                {
                    row.PropertyChanged += OnPackageRowChanged;
                }
            }
            RefreshShipReadiness();
        };
        foreach (var row in Packages)
        {
            row.PropertyChanged += OnPackageRowChanged;
        }
    }

    private void OnPackageRowChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => RefreshShipReadiness();

    private void RefreshShipReadiness()
    {
        OnPropertyChanged(nameof(ShipDisabledReason));
        // Package/ship-to edits can make the quote stale, which changes the fee.
        OnPropertyChanged(nameof(ShippingFeePreview));
        ShipCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// UPS always needs one of the shop's own accounts as the Shipper — even
    /// when a third party pays — so with none set up in Settings nothing can
    /// ship. Shown as a banner at the top of an unshipped card, not just as
    /// the disabled-Ship reason, so staff see it before filling anything in.
    /// </summary>
    public bool ShowNoOwnAccountWarning { get; }

    private const string NoOwnAccountMessage = "No UPS account of your own is set up — add one in Settings > Our UPS Accounts.";

    private bool CanShip() => Result is null && ShipDisabledReason is null;

    /// <summary>Why the Ship button is currently disabled, or null if it's ready — surfaced under the button in the UI so staff don't have to guess which field is missing.</summary>
    public string? ShipDisabledReason
    {
        get
        {
            if (IsBusy)
            {
                return null;
            }

            if (SelectedShipFrom is null)
            {
                return "Choose a ship-from address.";
            }

            if (SelectedOwnAccount is null)
            {
                return NoOwnAccountMessage;
            }

            if (BillThirdParty && !CanResolveBillingAccount())
            {
                return "Choose or enter a billing account.";
            }

            if (Packages.Count == 0 || Packages.Any(p => p.WeightLbs <= 0))
            {
                return "Enter a weight for every package.";
            }

            // Own-account shipments record the Get Rates quote as their cost,
            // so one must exist for exactly what's about to be shipped.
            // Third-party billing isn't the shop's cost, so it's exempt.
            if (!BillThirdParty)
            {
                if (RateQuotes.Count == 0)
                {
                    return "Click Get Rates first — the shipping cost is quoted from the shop's address.";
                }

                if (!RatesAreCurrent)
                {
                    return "Shipment details changed since rates were fetched — click Get Rates again.";
                }

                if (SelectedRateQuote is null)
                {
                    return $"No rate for {SelectedServiceLevel.Name} on this account — pick one of the quoted services, or click Get Rates again.";
                }
            }

            if (EnableQuantumView && string.IsNullOrWhiteSpace(QuantumViewPrimaryEmail))
            {
                return "UPS Email Notifications is on but there's no notification email.";
            }

            return null;
        }
    }

    /// <summary>Everything that feeds a Get Rates request except the service level and our own account (the quotes already cover every service, and every own account). Ship-to is a record, so ToString captures every field incl. IsResidential.</summary>
    private string CurrentRateKey()
    {
        var (_, billingNumber, billingZip, billingCountry) = BillThirdParty && CanResolveBillingAccount()
            ? ResolveBillingAccount()
            : (string.Empty, string.Empty, string.Empty, string.Empty);

        return string.Join("|",
            _shipTo.ToString(),
            BillThirdParty,
            billingNumber,
            billingZip,
            billingCountry,
            string.Join(";", Packages.Select(p => $"{p.WeightLbs}x{p.LengthIn}x{p.WidthIn}x{p.HeightIn}")));
    }

    private bool RatesAreCurrent => RateQuotes.Count > 0 && _ratedKey == CurrentRateKey();

    /// <summary>The quote matching the selected service + own account, if Get Rates returned one with a cost.</summary>
    private UpsRateQuote? SelectedRateQuote => SelectedOwnAccount is null
        ? null
        : RateQuotes.FirstOrDefault(q =>
            q.ServiceCode == SelectedServiceLevel.Code
            && q.AccountNumber == SelectedOwnAccount.AccountNumber
            && q.TotalCost is not null);

    private bool CanResolveBillingAccount() => ShowNewBillingAccountFields
        ? !string.IsNullOrWhiteSpace(NewBillingAccountNumber)
        : SelectedBillingAccount is not null;

    private void RebuildBillingAccountPickerOptions() =>
        BillingAccountPickerOptions = new ObservableCollection<ContractorBillingAccount>(BillingAccountOptions) { NewBillingAccountSentinel };

    /// <summary>Resolves which account/zip/country to bill for this shipment — a saved ContractorBillingAccount, or the ad hoc "new account" fields when there's no saved account selected (or the user chose to enter a different one).</summary>
    private (string Label, string Number, string Zip, string Country) ResolveBillingAccount() =>
        ShowNewBillingAccountFields
            ? (string.IsNullOrWhiteSpace(NewBillingLabel) ? "Third-Party Billing" : NewBillingLabel,
               NewBillingAccountNumber,
               NewBillingPostalCode,
               string.IsNullOrWhiteSpace(NewBillingCountryCode) ? "US" : NewBillingCountryCode)
            : (SelectedBillingAccount!.Label, SelectedBillingAccount.UpsAccountNumber,
               SelectedBillingAccount.UpsAccountPostalCode, SelectedBillingAccount.UpsAccountCountryCode);

    [RelayCommand(CanExecute = nameof(CanShip))]
    private async Task ShipAsync()
    {
        if (SelectedShipFrom is null || SelectedOwnAccount is null)
        {
            return;
        }

        if (EnableQuantumView && string.IsNullOrWhiteSpace(QuantumViewPrimaryEmail))
        {
            ErrorMessage = "UPS Email Notifications is on but there's no notification email — enter one or turn it off.";
            return;
        }

        // Captured before anything below can touch the billing fields
        // (saving a new billing account clears them, which changes the key).
        var ratedQuote = RatesAreCurrent ? SelectedRateQuote : null;
        if (!BillThirdParty && ratedQuote is null)
        {
            ErrorMessage = ShipDisabledReason ?? "Click Get Rates again before shipping.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        PrintWarning = null;
        try
        {
            if (BillThirdParty && SaveBillingAccountToProfile && ShowNewBillingAccountFields)
            {
                await SaveNewBillingAccountToProfileAsync();
            }

            var (billingLabel, billingNumber, billingZip, billingCountry) = BillThirdParty
                ? ResolveBillingAccount()
                : (string.Empty, string.Empty, string.Empty, string.Empty);

            var input = new ShipAndPrintInput(
                _printavoOrderId,
                _printavoVisualId,
                _contractor?.Id,
                SelectedShipFrom.Label,
                UpsPartyMapper.FromShipFrom(SelectedShipFrom),
                ShipToLabel,
                _shipTo,
                BillThirdParty ? UpsBillingType.ThirdParty : UpsBillingType.OwnAccount,
                BillThirdParty ? billingLabel : null,
                BillThirdParty ? billingNumber : null,
                BillThirdParty ? billingZip : null,
                BillThirdParty ? billingCountry : null,
                SelectedOwnAccount.AccountNumber,
                SelectedServiceLevel.Code,
                SelectedServiceLevel.Name,
                Packages.Select(p => new UpsPackageInput(p.WeightLbs, p.LengthIn, p.WidthIn, p.HeightIn)).ToList(),
                ReferenceNumber1: _poNumber,
                ReferenceNumber2: _printavoVisualId,
                HasMultipleShipToAddresses: _hasMultipleShipToAddresses,
                EnableQuantumView: EnableQuantumView,
                QuantumViewPrimaryEmail: EnableQuantumView ? QuantumViewPrimaryEmail : null,
                QuantumViewSecondaryEmail: EnableQuantumView ? QuantumViewSecondaryEmail : null,
                Notes: string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
                UseCombinedTaskWriteBack: _useCombinedTaskWriteBack,
                RatedCost: ratedQuote?.TotalCost,
                InvoiceFeeAmount: AddShippingToInvoice && CanAddShippingToInvoice && ratedQuote?.TotalCost is { } negotiated
                    ? ShippingFeeCalculator.Calculate(negotiated, ratedQuote.PublishedCost, ShippingFeeBasis)
                    : null);

            var outcome = await _workflowService.ShipAndPrintAsync(input);
            Result = outcome.ShipmentRecord;
            PrintWarning = outcome.PrintWarning;
            PrintavoWriteBackWarning = outcome.PrintavoWriteBackWarning;
            _onPrintavoWriteBack?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Shipment failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            ShipCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>
    /// Provisions a Contractor on the fly (via the same "Import from
    /// Printavo" path the Contractors screen uses, so a saved account also
    /// gets the usual best-effort ship-from prefill) if this order's
    /// customer isn't one yet, then saves the ad hoc billing account fields
    /// onto it — mirrors ContractorsViewModel.AddBillingAccountAsync.
    /// </summary>
    private async Task SaveNewBillingAccountToProfileAsync()
    {
        if (_contractor is null)
        {
            _contractor = await _contractorProvisioningService.ImportFromPrintavoAsync(_customerId, _customerCompanyName);
            _onContractorProvisioned?.Invoke(_contractor);
        }

        var saved = await _contractorRepository.SaveBillingAccountAsync(new ContractorBillingAccount
        {
            ContractorId = _contractor.Id,
            Label = string.IsNullOrWhiteSpace(NewBillingLabel) ? "Third-Party Billing" : NewBillingLabel,
            UpsAccountNumber = NewBillingAccountNumber,
            UpsAccountPostalCode = NewBillingPostalCode,
            UpsAccountCountryCode = string.IsNullOrWhiteSpace(NewBillingCountryCode) ? "US" : NewBillingCountryCode,
            IsDefault = NewBillingIsDefault,
            Notes = NewBillingNotes
        });

        if (saved.IsDefault)
        {
            foreach (var other in _contractor.BillingAccounts)
            {
                other.IsDefault = false;
            }
        }

        _contractor.BillingAccounts.Add(saved);
        // BillingAccountOptions may already reference-equal _contractor.BillingAccounts
        // (when _contractor existed before this call) — the generated setter
        // would then silently skip PropertyChanged on this reassignment, so
        // HasSavedBillingAccounts/ShowNewBillingAccountFields are refreshed
        // explicitly rather than relying on it.
        BillingAccountOptions = _contractor.BillingAccounts;
        RebuildBillingAccountPickerOptions();
        OnPropertyChanged(nameof(HasSavedBillingAccounts));
        OnPropertyChanged(nameof(ShowNewBillingAccountFields));
        SelectedBillingAccount = saved; // not the sentinel — OnSelectedBillingAccountChanged flips UseNewBillingAccount back off
        NewBillingLabel = NewBillingAccountNumber = NewBillingPostalCode = NewBillingNotes = string.Empty;
        NewBillingCountryCode = "US";
        NewBillingIsDefault = false;
    }

    private bool CanValidateBillingAccount() => !IsValidatingBillingAccount;

    [RelayCommand(CanExecute = nameof(CanValidateBillingAccount))]
    private async Task ValidateBillingAccountAsync()
    {
        if (string.IsNullOrWhiteSpace(NewBillingAccountNumber) || SelectedShipFrom is null || SelectedOwnAccount is null)
        {
            BillingAccountValidationStatus = "Enter an account number, and choose a ship-from and our UPS account, first.";
            BillingAccountValidationHasWarning = true;
            return;
        }

        IsValidatingBillingAccount = true;
        BillingAccountValidationStatus = "Validating with UPS...";
        BillingAccountValidationHasWarning = false;
        try
        {
            var result = await _upsClient.ValidateThirdPartyAccountAsync(
                UpsPartyMapper.FromShipFrom(SelectedShipFrom),
                _shipTo,
                SelectedOwnAccount.AccountNumber,
                NewBillingAccountNumber,
                NewBillingPostalCode,
                string.IsNullOrWhiteSpace(NewBillingCountryCode) ? "US" : NewBillingCountryCode);

            BillingAccountValidationStatus = result.Message;
            BillingAccountValidationHasWarning = !result.IsValid;
        }
        catch (Exception ex)
        {
            BillingAccountValidationStatus = $"Couldn't validate account: {ex.Message}";
            BillingAccountValidationHasWarning = true;
        }
        finally
        {
            IsValidatingBillingAccount = false;
        }
    }

    /// <summary>False once both applicable Printavo write-back steps have succeeded — hides the retry button.</summary>
    public bool ShowPrintavoRetryButton => Result?.NeedsPrintavoWriteBack ?? false;

    /// <summary>Small status line for the result area — read once per Ship/Retry rather than bound directly to Result's own properties, since ShipmentRecord isn't INotifyPropertyChanged and RetryPrintavoWriteBackAsync mutates it in place (same reference back from SaveAsync), which wouldn't otherwise refresh a nested Result.X binding.</summary>
    public string PrintavoSyncStatus => Result switch
    {
        null => string.Empty,
        { UseCombinedTaskWriteBack: true } r => $"Printavo: {(string.IsNullOrWhiteSpace(r.PrintavoTaskId) ? "task not created" : "task created")}{InvoiceFeeSyncSuffix(r)}",
        { BillingType: ShipmentBillingType.OwnAccount } r => $"Printavo: {(string.IsNullOrWhiteSpace(r.PrintavoTaskId) ? "task not created" : "task created")}; {(r.CustomerNoteAppended ? "customer note updated" : "customer note not updated")}{InvoiceFeeSyncSuffix(r)}",
        var r => $"Printavo: {(r.CustomerNoteAppended ? "customer note updated" : "customer note not updated")}{InvoiceFeeSyncSuffix(r)}"
    };

    private static string InvoiceFeeSyncSuffix(ShipmentRecord r) => r switch
    {
        { InvoiceFeeAmount: null } => string.Empty,
        { Status: ShipmentStatus.Voided } => string.IsNullOrWhiteSpace(r.PrintavoFeeId) ? "; shipping fee removed" : "; shipping fee NOT removed",
        _ => string.IsNullOrWhiteSpace(r.PrintavoFeeId) ? "; shipping fee not added" : "; shipping fee added"
    };

    private bool CanRetryPrintavoWriteBack() => !IsBusy && ShowPrintavoRetryButton;

    [RelayCommand(CanExecute = nameof(CanRetryPrintavoWriteBack))]
    private async Task RetryPrintavoWriteBackAsync()
    {
        if (Result is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var (record, warning) = await _workflowService.RetryPrintavoWriteBackAsync(Result);
            // RetryPrintavoWriteBackAsync mutates and returns the same
            // ShipmentRecord reference — the generated Result setter would
            // no-op on a reference-equal reassignment, so the dependent
            // computed properties are refreshed explicitly rather than
            // relying on OnResultChanged firing.
            Result = record;
            PrintavoWriteBackWarning = warning;
            OnPropertyChanged(nameof(ShowPrintavoRetryButton));
            OnPropertyChanged(nameof(PrintavoSyncStatus));
            _onPrintavoWriteBack?.Invoke();
        }
        catch (Exception ex)
        {
            PrintavoWriteBackWarning = $"Retry failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            RetryPrintavoWriteBackCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>
    /// Own-account billing with more than one saved account gets rates from
    /// every account, merged and sorted by cost, so staff can pick whichever
    /// account+service comes out cheapest. Third-party billing (or a single
    /// saved account) just quotes the one account currently selected.
    /// </summary>
    private bool CanGetRates() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanGetRates))]
    private async Task GetRatesAsync()
    {
        var accountsToQuery = !BillThirdParty && OwnAccountOptions.Count > 1
            ? OwnAccountOptions.ToList()
            : SelectedOwnAccount is not null ? new List<OwnUpsAccount> { SelectedOwnAccount } : new List<OwnUpsAccount>();

        if (accountsToQuery.Count == 0)
        {
            ErrorMessage = NoOwnAccountMessage;
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        // Clear stale quotes up front — otherwise a failed refetch leaves the
        // previous (now-outdated) quotes on screen looking current.
        RateQuotes = new ObservableCollection<UpsRateQuote>();
        AppliedRateQuote = null;
        // Keyed before the requests go out, so an edit made while they're in
        // flight leaves the returned quotes correctly marked stale.
        _ratedKey = CurrentRateKey();
        try
        {
            // Each account is queried independently — one account being
            // misconfigured (or CIE-only, or momentarily rate-limited)
            // shouldn't hide rates that another account returned fine. All
            // accounts are queried concurrently, so the wait is the slowest
            // single account rather than the sum of all of them. Task.WhenAll
            // enumerates the lambdas synchronously on the UI thread, so every
            // BuildRequest runs before any response can come back — every
            // account is quoted on the same form state.
            var results = await Task.WhenAll(accountsToQuery.Select(async account =>
            {
                try
                {
                    var quotes = await _upsClient.ShopRatesAsync(BuildRequest(account));
                    return (Quotes: quotes.Select(q => q with { AccountLabel = account.Label, AccountNumber = account.AccountNumber }).ToList(), Error: (string?)null);
                }
                catch (Exception ex)
                {
                    return (Quotes: new List<UpsRateQuote>(), Error: accountsToQuery.Count > 1 ? $"{account.Label}: {ex.Message}" : ex.Message);
                }
            }));

            var allQuotes = results.SelectMany(r => r.Quotes).ToList();
            var perAccountErrors = results.Select(r => r.Error).OfType<string>().ToList();

            RateQuotes = new ObservableCollection<UpsRateQuote>(allQuotes.OrderBy(q => q.TotalCost));

            if (perAccountErrors.Count > 0)
            {
                ErrorMessage = allQuotes.Count > 0
                    ? $"Some accounts didn't return rates: {string.Join("; ", perAccountErrors)}"
                    : $"Couldn't get rates: {string.Join("; ", perAccountErrors)}";
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private UpsShipmentRequest BuildRequest(OwnUpsAccount account)
    {
        var (_, billingNumber, billingZip, billingCountry) = BillThirdParty && CanResolveBillingAccount()
            ? ResolveBillingAccount()
            : (string.Empty, string.Empty, string.Empty, string.Empty);

        return new(
            _rateOrigin,
            _shipTo,
            account.AccountNumber,
            BillThirdParty ? UpsBillingType.ThirdParty : UpsBillingType.OwnAccount,
            BillThirdParty ? billingNumber : null,
            BillThirdParty ? billingZip : null,
            BillThirdParty ? billingCountry : null,
            SelectedServiceLevel.Code,
            SelectedServiceLevel.Name,
            Packages.Select(p => new UpsPackageInput(p.WeightLbs, p.LengthIn, p.WidthIn, p.HeightIn)).ToList());
    }

    [ObservableProperty]
    private UpsRateQuote? appliedRateQuote;

    [RelayCommand]
    private void ApplyRateQuote(UpsRateQuote quote)
    {
        var serviceMatch = ServiceLevels.FirstOrDefault(s => s.Code == quote.ServiceCode);
        if (serviceMatch is not null)
        {
            SelectedServiceLevel = serviceMatch;
        }

        if (quote.AccountNumber is not null)
        {
            var accountMatch = OwnAccountOptions.FirstOrDefault(a => a.AccountNumber == quote.AccountNumber);
            if (accountMatch is not null)
            {
                SelectedOwnAccount = accountMatch;
            }
        }

        AppliedRateQuote = quote;
    }

    /// <summary>
    /// Checks the ship-to against UPS's Address Validation ("XAV") API and
    /// applies the residential/commercial classification to _shipTo so
    /// rating/shipping use the correct surcharge. Runs once automatically
    /// (see constructor) and can be re-run from the UI. NOTE: in UPS's CIE
    /// test environment, street-level validation only returns real matches
    /// for addresses in NY and CA — every other CIE address comes back with
    /// no candidates even when it's a real, deliverable address, so
    /// "couldn't verify" during CIE testing isn't necessarily a bad address.
    /// </summary>
    private bool CanValidateAddress() => !IsValidatingAddress;

    [RelayCommand(CanExecute = nameof(CanValidateAddress))]
    private async Task ValidateAddressAsync()
    {
        // _shipTo is a plain field, so nothing else notices when it changes —
        // every edit path runs through here, and both refreshes re-check
        // whether the current rate quotes still match the ship-to (once for
        // the caller's edit, once for any classification/ZIP+4 applied below).
        RefreshShipReadiness();
        IsValidatingAddress = true;
        AddressValidationStatus = "Validating address...";
        AddressValidationHasWarning = false;
        AddressSuggestions = new ObservableCollection<UpsAddressCandidate>();
        try
        {
            var validated = _shipTo;
            var result = await _upsClient.ValidateAddressAsync(validated);

            if (result.Classification == UpsAddressClassification.Residential)
            {
                _shipTo = _shipTo with { IsResidential = true };
            }
            else if (result.Classification == UpsAddressClassification.Commercial)
            {
                _shipTo = _shipTo with { IsResidential = false };
            }

            if (result.IsExactMatch)
            {
                AddressValidationStatus = result.Classification switch
                {
                    UpsAddressClassification.Residential => "UPS verified this address as residential (a residential surcharge may apply).",
                    UpsAddressClassification.Commercial => "UPS verified this address as commercial.",
                    _ => "UPS verified this address but couldn't classify it as residential or commercial."
                };

                if (TryApplyZipPlusFour(validated, result.Candidates))
                {
                    AddressValidationStatus += $" ZIP+4 added ({UsPostalCode.Format(_shipTo.PostalCode)}).";
                }
            }
            else if (result.IsAmbiguous && result.Candidates.Count > 0)
            {
                AddressSuggestions = new ObservableCollection<UpsAddressCandidate>(result.Candidates);
                AddressValidationStatus = result.Candidates.Count == 1
                    ? "UPS couldn't match this exactly. Its suggestion is below — click it to use it, or Edit Address to fix it by hand."
                    : $"UPS couldn't match this exactly. It suggested {result.Candidates.Count} addresses below — click one to use it, or Edit Address to fix it by hand.";
                AddressValidationHasWarning = true;
            }
            else if (result.HasNoCandidates)
            {
                AddressValidationStatus = result.IsTestEnvironment
                    ? "UPS returned no match for this address. This also happens for any address outside NY/CA while testing in the CIE environment, so it isn't necessarily wrong — but double-check it before shipping."
                    : "UPS couldn't find this address and had no suggestions. Double-check the street line — UPS often won't recognize abbreviations it doesn't use, e.g. a highway written as \"U.S. 22\" instead of \"US Highway 22\". Fix it with Edit Address, or ship anyway if you're sure it's right.";
                AddressValidationHasWarning = true;
            }
            else
            {
                AddressValidationStatus = "Address validation returned an unexpected result.";
                AddressValidationHasWarning = true;
            }
        }
        catch (Exception ex)
        {
            AddressValidationStatus = $"Couldn't validate address: {ex.Message}";
            AddressValidationHasWarning = true;
        }
        finally
        {
            IsValidatingAddress = false;
            RefreshShipReadiness();
        }
    }

    /// <summary>
    /// On an exact match UPS returns its standardized address as a candidate,
    /// including the ZIP+4 extension. Appends it to a plain 5-digit US ZIP —
    /// only when UPS's 5-digit ZIP agrees with ours, and only if the ship-to
    /// wasn't edited while validation was in flight.
    /// </summary>
    private bool TryApplyZipPlusFour(UpsPartyInput validated, IReadOnlyList<UpsAddressCandidate> candidates)
    {
        var candidate = candidates.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c.PostcodeExtension));
        if (candidate is null
            || validated.CountryCode != "US"
            || validated.PostalCode.Length != 5
            || candidate.PostalCode != validated.PostalCode
            || _shipTo with { IsResidential = null } != validated with { IsResidential = null })
        {
            return false;
        }

        _shipTo = _shipTo with { PostalCode = candidate.FullPostalCode };
        UpdateShipToSummary();
        if (!IsEditingShipTo)
        {
            // Don't clobber a half-typed manual edit.
            ResetShipToEditFields();
        }

        return true;
    }

    /// <summary>Replaces the ship-to's street/city/state/ZIP with a UPS suggestion (keeping name, attention and phone), then re-validates it.</summary>
    [RelayCommand]
    private async Task UseSuggestedAddressAsync(UpsAddressCandidate candidate)
    {
        var extraLines = candidate.AddressLines.Skip(1).ToList();
        _shipTo = _shipTo with
        {
            Address1 = UpsAddressLimits.Truncate(candidate.AddressLines.FirstOrDefault(), UpsAddressLimits.AddressLineMaxLength) ?? string.Empty,
            // UPS folds a valid suite/apt into its own address lines; if it
            // returned just one line, keep ours rather than silently
            // dropping an apartment number.
            Address2 = extraLines.Count > 0
                ? UpsAddressLimits.Truncate(string.Join(" ", extraLines), UpsAddressLimits.AddressLineMaxLength)
                : _shipTo.Address2,
            City = UpsAddressLimits.Truncate(candidate.City, UpsAddressLimits.CityMaxLength) ?? string.Empty,
            State = UpsAddressLimits.Truncate(candidate.State, UpsAddressLimits.StateProvinceCodeMaxLength) ?? string.Empty,
            PostalCode = UpsAddressLimits.Truncate(candidate.FullPostalCode, UpsAddressLimits.PostalCodeMaxLength) ?? string.Empty,
            CountryCode = candidate.CountryCode,
            IsResidential = null
        };

        UpdateShipToSummary();
        ResetShipToEditFields();

        await ValidateAddressAsync();
    }

    /// <summary>Manual override for when the Printavo-sourced ship-to is wrong or won't validate — replaces _shipTo outright and re-runs validation against the corrected address.</summary>
    [RelayCommand]
    private void EditShipTo() => IsEditingShipTo = true;

    [RelayCommand]
    private void CancelEditShipTo()
    {
        ResetShipToEditFields();
        IsEditingShipTo = false;
    }

    [RelayCommand]
    private async Task SaveShipToEditsAsync()
    {
        // A hand-corrected address is a fresh address as far as UPS is
        // concerned — IsResidential intentionally reset to null (via the
        // positional constructor below) until ValidateAddressAsync
        // reclassifies it.
        _shipTo = new UpsPartyInput(
            UpsAddressLimits.Truncate(EditShipToName, UpsAddressLimits.NameMaxLength) ?? string.Empty,
            UpsAddressLimits.Truncate(EditShipToAttentionName, UpsAddressLimits.AttentionNameMaxLength),
            UpsAddressLimits.Truncate(EditShipToAddress1, UpsAddressLimits.AddressLineMaxLength) ?? string.Empty,
            UpsAddressLimits.Truncate(EditShipToAddress2, UpsAddressLimits.AddressLineMaxLength),
            UpsAddressLimits.Truncate(EditShipToCity, UpsAddressLimits.CityMaxLength) ?? string.Empty,
            UpsAddressLimits.Truncate(EditShipToState, UpsAddressLimits.StateProvinceCodeMaxLength) ?? string.Empty,
            UpsAddressLimits.Truncate(EditShipToPostalCode, UpsAddressLimits.PostalCodeMaxLength) ?? string.Empty,
            UpsAddressLimits.Truncate(EditShipToCountryCode, UpsAddressLimits.CountryCodeLength) ?? "US",
            UpsAddressLimits.Truncate(EditShipToPhoneNumber, UpsAddressLimits.PhoneNumberMaxLength));

        UpdateShipToSummary();
        IsEditingShipTo = false;

        await ValidateAddressAsync();
    }

    private void UpdateShipToSummary() =>
        ShipToSummary = $"{_shipTo.Name}\n{_shipTo.Address1} {_shipTo.Address2}\n{_shipTo.City}, {_shipTo.State} {UsPostalCode.Format(_shipTo.PostalCode)}".Replace("  ", " ");

    private void ResetShipToEditFields()
    {
        EditShipToName = _shipTo.Name;
        EditShipToAttentionName = _shipTo.AttentionName;
        EditShipToAddress1 = _shipTo.Address1;
        EditShipToAddress2 = _shipTo.Address2;
        EditShipToCity = _shipTo.City;
        EditShipToState = _shipTo.State;
        EditShipToPostalCode = _shipTo.PostalCode;
        EditShipToCountryCode = _shipTo.CountryCode;
        EditShipToPhoneNumber = _shipTo.PhoneNumber;
    }

    private bool CanVoid() => Result is { Status: ShipmentStatus.Purchased } && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanVoid))]
    private async Task VoidAsync()
    {
        if (Result is null)
        {
            return;
        }

        if (!await _dialogService.ConfirmAsync("Void Shipment", "Void this shipment? This cannot be undone."))
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            // VoidShipmentAsync mutates and returns the same ShipmentRecord
            // reference (SaveAsync hands back what it was given) — the
            // generated Result setter would no-op on a reference-equal
            // reassignment, leaving every {Binding Result.*} in the result
            // panel (status text, etc.) showing stale pre-void data even
            // though the void itself succeeded. Force the refresh
            // explicitly, same fix as RetryPrintavoWriteBackAsync needed.
            var (record, warning) = await _workflowService.VoidShipmentAsync(Result);
            Result = record;
            OnPropertyChanged(nameof(Result));
            OnPropertyChanged(nameof(ShowPrintavoRetryButton));
            OnPropertyChanged(nameof(PrintavoSyncStatus));
            RetryPrintavoWriteBackCommand.NotifyCanExecuteChanged();
            PrintavoWriteBackWarning = warning;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Void failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            VoidCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanReprint() => Result is not null && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanReprint))]
    private async Task ReprintAsync()
    {
        if (Result is null)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await _workflowService.ReprintAsync(Result);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Reprint failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void AddPackage() => Packages.Add(new PackageRowViewModel());

    [RelayCommand]
    private void RemovePackage(PackageRowViewModel row)
    {
        if (Packages.Count > 1)
        {
            Packages.Remove(row);
            ErrorMessage = null;
        }
        else
        {
            ErrorMessage = "A shipment needs at least one package.";
        }
    }

    [RelayCommand]
    private void ClonePackage(PackageRowViewModel row)
    {
        var index = Packages.IndexOf(row);
        var count = Math.Max(1, row.CloneCount);
        for (var i = 0; i < count; i++)
        {
            Packages.Insert(index + 1 + i, row.Clone());
        }
    }

    [RelayCommand]
    private async Task RemoveAsync()
    {
        if (!await _dialogService.ConfirmAsync("Remove Shipment", "Remove this shipment card? Anything entered for it will be lost."))
        {
            return;
        }

        _onRemove(this);
    }

    partial void OnIsBusyChanged(bool value)
    {
        RefreshShipReadiness();
        VoidCommand.NotifyCanExecuteChanged();
        ReprintCommand.NotifyCanExecuteChanged();
        RetryPrintavoWriteBackCommand.NotifyCanExecuteChanged();
        GetRatesCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedShipFromChanged(ContractorShipFromAddress? value) => ShipCommand.NotifyCanExecuteChanged();

    partial void OnSelectedOwnAccountChanged(OwnUpsAccount? value) => ShipCommand.NotifyCanExecuteChanged();

    /// <summary>Picking the "+ Enter a different account" sentinel from the dropdown is how UseNewBillingAccount gets turned on — there's no separate checkbox for it.</summary>
    partial void OnSelectedBillingAccountChanged(ContractorBillingAccount? value)
    {
        UseNewBillingAccount = ReferenceEquals(value, NewBillingAccountSentinel);
        ShipCommand.NotifyCanExecuteChanged();
    }

    partial void OnBillThirdPartyChanged(bool value) => ShipCommand.NotifyCanExecuteChanged();

    partial void OnBillingAccountOptionsChanged(ObservableCollection<ContractorBillingAccount> value)
    {
        OnPropertyChanged(nameof(HasSavedBillingAccounts));
        OnPropertyChanged(nameof(ShowNewBillingAccountFields));
    }

    partial void OnUseNewBillingAccountChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowNewBillingAccountFields));
        ShipCommand.NotifyCanExecuteChanged();
    }

    partial void OnNewBillingAccountNumberChanged(string value) => ShipCommand.NotifyCanExecuteChanged();

    partial void OnNewBillingPostalCodeChanged(string value) => RefreshShipReadiness();

    partial void OnNewBillingCountryCodeChanged(string value) => RefreshShipReadiness();

    partial void OnSaveBillingAccountToProfileChanged(bool value) => ShipCommand.NotifyCanExecuteChanged();

    partial void OnResultChanged(ShipmentRecord? value)
    {
        ShipCommand.NotifyCanExecuteChanged();
        VoidCommand.NotifyCanExecuteChanged();
        RetryPrintavoWriteBackCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(ShowPrintavoRetryButton));
        OnPropertyChanged(nameof(PrintavoSyncStatus));
        CanEditShipTo = value is null;
        if (value is not null)
        {
            IsEditingShipTo = false;
        }
    }
}
