using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrintavoShipper.App.Services;
using PrintavoShipper.Core.Interfaces;
using PrintavoShipper.Core.Models;
using PrintavoShipper.FedEx;

namespace PrintavoShipper.App.ViewModels;

/// <summary>
/// FedEx equivalent of ShipmentCardViewModel — one card per ship-to address
/// on a FedEx-delivery-method order. Smaller than the UPS card in one
/// respect: FedEx has no Quantum-View-style notification feature, so there's
/// no notification section. Own-account billing IS supported (the shop does
/// have its own FedEx account, just uses it less often than third-party
/// billing) but deliberately doesn't reproduce UPS's multi-account
/// rate-shopping-and-merge — there's no evidence this shop juggles multiple
/// FedEx accounts the way it does UPS accounts, so Get Rates only queries
/// whichever single account is currently selected.
/// </summary>
public partial class FedExShipmentCardViewModel : ObservableObject
{
    private readonly string _printavoOrderId;
    private readonly string _printavoVisualId;
    private readonly string? _poNumber;
    private readonly bool _hasMultipleShipToAddresses;
    private readonly string _customerId;
    private readonly string _customerCompanyName;
    private Contractor? _contractor;
    private FedExPartyInput _shipTo;

    /// <summary>The shop's Default Ship-From address — Get Rates always quotes from here, and that quote is the recorded cost. See ShipmentCardViewModel._rateOrigin.</summary>
    private readonly FedExPartyInput _rateOrigin;

    /// <summary>CurrentRateKey() as of the last Get Rates — RateQuotes only count as current while it still matches.</summary>
    private string? _ratedKey;

    private readonly IShippingWorkflowService _workflowService;
    private readonly IFedExClient _fedExClient;
    private readonly IContractorRepository _contractorRepository;
    private readonly IContractorProvisioningService _contractorProvisioningService;
    private readonly IDialogService _dialogService;
    private readonly Action<FedExShipmentCardViewModel> _onRemove;
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
    private ObservableCollection<OwnFedExAccount> ownAccountOptions = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShipDisabledReason))]
    [NotifyCanExecuteChangedFor(nameof(ShipCommand))]
    private OwnFedExAccount? selectedOwnAccount;

    /// <summary>True bills a saved/ad hoc third-party account (the common case); false bills the shop's own FedEx account.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShipDisabledReason))]
    [NotifyCanExecuteChangedFor(nameof(ShipCommand))]
    private bool billThirdParty = true;

    /// <summary>Sentinel item appended to BillingAccountPickerOptions so "enter a different account" is a normal dropdown choice — mirrors ShipmentCardViewModel's NewBillingAccountSentinel.</summary>
    private static readonly ContractorFedExBillingAccount NewBillingAccountSentinel = new() { Id = -1, Label = "+ Enter a different account" };

    [ObservableProperty]
    private ObservableCollection<ContractorFedExBillingAccount> billingAccountOptions = new();

    [ObservableProperty]
    private ObservableCollection<ContractorFedExBillingAccount> billingAccountPickerOptions = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShipDisabledReason))]
    [NotifyCanExecuteChangedFor(nameof(ShipCommand))]
    private ContractorFedExBillingAccount? selectedBillingAccount;

    [ObservableProperty]
    private bool useNewBillingAccount;

    [ObservableProperty]
    private string newBillingLabel = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShipDisabledReason))]
    [NotifyCanExecuteChangedFor(nameof(ShipCommand))]
    private string newBillingAccountNumber = string.Empty;

    [ObservableProperty]
    private string newBillingCountryCode = "US";

    [ObservableProperty]
    private bool newBillingIsDefault;

    [ObservableProperty]
    private string newBillingNotes = string.Empty;

    [ObservableProperty]
    private bool saveBillingAccountToProfile;

    /// <summary>Display name for the "Save this account to ___'s profile" checkbox — the contractor's name once one exists, otherwise the order's own customer name.</summary>
    public string BillingProfileDisplayName => _contractor?.CompanyName ?? _customerCompanyName;

    public bool HasSavedBillingAccounts => BillingAccountOptions.Count > 0;

    public bool ShowNewBillingAccountFields => !HasSavedBillingAccounts || UseNewBillingAccount;

    [ObservableProperty]
    private ObservableCollection<ServiceLevelOption> serviceLevels = new(
        FedExServiceLevels.All.Select(s => new ServiceLevelOption(s.Code, s.Name)));

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShipDisabledReason))]
    [NotifyCanExecuteChangedFor(nameof(ShipCommand))]
    private ServiceLevelOption selectedServiceLevel = new("FEDEX_GROUND", "FedEx Ground");

    [ObservableProperty]
    private ObservableCollection<PackageRowViewModel> packages = new() { new PackageRowViewModel() };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShipDisabledReason))]
    [NotifyCanExecuteChangedFor(nameof(ShipCommand))]
    private ObservableCollection<FedExRateQuote> rateQuotes = new();

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
    private string? notes;

    /// <summary>Read-only display of what goes on the label as REF1/REF2 — the order's Printavo PO# and invoice (visualId) number.</summary>
    public string ReferenceNumbersDisplay => string.IsNullOrWhiteSpace(_poNumber)
        ? $"Ref 2: Invoice {_printavoVisualId} (no PO # on this order)"
        : $"Ref 1: PO {_poNumber} · Ref 2: Invoice {_printavoVisualId}";

    public FedExShipmentCardViewModel(
        string printavoOrderId,
        string printavoVisualId,
        string? poNumber,
        bool hasMultipleShipToAddresses,
        string customerId,
        string customerCompanyName,
        Contractor? contractor,
        string shipToLabelText,
        FedExPartyInput shipTo,
        ContractorShipFromAddress defaultShipFrom,
        IReadOnlyList<OwnFedExAccount> ownAccounts,
        IShippingWorkflowService workflowService,
        IFedExClient fedExClient,
        IContractorRepository contractorRepository,
        IContractorProvisioningService contractorProvisioningService,
        IDialogService dialogService,
        Action<FedExShipmentCardViewModel> onRemove,
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
        _rateOrigin = FedExPartyMapper.FromShipFrom(defaultShipFrom);
        RateOriginSummary = $"Rates quoted from {defaultShipFrom.CompanyName}, {defaultShipFrom.City}, {defaultShipFrom.State}";
        _workflowService = workflowService;
        _fedExClient = fedExClient;
        _contractorRepository = contractorRepository;
        _contractorProvisioningService = contractorProvisioningService;
        _dialogService = dialogService;
        _onRemove = onRemove;
        _onContractorProvisioned = onContractorProvisioned;
        _useCombinedTaskWriteBack = useCombinedTaskWriteBack;
        _onPrintavoWriteBack = onPrintavoWriteBack;

        ShipToLabel = shipToLabelText;
        UpdateShipToSummary();
        ResetShipToEditFields();

        // Same "shop default when the contractor has no ship-from address of
        // its own" fallback as ShipmentCardViewModel — see its constructor
        // comment for the full reasoning.
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
            BillingAccountOptions = contractor.FedExBillingAccounts;
        }

        RebuildBillingAccountPickerOptions();

        OwnAccountOptions = new ObservableCollection<OwnFedExAccount>(ownAccounts);
        SelectedOwnAccount = OwnAccountOptions.FirstOrDefault(a => a.IsDefault) ?? OwnAccountOptions.FirstOrDefault();
        ShowNoOwnAccountWarning = existingShipment is null && OwnAccountOptions.Count == 0;

        SelectedShipFrom = ShipFromOptions.FirstOrDefault(a => a.IsDefault) ?? ShipFromOptions.FirstOrDefault();

        // Bills the shop's own account by default, and third-party only when
        // the contractor profile has a saved FedEx account to bill.
        var defaultBillingAccount = BillingAccountOptions.FirstOrDefault(a => a.IsDefault);
        SelectedBillingAccount = defaultBillingAccount ?? BillingAccountOptions.FirstOrDefault();
        BillThirdParty = BillingAccountOptions.Count > 0;

        SelectedServiceLevel = ServiceLevels.First(s => s.Code == "FEDEX_GROUND");

        if (existingShipment is not null)
        {
            // A shipment made earlier, reloaded when its order is reopened:
            // straight to the result view, no address re-validation.
            Result = existingShipment;
            AddressValidationStatus = string.Empty;
        }
        else
        {
            _ = ValidateAddressAsync();
        }

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
        ShipCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// FedEx always needs one of the shop's own accounts as the shipper —
    /// even when a third party pays — so with none set up in Settings nothing
    /// can ship. Shown as a banner at the top of an unshipped card, not just
    /// as the disabled-Ship reason, so staff see it before filling anything in.
    /// </summary>
    public bool ShowNoOwnAccountWarning { get; }

    private const string NoOwnAccountMessage = "No FedEx account of your own is set up — add one in Settings > Our FedEx Accounts.";

    private bool CanShip() => Result is null && ShipDisabledReason is null;

    /// <summary>Why the Ship button is currently disabled, or null if it's ready.</summary>
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
                return "Choose or enter a FedEx billing account.";
            }

            if (Packages.Count == 0 || Packages.Any(p => p.WeightLbs <= 0))
            {
                return "Enter a weight for every package.";
            }

            // Same rule as the UPS card: own-account shipments record the Get
            // Rates quote as their cost, so one must exist for exactly what's
            // about to be shipped. Third-party billing is exempt.
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
                    return $"No rate for {SelectedServiceLevel.Name} — pick one of the quoted services, or click Get Rates again.";
                }
            }

            return null;
        }
    }

    /// <summary>Everything that feeds a Get Rates request except the service level (the quotes cover every service). Unlike UPS, FedEx quotes aren't tagged per account, so our own account is part of the key.</summary>
    private string CurrentRateKey()
    {
        var (_, billingNumber, billingZip, billingCountry) = BillThirdParty && CanResolveBillingAccount()
            ? ResolveBillingAccount()
            : (string.Empty, string.Empty, string.Empty, string.Empty);

        return string.Join("|",
            _shipTo.ToString(),
            SelectedOwnAccount?.AccountNumber,
            BillThirdParty,
            billingNumber,
            billingZip,
            billingCountry,
            string.Join(";", Packages.Select(p => $"{p.WeightLbs}x{p.LengthIn}x{p.WidthIn}x{p.HeightIn}")));
    }

    private bool RatesAreCurrent => RateQuotes.Count > 0 && _ratedKey == CurrentRateKey();

    /// <summary>The quote matching the selected service, if Get Rates returned one with a cost.</summary>
    private FedExRateQuote? SelectedRateQuote =>
        RateQuotes.FirstOrDefault(q => q.ServiceType == SelectedServiceLevel.Code && q.TotalCost is not null);

    private bool CanResolveBillingAccount() => ShowNewBillingAccountFields
        ? !string.IsNullOrWhiteSpace(NewBillingAccountNumber)
        : SelectedBillingAccount is not null;

    private void RebuildBillingAccountPickerOptions() =>
        BillingAccountPickerOptions = new ObservableCollection<ContractorFedExBillingAccount>(BillingAccountOptions) { NewBillingAccountSentinel };

    /// <summary>Resolves which account/zip/country to bill for this shipment — a saved ContractorFedExBillingAccount, or the ad hoc "new account" fields.</summary>
    private (string Label, string Number, string Zip, string Country) ResolveBillingAccount() =>
        ShowNewBillingAccountFields
            ? (string.IsNullOrWhiteSpace(NewBillingLabel) ? "Third-Party Billing" : NewBillingLabel,
               NewBillingAccountNumber,
               string.Empty,
               string.IsNullOrWhiteSpace(NewBillingCountryCode) ? "US" : NewBillingCountryCode)
            : (SelectedBillingAccount!.Label, SelectedBillingAccount.AccountNumber,
               SelectedBillingAccount.AccountPostalCode, SelectedBillingAccount.AccountCountryCode);

    [RelayCommand(CanExecute = nameof(CanShip))]
    private async Task ShipAsync()
    {
        if (SelectedShipFrom is null)
        {
            return;
        }

        // Captured before saving a new billing account can clear the billing fields (which changes the key).
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

            var input = new FedExShipAndPrintInput(
                _printavoOrderId,
                _printavoVisualId,
                _contractor?.Id,
                SelectedShipFrom.Label,
                FedExPartyMapper.FromShipFrom(SelectedShipFrom),
                ShipToLabel,
                _shipTo,
                BillThirdParty ? FedExBillingType.ThirdParty : FedExBillingType.OwnAccount,
                BillThirdParty ? billingLabel : null,
                BillThirdParty ? billingNumber : null,
                BillThirdParty ? billingZip : null,
                BillThirdParty ? billingCountry : null,
                ShipperAccountNumber: SelectedOwnAccount?.AccountNumber,
                SelectedServiceLevel.Code,
                SelectedServiceLevel.Name,
                Packages.Select(p => new FedExPackageInput(p.WeightLbs, p.LengthIn, p.WidthIn, p.HeightIn)).ToList(),
                ReferenceNumber1: _poNumber,
                ReferenceNumber2: _printavoVisualId,
                HasMultipleShipToAddresses: _hasMultipleShipToAddresses,
                Notes: string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
                UseCombinedTaskWriteBack: _useCombinedTaskWriteBack,
                RatedCost: ratedQuote?.TotalCost);

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

    /// <summary>Mirrors ShipmentCardViewModel.SaveNewBillingAccountToProfileAsync, against ContractorFedExBillingAccount.</summary>
    private async Task SaveNewBillingAccountToProfileAsync()
    {
        if (_contractor is null)
        {
            _contractor = await _contractorProvisioningService.ImportFromPrintavoAsync(_customerId, _customerCompanyName);
            _onContractorProvisioned?.Invoke(_contractor);
        }

        var saved = await _contractorRepository.SaveFedExBillingAccountAsync(new ContractorFedExBillingAccount
        {
            ContractorId = _contractor.Id,
            Label = string.IsNullOrWhiteSpace(NewBillingLabel) ? "Third-Party Billing" : NewBillingLabel,
            AccountNumber = NewBillingAccountNumber,
            AccountPostalCode = string.Empty,
            AccountCountryCode = string.IsNullOrWhiteSpace(NewBillingCountryCode) ? "US" : NewBillingCountryCode,
            IsDefault = NewBillingIsDefault,
            Notes = NewBillingNotes
        });

        if (saved.IsDefault)
        {
            foreach (var other in _contractor.FedExBillingAccounts)
            {
                other.IsDefault = false;
            }
        }

        _contractor.FedExBillingAccounts.Add(saved);
        BillingAccountOptions = _contractor.FedExBillingAccounts;
        RebuildBillingAccountPickerOptions();
        OnPropertyChanged(nameof(HasSavedBillingAccounts));
        OnPropertyChanged(nameof(ShowNewBillingAccountFields));
        SelectedBillingAccount = saved;
        NewBillingLabel = NewBillingAccountNumber = NewBillingNotes = string.Empty;
        NewBillingCountryCode = "US";
        NewBillingIsDefault = false;
    }

    /// <summary>False once both applicable Printavo write-back steps have succeeded — hides the retry button.</summary>
    public bool ShowPrintavoRetryButton => Result?.NeedsPrintavoWriteBack ?? false;

    public string PrintavoSyncStatus => Result switch
    {
        null => string.Empty,
        { UseCombinedTaskWriteBack: true } r => $"Printavo: {(string.IsNullOrWhiteSpace(r.PrintavoTaskId) ? "task not created" : "task created")}",
        { BillingType: ShipmentBillingType.OwnAccount } r => $"Printavo: {(string.IsNullOrWhiteSpace(r.PrintavoTaskId) ? "task not created" : "task created")}; {(r.CustomerNoteAppended ? "customer note updated" : "customer note not updated")}",
        var r => $"Printavo: {(r.CustomerNoteAppended ? "customer note updated" : "customer note not updated")}"
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

    private bool CanGetRates() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanGetRates))]
    private async Task GetRatesAsync()
    {
        if (SelectedOwnAccount is null)
        {
            ErrorMessage = NoOwnAccountMessage;
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        RateQuotes = new ObservableCollection<FedExRateQuote>();
        AppliedRateQuote = null;
        // Keyed before the request goes out, so an edit made while it's in
        // flight leaves the returned quotes correctly marked stale.
        _ratedKey = CurrentRateKey();
        try
        {
            var request = BuildRequest();
            var quotes = await _fedExClient.ShopRatesAsync(request);
            RateQuotes = new ObservableCollection<FedExRateQuote>(quotes.OrderBy(q => q.TotalCost));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't get rates: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private FedExShipmentRequest BuildRequest()
    {
        var (_, billingNumber, billingZip, billingCountry) = BillThirdParty && CanResolveBillingAccount()
            ? ResolveBillingAccount()
            : (string.Empty, string.Empty, string.Empty, string.Empty);

        return new(
            _rateOrigin,
            _shipTo,
            ShipperAccountNumber: SelectedOwnAccount?.AccountNumber,
            BillThirdParty ? FedExBillingType.ThirdParty : FedExBillingType.OwnAccount,
            BillThirdParty ? billingNumber : null,
            BillThirdParty ? billingZip : null,
            BillThirdParty ? billingCountry : null,
            SelectedServiceLevel.Code,
            SelectedServiceLevel.Name,
            Packages.Select(p => new FedExPackageInput(p.WeightLbs, p.LengthIn, p.WidthIn, p.HeightIn)).ToList());
    }

    [ObservableProperty]
    private FedExRateQuote? appliedRateQuote;

    [RelayCommand]
    private void ApplyRateQuote(FedExRateQuote quote)
    {
        var serviceMatch = ServiceLevels.FirstOrDefault(s => s.Code == quote.ServiceType);
        if (serviceMatch is not null)
        {
            SelectedServiceLevel = serviceMatch;
        }

        AppliedRateQuote = quote;
    }

    /// <summary>
    /// Checks the ship-to against FedEx's Address Validation API and applies
    /// the residential/commercial classification to _shipTo. Mirrors
    /// ShipmentCardViewModel.ValidateAddressAsync — same "not yet confirmed
    /// live" caveat as the rest of the FedEx client applies here too.
    /// </summary>
    private bool CanValidateAddress() => !IsValidatingAddress;

    [RelayCommand(CanExecute = nameof(CanValidateAddress))]
    private async Task ValidateAddressAsync()
    {
        // _shipTo is a plain field — re-check rate currency for the caller's
        // edit now, and for any classification applied below in finally.
        RefreshShipReadiness();
        IsValidatingAddress = true;
        AddressValidationStatus = "Validating address...";
        AddressValidationHasWarning = false;
        try
        {
            var result = await _fedExClient.ValidateAddressAsync(_shipTo);

            if (result.Classification == FedExAddressClassification.Residential)
            {
                _shipTo = _shipTo with { IsResidential = true };
            }
            else if (result.Classification == FedExAddressClassification.Commercial)
            {
                _shipTo = _shipTo with { IsResidential = false };
            }

            if (result.IsExactMatch)
            {
                AddressValidationStatus = result.Classification switch
                {
                    FedExAddressClassification.Residential => "FedEx verified this address as residential (a residential surcharge may apply).",
                    FedExAddressClassification.Commercial => "FedEx verified this address as commercial.",
                    _ => "FedEx verified this address but couldn't classify it as residential or commercial."
                };
            }
            else if (result.IsAmbiguous && result.Candidates.Count > 0)
            {
                var c = result.Candidates[0];
                AddressValidationStatus = $"FedEx couldn't match this exactly — closest suggestion: {string.Join(" ", c.AddressLines)}, {c.City}, {c.State} {c.PostalCode}.";
                AddressValidationHasWarning = true;
            }
            else if (result.HasNoCandidates)
            {
                AddressValidationStatus = "FedEx returned no match for this address. Double-check it before shipping.";
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
        _shipTo = new FedExPartyInput(
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
        ShipToSummary = $"{_shipTo.Name}\n{_shipTo.Address1} {_shipTo.Address2}\n{_shipTo.City}, {_shipTo.State} {_shipTo.PostalCode}".Replace("  ", " ");

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
            // FedEx shipments never add an invoice fee, so there's no fee-removal warning to show.
            (Result, _) = await _workflowService.VoidShipmentAsync(Result);
            OnPropertyChanged(nameof(Result));
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

    partial void OnSelectedOwnAccountChanged(OwnFedExAccount? value) => ShipCommand.NotifyCanExecuteChanged();

    partial void OnBillThirdPartyChanged(bool value) => ShipCommand.NotifyCanExecuteChanged();

    /// <summary>Picking the "+ Enter a different account" sentinel from the dropdown is how UseNewBillingAccount gets turned on.</summary>
    partial void OnSelectedBillingAccountChanged(ContractorFedExBillingAccount? value)
    {
        UseNewBillingAccount = ReferenceEquals(value, NewBillingAccountSentinel);
        ShipCommand.NotifyCanExecuteChanged();
    }

    partial void OnBillingAccountOptionsChanged(ObservableCollection<ContractorFedExBillingAccount> value)
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
