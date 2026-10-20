using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrintavoShipper.App.Services;
using PrintavoShipper.Core.Interfaces;
using PrintavoShipper.Core.Models;
using PrintavoShipper.Printavo;
using PrintavoShipper.Printavo.Dto;
using PrintavoShipper.Ups;

namespace PrintavoShipper.App.ViewModels;

/// <summary>
/// Phase 2: full local CRUD for contractor ship-from addresses and
/// third-party billing accounts, plus importing a contractor from Printavo's
/// customer list before it's ever been seen on an opened order.
/// </summary>
public partial class ContractorsViewModel : ObservableObject
{
    private readonly IContractorRepository _contractorRepository;
    private readonly IPrintavoClient _printavoClient;
    private readonly IContractorProvisioningService _contractorProvisioningService;
    private readonly IUpsClient _upsClient;
    private readonly ISettingsService _settingsService;
    private readonly IUpsAccountRepository _upsAccountRepository;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private ObservableCollection<Contractor> contractors = new();

    [ObservableProperty]
    private Contractor? selectedContractor;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchPrintavoCustomersCommand))]
    private bool isLoading;

    [ObservableProperty]
    private string? statusMessage;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private bool showArchived;

    // New ship-from address form
    [ObservableProperty] private string newShipFromLabel = string.Empty;
    [ObservableProperty] private string newShipFromAttentionName = string.Empty;
    [ObservableProperty] private string newShipFromCompanyName = string.Empty;
    [ObservableProperty] private string newShipFromAddress1 = string.Empty;
    [ObservableProperty] private string newShipFromAddress2 = string.Empty;
    [ObservableProperty] private string newShipFromCity = string.Empty;
    [ObservableProperty] private string newShipFromState = string.Empty;
    [ObservableProperty] private string newShipFromPostalCode = string.Empty;
    [ObservableProperty] private string newShipFromCountryCode = "US";
    [ObservableProperty] private string newShipFromPhoneNumber = string.Empty;
    [ObservableProperty] private bool newShipFromIsDefault;

    // New billing account form
    [ObservableProperty] private string newBillingLabel = string.Empty;
    [ObservableProperty] private string newBillingAccountNumber = string.Empty;
    [ObservableProperty] private string newBillingPostalCode = string.Empty;
    [ObservableProperty] private string newBillingCountryCode = "US";
    [ObservableProperty] private bool newBillingIsDefault;
    [ObservableProperty] private string newBillingNotes = string.Empty;
    [ObservableProperty] private string? billingValidationStatus;
    [ObservableProperty] private bool billingValidationHasWarning;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ValidateNewBillingAccountCommand))]
    private bool isValidatingBillingAccount;

    // New FedEx billing account form
    [ObservableProperty] private string newFedExBillingLabel = string.Empty;
    [ObservableProperty] private string newFedExBillingAccountNumber = string.Empty;
    [ObservableProperty] private string newFedExBillingCountryCode = "US";
    [ObservableProperty] private bool newFedExBillingIsDefault;
    [ObservableProperty] private string newFedExBillingNotes = string.Empty;

    // Import from Printavo
    [ObservableProperty] private string importSearchText = string.Empty;
    [ObservableProperty] private ObservableCollection<PrintavoCustomerSummary> importCandidates = new();
    [ObservableProperty] private bool isImportSearchLoaded;

    /// <summary>
    /// Printavo's full customer list, kept for this (session-long) view
    /// model so changing the search text just re-filters locally instead of
    /// re-paging the whole list from Printavo (dozens of rate-limited
    /// requests) on every Search click. Refreshed when older than
    /// PrintavoCustomersCacheLifetime, or when a search finds no match — so
    /// a customer added in Printavo minutes ago can still be found.
    /// </summary>
    private IReadOnlyList<PrintavoCustomerSummary>? _printavoCustomersCache;
    private DateTimeOffset _printavoCustomersFetchedAt;
    private static readonly TimeSpan PrintavoCustomersCacheLifetime = TimeSpan.FromMinutes(10);

    public ContractorsViewModel(
        IContractorRepository contractorRepository,
        IPrintavoClient printavoClient,
        IContractorProvisioningService contractorProvisioningService,
        IUpsClient upsClient,
        ISettingsService settingsService,
        IUpsAccountRepository upsAccountRepository,
        IDialogService dialogService)
    {
        _contractorRepository = contractorRepository;
        _printavoClient = printavoClient;
        _contractorProvisioningService = contractorProvisioningService;
        _upsClient = upsClient;
        _settingsService = settingsService;
        _upsAccountRepository = upsAccountRepository;
        _dialogService = dialogService;
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var all = await _contractorRepository.GetAllAsync(ShowArchived);
            Contractors = new ObservableCollection<Contractor>(all);
            if (SelectedContractor is not null)
            {
                // Only swap the reference if the contractor is still in the
                // reloaded (filtered-by-ShowArchived) set — if it just got
                // archived while "Show archived" is off, for example, leave
                // the previously-selected object as-is instead of nulling it
                // out, so the detail panel doesn't instantly vanish out from
                // under the user right after the action that caused this.
                var stillPresent = Contractors.FirstOrDefault(c => c.Id == SelectedContractor.Id);
                if (stillPresent is not null)
                {
                    SelectedContractor = stillPresent;
                }
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnShowArchivedChanged(bool value) => _ = LoadAsync();

    // Editing any of these after a Validate click means the last validation
    // result no longer describes what's currently in the form — clear it so
    // a stale "valid" confirmation can't be mistaken for having validated
    // the edited number.
    partial void OnNewBillingAccountNumberChanged(string value) => ClearBillingValidation();
    partial void OnNewBillingPostalCodeChanged(string value) => ClearBillingValidation();
    partial void OnNewBillingCountryCodeChanged(string value) => ClearBillingValidation();

    private void ClearBillingValidation()
    {
        BillingValidationStatus = null;
        BillingValidationHasWarning = false;
    }

    public void SelectContractorById(string contractorId)
    {
        SelectedContractor = Contractors.FirstOrDefault(c => c.Id == contractorId);
    }

    partial void OnSelectedContractorChanged(Contractor? value)
    {
        StatusMessage = null;
        ErrorMessage = null;
    }

    [RelayCommand]
    private async Task AddShipFromAddressAsync()
    {
        if (SelectedContractor is null || string.IsNullOrWhiteSpace(NewShipFromAddress1))
        {
            ErrorMessage = "Select a contractor and enter at least an address line first.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewShipFromCity) || string.IsNullOrWhiteSpace(NewShipFromState) || string.IsNullOrWhiteSpace(NewShipFromPostalCode))
        {
            ErrorMessage = "Enter address, city, state, and ZIP before saving.";
            return;
        }

        ErrorMessage = null;

        var saved = await _contractorRepository.SaveShipFromAddressAsync(new ContractorShipFromAddress
        {
            ContractorId = SelectedContractor.Id,
            Label = string.IsNullOrWhiteSpace(NewShipFromLabel) ? "Ship From" : NewShipFromLabel,
            AttentionName = NewShipFromAttentionName,
            CompanyName = NewShipFromCompanyName,
            Address1 = NewShipFromAddress1,
            Address2 = NewShipFromAddress2,
            City = NewShipFromCity,
            State = NewShipFromState,
            PostalCode = NewShipFromPostalCode,
            CountryCode = string.IsNullOrWhiteSpace(NewShipFromCountryCode) ? "US" : NewShipFromCountryCode,
            PhoneNumber = NewShipFromPhoneNumber,
            IsDefault = NewShipFromIsDefault
        });

        NewShipFromLabel = NewShipFromAttentionName = NewShipFromCompanyName = string.Empty;
        NewShipFromAddress1 = NewShipFromAddress2 = NewShipFromCity = NewShipFromState = NewShipFromPostalCode = NewShipFromPhoneNumber = string.Empty;
        NewShipFromCountryCode = "US";
        NewShipFromIsDefault = false;

        // Mutate SelectedContractor in place (it's the same object instance
        // still referenced by the Contractors list) rather than reloading
        // and replacing it — see the note on Contractor for why a full
        // reload+reassignment silently no-ops against WPF's SelectedItem
        // tracking and [ObservableProperty]'s equality-based change skip.
        if (saved.IsDefault)
        {
            foreach (var other in SelectedContractor.ShipFromAddresses)
            {
                other.IsDefault = false;
            }
        }

        SelectedContractor.ShipFromAddresses.Add(saved);
        StatusMessage = "Ship-from address saved.";
    }

    [RelayCommand]
    private async Task DeleteShipFromAddressAsync(ContractorShipFromAddress? address)
    {
        if (address is null || SelectedContractor is null)
        {
            return;
        }

        if (!await _dialogService.ConfirmAsync("Delete Ship-From Address", "Delete this ship-from address?"))
        {
            return;
        }

        await _contractorRepository.DeleteShipFromAddressAsync(address.Id);

        // Re-fetch rather than just removing locally: the repository may
        // have promoted a new default when the deleted one was it, and the
        // UI needs to reflect that. Same in-place-rebuild technique as
        // SetDefaultBillingAccountAsync, for the same SelectedItem-reset
        // reason documented there.
        var refreshed = await _contractorRepository.GetByIdAsync(SelectedContractor.Id);
        if (refreshed is not null)
        {
            SelectedContractor.ShipFromAddresses.Clear();
            foreach (var a in refreshed.ShipFromAddresses)
            {
                SelectedContractor.ShipFromAddresses.Add(a);
            }
        }
    }

    [RelayCommand]
    private async Task AddBillingAccountAsync()
    {
        if (SelectedContractor is null || string.IsNullOrWhiteSpace(NewBillingAccountNumber))
        {
            ErrorMessage = "Select a contractor and enter a UPS account number first.";
            return;
        }

        ErrorMessage = null;

        var saved = await _contractorRepository.SaveBillingAccountAsync(new ContractorBillingAccount
        {
            ContractorId = SelectedContractor.Id,
            Label = string.IsNullOrWhiteSpace(NewBillingLabel) ? "Third-Party Billing" : NewBillingLabel,
            UpsAccountNumber = NewBillingAccountNumber,
            UpsAccountPostalCode = NewBillingPostalCode,
            UpsAccountCountryCode = string.IsNullOrWhiteSpace(NewBillingCountryCode) ? "US" : NewBillingCountryCode,
            IsDefault = NewBillingIsDefault,
            Notes = NewBillingNotes
        });

        NewBillingLabel = NewBillingAccountNumber = NewBillingPostalCode = NewBillingNotes = string.Empty;
        NewBillingCountryCode = "US";
        NewBillingIsDefault = false;

        if (saved.IsDefault)
        {
            foreach (var other in SelectedContractor.BillingAccounts)
            {
                other.IsDefault = false;
            }
        }

        SelectedContractor.BillingAccounts.Add(saved);
        StatusMessage = "Billing account saved.";
    }

    /// <summary>
    /// There's no live order here, so the dry run uses the shop's own
    /// default ship-from address as both ends of a self-addressed rate
    /// query — it just needs to be internally consistent, not a real
    /// destination, since the account+zip pairing is what's actually under
    /// test (UPS validates that against its own file as a side effect of
    /// any BillThirdParty rate/ship request).
    /// </summary>
    private bool CanValidateNewBillingAccount() => !IsValidatingBillingAccount;

    [RelayCommand(CanExecute = nameof(CanValidateNewBillingAccount))]
    private async Task ValidateNewBillingAccountAsync()
    {
        if (string.IsNullOrWhiteSpace(NewBillingAccountNumber))
        {
            BillingValidationStatus = "Enter a UPS account number first.";
            BillingValidationHasWarning = true;
            return;
        }

        IsValidatingBillingAccount = true;
        BillingValidationStatus = "Validating with UPS...";
        BillingValidationHasWarning = false;
        try
        {
            var ownAccounts = await _upsAccountRepository.GetAllAsync();
            var ownAccount = ownAccounts.FirstOrDefault(a => a.IsDefault) ?? ownAccounts.FirstOrDefault();
            if (ownAccount is null)
            {
                BillingValidationStatus = "Can't validate — configure a UPS account in Settings first.";
                BillingValidationHasWarning = true;
                return;
            }

            var settings = await _settingsService.GetSettingsAsync();
            var shopAddress = new UpsPartyInput(
                settings.DefaultShipFromCompanyName,
                settings.DefaultShipFromAttentionName,
                settings.DefaultShipFromAddress1,
                settings.DefaultShipFromAddress2,
                settings.DefaultShipFromCity,
                settings.DefaultShipFromState,
                settings.DefaultShipFromPostalCode,
                settings.DefaultShipFromCountryCode,
                settings.DefaultShipFromPhoneNumber);

            var result = await _upsClient.ValidateThirdPartyAccountAsync(
                shopAddress,
                shopAddress,
                ownAccount.AccountNumber,
                NewBillingAccountNumber,
                NewBillingPostalCode,
                string.IsNullOrWhiteSpace(NewBillingCountryCode) ? "US" : NewBillingCountryCode);

            BillingValidationStatus = result.Message;
            BillingValidationHasWarning = !result.IsValid;
        }
        catch (Exception ex)
        {
            BillingValidationStatus = $"Couldn't validate account: {ex.Message}";
            BillingValidationHasWarning = true;
        }
        finally
        {
            IsValidatingBillingAccount = false;
        }
    }

    [RelayCommand]
    private async Task DeleteBillingAccountAsync(ContractorBillingAccount? account)
    {
        if (account is null || SelectedContractor is null)
        {
            return;
        }

        if (!await _dialogService.ConfirmAsync("Delete Billing Account", "Delete this billing account?"))
        {
            return;
        }

        await _contractorRepository.DeleteBillingAccountAsync(account.Id);

        // Same reasoning as DeleteShipFromAddressAsync above: re-fetch so a
        // promoted default is reflected, using the same proven in-place
        // rebuild technique as SetDefaultBillingAccountAsync.
        var refreshed = await _contractorRepository.GetByIdAsync(SelectedContractor.Id);
        if (refreshed is not null)
        {
            SelectedContractor.BillingAccounts.Clear();
            foreach (var a in refreshed.BillingAccounts)
            {
                SelectedContractor.BillingAccounts.Add(a);
            }
        }
    }

    /// <summary>
    /// Changes which already-saved billing account is the default (the
    /// "Default" checkbox on the add form only sets it at creation time).
    /// Unlike Contractor, ContractorBillingAccount doesn't implement
    /// INotifyPropertyChanged, so flipping IsDefault on the other
    /// now-non-default accounts wouldn't refresh their "(default)" label —
    /// re-fetching just this one contractor and rebuilding
    /// SelectedContractor.BillingAccounts in place (Clear + re-Add, so the
    /// bound ItemsControl regenerates every card via CollectionChanged)
    /// forces that refresh without ever reassigning Contractors or
    /// SelectedContractor themselves. A full LoadAsync() reload was tried
    /// first, but replacing Contractors with freshly-loaded objects resets
    /// the DataGrid's SelectedItem — the DataGrid's own selection-reset (on
    /// the new ItemsSource) races the reselect-by-Id that follows and wins,
    /// so the contractor detail panel closed out from under the user.
    /// </summary>
    [RelayCommand]
    private async Task SetDefaultBillingAccountAsync(ContractorBillingAccount? account)
    {
        if (account is null || SelectedContractor is null || account.IsDefault)
        {
            return;
        }

        account.IsDefault = true;
        await _contractorRepository.SaveBillingAccountAsync(account);

        var refreshed = await _contractorRepository.GetByIdAsync(SelectedContractor.Id);
        if (refreshed is not null)
        {
            SelectedContractor.BillingAccounts.Clear();
            foreach (var a in refreshed.BillingAccounts)
            {
                SelectedContractor.BillingAccounts.Add(a);
            }
        }

        ErrorMessage = null;
        StatusMessage = $"{(string.IsNullOrWhiteSpace(account.Label) ? "Account" : account.Label)} set as default.";
    }

    [RelayCommand]
    private async Task AddFedExBillingAccountAsync()
    {
        if (SelectedContractor is null || string.IsNullOrWhiteSpace(NewFedExBillingAccountNumber))
        {
            ErrorMessage = "Select a contractor and enter a FedEx account number first.";
            return;
        }

        ErrorMessage = null;

        var saved = await _contractorRepository.SaveFedExBillingAccountAsync(new ContractorFedExBillingAccount
        {
            ContractorId = SelectedContractor.Id,
            Label = string.IsNullOrWhiteSpace(NewFedExBillingLabel) ? "Third-Party Billing" : NewFedExBillingLabel,
            AccountNumber = NewFedExBillingAccountNumber,
            AccountPostalCode = string.Empty,
            AccountCountryCode = string.IsNullOrWhiteSpace(NewFedExBillingCountryCode) ? "US" : NewFedExBillingCountryCode,
            IsDefault = NewFedExBillingIsDefault,
            Notes = NewFedExBillingNotes
        });

        NewFedExBillingLabel = NewFedExBillingAccountNumber = NewFedExBillingNotes = string.Empty;
        NewFedExBillingCountryCode = "US";
        NewFedExBillingIsDefault = false;

        if (saved.IsDefault)
        {
            foreach (var other in SelectedContractor.FedExBillingAccounts)
            {
                other.IsDefault = false;
            }
        }

        SelectedContractor.FedExBillingAccounts.Add(saved);
        StatusMessage = "FedEx billing account saved.";
    }

    [RelayCommand]
    private async Task DeleteFedExBillingAccountAsync(ContractorFedExBillingAccount? account)
    {
        if (account is null || SelectedContractor is null)
        {
            return;
        }

        if (!await _dialogService.ConfirmAsync("Delete FedEx Billing Account", "Delete this FedEx billing account?"))
        {
            return;
        }

        await _contractorRepository.DeleteFedExBillingAccountAsync(account.Id);

        var refreshed = await _contractorRepository.GetByIdAsync(SelectedContractor.Id);
        if (refreshed is not null)
        {
            SelectedContractor.FedExBillingAccounts.Clear();
            foreach (var a in refreshed.FedExBillingAccounts)
            {
                SelectedContractor.FedExBillingAccounts.Add(a);
            }
        }
    }

    [RelayCommand]
    private async Task SetDefaultFedExBillingAccountAsync(ContractorFedExBillingAccount? account)
    {
        if (account is null || SelectedContractor is null || account.IsDefault)
        {
            return;
        }

        account.IsDefault = true;
        await _contractorRepository.SaveFedExBillingAccountAsync(account);

        var refreshed = await _contractorRepository.GetByIdAsync(SelectedContractor.Id);
        if (refreshed is not null)
        {
            SelectedContractor.FedExBillingAccounts.Clear();
            foreach (var a in refreshed.FedExBillingAccounts)
            {
                SelectedContractor.FedExBillingAccounts.Add(a);
            }
        }

        ErrorMessage = null;
        StatusMessage = $"{(string.IsNullOrWhiteSpace(account.Label) ? "Account" : account.Label)} set as default.";
    }

    [RelayCommand]
    private async Task ArchiveSelectedAsync()
    {
        if (SelectedContractor is null)
        {
            return;
        }

        var name = SelectedContractor.CompanyName;
        await _contractorRepository.SetArchivedAsync(SelectedContractor.Id, isArchived: true);
        // Set directly rather than waiting on LoadAsync's reload: if "Show
        // archived" is off, LoadAsync's refreshed Contractors won't include
        // this contractor at all, and per LoadAsync's own reselect logic
        // that means SelectedContractor keeps pointing at this exact object
        // — which needs its own IsArchived flag (and the "Archived" banner/
        // Archive-vs-Unarchive button bound to it) to reflect reality
        // immediately, not just whatever LoadAsync happens to reload.
        SelectedContractor.IsArchived = true;
        await LoadAsync();
        ErrorMessage = null;
        StatusMessage = $"Archived {name}. Check \"Show archived\" to find it again.";
    }

    [RelayCommand]
    private async Task UnarchiveSelectedAsync()
    {
        if (SelectedContractor is null)
        {
            return;
        }

        var name = SelectedContractor.CompanyName;
        await _contractorRepository.SetArchivedAsync(SelectedContractor.Id, isArchived: false);
        SelectedContractor.IsArchived = false;
        await LoadAsync();
        ErrorMessage = null;
        StatusMessage = $"Unarchived {name}.";
    }

    private bool CanSearchPrintavoCustomers() => !IsLoading;

    [RelayCommand(CanExecute = nameof(CanSearchPrintavoCustomers))]
    private async Task SearchPrintavoCustomersAsync()
    {
        IsLoading = true;
        StatusMessage = null;
        ErrorMessage = null;
        try
        {
            var cacheIsFresh = _printavoCustomersCache is not null
                && DateTimeOffset.UtcNow - _printavoCustomersFetchedAt < PrintavoCustomersCacheLifetime;
            var filtered = cacheIsFresh ? FilterPrintavoCustomers(_printavoCustomersCache!) : [];

            if (filtered.Count == 0)
            {
                _printavoCustomersCache = await _printavoClient.ListCustomersAsync();
                _printavoCustomersFetchedAt = DateTimeOffset.UtcNow;
                filtered = FilterPrintavoCustomers(_printavoCustomersCache);
            }

            IsImportSearchLoaded = true;

            // Exclude every locally-known contractor, archived or not — an
            // archived contractor still exists (just hidden), so it must
            // never show up as an "importable new" candidate. Fetched
            // separately from `Contractors` since that list respects
            // ShowArchived and may be filtered to active-only right now.
            var alreadyKnown = await _contractorRepository.GetAllIdsAsync();
            ImportCandidates = new ObservableCollection<PrintavoCustomerSummary>(
                filtered.Where(c => !alreadyKnown.Contains(c.Id)).OrderBy(c => c.CompanyName));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't load Printavo customers: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private List<PrintavoCustomerSummary> FilterPrintavoCustomers(IReadOnlyList<PrintavoCustomerSummary> all) =>
        string.IsNullOrWhiteSpace(ImportSearchText)
            ? all.ToList()
            : all.Where(c => c.CompanyName.Contains(ImportSearchText, StringComparison.OrdinalIgnoreCase)).ToList();

    [RelayCommand]
    private async Task ImportContractorAsync(PrintavoCustomerSummary? customer)
    {
        if (customer is null)
        {
            return;
        }

        var contractor = await _contractorProvisioningService.ImportFromPrintavoAsync(customer.Id, customer.CompanyName);
        ImportCandidates.Remove(customer);
        await LoadAsync();
        SelectedContractor = Contractors.FirstOrDefault(c => c.Id == contractor.Id);
        ErrorMessage = null;
        StatusMessage = contractor.ShipFromAddresses.Count > 0
            ? $"Added {customer.CompanyName} with a ship-from address from Printavo — review below."
            : $"Added {customer.CompanyName}. No Printavo address on file — enter one below.";
    }
}
