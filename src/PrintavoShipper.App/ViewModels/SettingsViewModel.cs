using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrintavoShipper.App.Services;
using PrintavoShipper.Core.Data;
using PrintavoShipper.Core.Interfaces;
using PrintavoShipper.Core.Models;
using PrintavoShipper.FedEx;
using PrintavoShipper.Printavo;
using PrintavoShipper.Printavo.Dto;
using PrintavoShipper.Ups;

namespace PrintavoShipper.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IPrintavoClient _printavoClient;
    private readonly IUpsAuthService _upsAuthService;
    private readonly IFedExAuthService _fedExAuthService;
    private readonly ILabelPrintService _labelPrintService;
    private readonly IUpsAccountRepository _upsAccountRepository;
    private readonly IFedExAccountRepository _fedExAccountRepository;
    private readonly IShipmentArchiveService _shipmentArchiveService;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;

    private AppSettings _settings = new();

    [ObservableProperty]
    private string? printavoEmail;

    [ObservableProperty]
    private string printavoApiTokenInput = string.Empty;

    [ObservableProperty]
    private bool isPrintavoTokenConfigured;

    [ObservableProperty]
    private string printavoCurrentUserName = string.Empty;

    [ObservableProperty]
    private string upsClientIdInput = string.Empty;

    [ObservableProperty]
    private bool isUpsClientIdConfigured;

    [ObservableProperty]
    private string upsClientSecretInput = string.Empty;

    [ObservableProperty]
    private bool isUpsClientSecretConfigured;

    [ObservableProperty]
    private UpsEnvironment upsEnvironment;

    [ObservableProperty]
    private string? upsMerchantId;

    [ObservableProperty]
    private ObservableCollection<OwnUpsAccount> ownUpsAccounts = new();

    [ObservableProperty]
    private string newOwnAccountLabel = string.Empty;

    [ObservableProperty]
    private string newOwnAccountNumber = string.Empty;

    [ObservableProperty]
    private string newOwnAccountPostalCode = string.Empty;

    [ObservableProperty]
    private string newOwnAccountCountryCode = "US";

    [ObservableProperty]
    private bool newOwnAccountIsDefault;

    [ObservableProperty]
    private string fedExClientIdInput = string.Empty;

    [ObservableProperty]
    private bool isFedExClientIdConfigured;

    [ObservableProperty]
    private string fedExClientSecretInput = string.Empty;

    [ObservableProperty]
    private bool isFedExClientSecretConfigured;

    [ObservableProperty]
    private FedExEnvironment fedExEnvironment;

    [ObservableProperty]
    private string? fedExAccountNumber;

    [ObservableProperty]
    private ObservableCollection<OwnFedExAccount> ownFedExAccounts = new();

    [ObservableProperty]
    private string newOwnFedExAccountLabel = string.Empty;

    [ObservableProperty]
    private string newOwnFedExAccountNumber = string.Empty;

    [ObservableProperty]
    private string newOwnFedExAccountPostalCode = string.Empty;

    [ObservableProperty]
    private string newOwnFedExAccountCountryCode = "US";

    [ObservableProperty]
    private bool newOwnFedExAccountIsDefault;

    [ObservableProperty]
    private string defaultShipFromCompanyName = string.Empty;

    [ObservableProperty]
    private string? defaultShipFromAttentionName;

    [ObservableProperty]
    private string defaultShipFromAddress1 = string.Empty;

    [ObservableProperty]
    private string? defaultShipFromAddress2;

    [ObservableProperty]
    private string defaultShipFromCity = string.Empty;

    [ObservableProperty]
    private string defaultShipFromState = string.Empty;

    [ObservableProperty]
    private string defaultShipFromPostalCode = string.Empty;

    [ObservableProperty]
    private string defaultShipFromCountryCode = "US";

    [ObservableProperty]
    private string? defaultShipFromPhoneNumber;

    [ObservableProperty]
    private ObservableCollection<string> availablePrinters = new();

    [ObservableProperty]
    private string? selectedPrinterName;

    [ObservableProperty]
    private LabelPaperMode labelPaperMode;

    [ObservableProperty]
    private int shipmentArchiveIntervalWeeks = 2;

    /// <summary>The shop logo for Pick-up box labels (raw file bytes) — staged here, persisted on Save like every other field.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveShopLogoCommand))]
    private byte[]? shopLogoImage;

    [ObservableProperty]
    private ImageSource? shopLogoPreview;

    // ---- Printavo Workflow ----

    [ObservableProperty]
    private ObservableCollection<StatusSettingRow> statusRows = new();

    [ObservableProperty]
    private ObservableCollection<UserSettingRow> userRows = new();

    /// <summary>Choices for every Printavo user picker on this screen — "(none)" plus the account's users.</summary>
    [ObservableProperty]
    private IReadOnlyList<PrintavoUserChoice> userChoices = new List<PrintavoUserChoice> { PrintavoUserChoice.None };

    [ObservableProperty]
    private PrintavoUserChoice? shippingCostTaskAssignee = PrintavoUserChoice.None;

    [ObservableProperty]
    private string printavoListsMessage = string.Empty;

    /// <summary>Comma-separated Printavo delivery method names.</summary>
    [ObservableProperty]
    private string upsDeliveryMethodNames = string.Empty;

    [ObservableProperty]
    private string fedExDeliveryMethodNames = string.Empty;

    [ObservableProperty]
    private string pickupDeliveryMethodNames = string.Empty;

    [ObservableProperty]
    private ShipmentCarrier defaultCarrier;

    // ---- Shipping Defaults ----

    [ObservableProperty]
    private ObservableCollection<BoxPresetRow> boxPresets = new();

    [ObservableProperty]
    private double shippingFeeMarkupPercent;

    /// <summary>The account's statuses/users as last fetched from Printavo — null until a fetch succeeds, in which case the rows show only what's already saved.</summary>
    private IReadOnlyList<PrintavoOrderStatusOption>? _printavoStatuses;
    private IReadOnlyList<PrintavoUser>? _printavoUsers;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyCanExecuteChangedFor(nameof(TestPrintavoConnectionCommand))]
    [NotifyCanExecuteChangedFor(nameof(TestUpsConnectionCommand))]
    [NotifyCanExecuteChangedFor(nameof(TestFedExConnectionCommand))]
    [NotifyCanExecuteChangedFor(nameof(PrintTestLabelCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddOwnUpsAccountCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddOwnFedExAccountCommand))]
    [NotifyCanExecuteChangedFor(nameof(RunArchiveNowCommand))]
    [NotifyCanExecuteChangedFor(nameof(ReloadPrintavoListsCommand))]
    private bool isBusy;

    public IReadOnlyList<UpsEnvironment> UpsEnvironments { get; } = Enum.GetValues<UpsEnvironment>();
    public IReadOnlyList<FedExEnvironment> FedExEnvironments { get; } = Enum.GetValues<FedExEnvironment>();
    public IReadOnlyList<LabelPaperMode> LabelPaperModes { get; } = Enum.GetValues<LabelPaperMode>();

    public SettingsViewModel(
        ISettingsService settingsService,
        IPrintavoClient printavoClient,
        IUpsAuthService upsAuthService,
        IFedExAuthService fedExAuthService,
        ILabelPrintService labelPrintService,
        IUpsAccountRepository upsAccountRepository,
        IFedExAccountRepository fedExAccountRepository,
        IShipmentArchiveService shipmentArchiveService,
        INavigationService navigationService,
        IDialogService dialogService)
    {
        _settingsService = settingsService;
        _printavoClient = printavoClient;
        _upsAuthService = upsAuthService;
        _fedExAuthService = fedExAuthService;
        _labelPrintService = labelPrintService;
        _upsAccountRepository = upsAccountRepository;
        _fedExAccountRepository = fedExAccountRepository;
        _shipmentArchiveService = shipmentArchiveService;
        _navigationService = navigationService;
        _dialogService = dialogService;

        AvailablePrinters = new ObservableCollection<string>(_labelPrintService.GetInstalledPrinterNames());

        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        _settings = await _settingsService.GetSettingsAsync();

        PrintavoEmail = _settings.PrintavoEmail;
        IsPrintavoTokenConfigured = _settings.PrintavoApiTokenProtected is { Length: > 0 };
        PrintavoCurrentUserName = _settings.PrintavoCurrentUserName ?? string.Empty;

        IsUpsClientIdConfigured = _settings.UpsClientIdProtected is { Length: > 0 };
        IsUpsClientSecretConfigured = _settings.UpsClientSecretProtected is { Length: > 0 };
        UpsEnvironment = _settings.UpsEnvironment;
        UpsMerchantId = _settings.UpsMerchantId;

        IsFedExClientIdConfigured = _settings.FedExClientIdProtected is { Length: > 0 };
        IsFedExClientSecretConfigured = _settings.FedExClientSecretProtected is { Length: > 0 };
        FedExEnvironment = _settings.FedExEnvironment;
        FedExAccountNumber = _settings.FedExAccountNumber;

        SelectedPrinterName = _settings.SelectedPrinterName;
        LabelPaperMode = _settings.LabelPaperMode;
        ShipmentArchiveIntervalWeeks = _settings.ShipmentArchiveIntervalWeeks;
        ShopLogoImage = _settings.ShopLogoImage;

        DefaultShipFromCompanyName = _settings.DefaultShipFromCompanyName;
        DefaultShipFromAttentionName = _settings.DefaultShipFromAttentionName;
        DefaultShipFromAddress1 = _settings.DefaultShipFromAddress1;
        DefaultShipFromAddress2 = _settings.DefaultShipFromAddress2;
        DefaultShipFromCity = _settings.DefaultShipFromCity;
        DefaultShipFromState = _settings.DefaultShipFromState;
        DefaultShipFromPostalCode = _settings.DefaultShipFromPostalCode;
        DefaultShipFromCountryCode = _settings.DefaultShipFromCountryCode;
        DefaultShipFromPhoneNumber = _settings.DefaultShipFromPhoneNumber;

        UpsDeliveryMethodNames = string.Join(", ", _settings.UpsDeliveryMethodNames);
        FedExDeliveryMethodNames = string.Join(", ", _settings.FedExDeliveryMethodNames);
        PickupDeliveryMethodNames = string.Join(", ", _settings.PickupDeliveryMethodNames);
        DefaultCarrier = _settings.DefaultCarrier;
        BoxPresets = new ObservableCollection<BoxPresetRow>(_settings.BoxSizePresets.Select(p => new BoxPresetRow
        {
            Name = p.Name,
            LengthIn = p.LengthIn,
            WidthIn = p.WidthIn,
            HeightIn = p.HeightIn,
            WeightLbs = p.WeightLbs
        }));
        ShippingFeeMarkupPercent = (double)_settings.ShippingFeeMarkupPercent;
        BuildWorkflowRows(SavedWorkflowState());

        OwnUpsAccounts = new ObservableCollection<OwnUpsAccount>(await _upsAccountRepository.GetAllAsync());
        OwnFedExAccounts = new ObservableCollection<OwnFedExAccount>(await _fedExAccountRepository.GetAllAsync());

        // First visit only — later reloads (e.g. after Save) reuse the lists.
        if (_printavoStatuses is null && await TryLoadPrintavoListsAsync())
        {
            BuildWorkflowRows(CurrentWorkflowState());
        }
    }

    /// <summary>The Printavo Workflow picks, as saved or as currently edited on screen.</summary>
    private sealed record WorkflowState(
        List<ConfiguredPrintavoStatus> ShippingStatuses,
        List<ConfiguredPrintavoStatus> PickupStatuses,
        List<ConfiguredPrintavoStatus> HiddenOrderStatuses,
        List<StaffMember> StaffMembers,
        string? ShippingCostTaskAssigneeUserId,
        bool ShippingCostTaskToOrderOwner);

    private WorkflowState SavedWorkflowState() => new(
        _settings.ShippingStatuses,
        _settings.PickupStatuses,
        _settings.HiddenOrderStatuses,
        _settings.StaffMembers,
        _settings.ShippingCostTaskAssigneeUserId,
        _settings.ShippingCostTaskToOrderOwner);

    /// <summary>
    /// The on-screen picks. Each list keeps its saved order (that's the order
    /// staff see in the pickers), with newly picked entries appended in row
    /// order.
    /// </summary>
    private WorkflowState CurrentWorkflowState()
    {
        var staffRows = UserRows.Where(r => r.IsStaff).ToList();
        return new WorkflowState(
            PickedStatuses(_settings.ShippingStatuses, r => r.ShowForShipping),
            PickedStatuses(_settings.PickupStatuses, r => r.ShowForPickup),
            PickedStatuses(_settings.HiddenOrderStatuses, r => r.HideFromOrders),
            KeepSavedOrder(_settings.StaffMembers.Select(s => s.PrintavoUserId), staffRows.Select(r => (r.Id, r.Name)))
                .Select(p => staffRows.First(r => r.Id == p.Id))
                .Select(r => new StaffMember(r.Id, r.Name, string.IsNullOrWhiteSpace(r.NotifyEmail) ? null : r.NotifyEmail.Trim()))
                .ToList(),
            ShippingCostTaskAssignee?.Id,
            ShippingCostTaskAssignee == PrintavoUserChoice.OrderOwner);
    }

    private List<ConfiguredPrintavoStatus> PickedStatuses(IEnumerable<ConfiguredPrintavoStatus> saved, Func<StatusSettingRow, bool> isPicked) =>
        KeepSavedOrder(saved.Select(s => s.Id), StatusRows.Where(isPicked).Select(r => (r.Id, r.Name)))
            .Select(p => new ConfiguredPrintavoStatus(p.Id, p.Name)).ToList();

    private static List<(string Id, string Name)> KeepSavedOrder(IEnumerable<string> savedIds, IEnumerable<(string Id, string Name)> picked)
    {
        var pickedList = picked.ToList();
        return savedIds.Where(id => pickedList.Any(p => p.Id == id))
            .Concat(pickedList.Select(p => p.Id))
            .Distinct()
            .Select(id => pickedList.First(p => p.Id == id))
            .ToList();
    }

    /// <summary>
    /// Rebuilds every Printavo Workflow row from scratch: the account's
    /// statuses/users in Printavo's order (current names), plus any saved
    /// pick Printavo didn't return (or that hasn't been fetched yet), so
    /// nothing saved silently disappears. Rows are replaced rather than
    /// patched, and the user picker's selection is resolved against the new
    /// choice list before it's published.
    /// </summary>
    private void BuildWorkflowRows(WorkflowState state)
    {
        var statusRows = new List<StatusSettingRow>();
        StatusSettingRow StatusRow(string id, string name)
        {
            var row = statusRows.FirstOrDefault(r => r.Id == id);
            if (row is null)
            {
                row = new StatusSettingRow { Id = id, Name = name };
                statusRows.Add(row);
            }

            return row;
        }

        foreach (var status in _printavoStatuses ?? [])
        {
            StatusRow(status.Id, status.Name);
        }

        foreach (var status in state.ShippingStatuses)
        {
            StatusRow(status.Id, status.Name).ShowForShipping = true;
        }

        foreach (var status in state.PickupStatuses)
        {
            StatusRow(status.Id, status.Name).ShowForPickup = true;
        }

        foreach (var status in state.HiddenOrderStatuses)
        {
            StatusRow(status.Id, status.Name).HideFromOrders = true;
        }

        // Non-staff rows start with their Printavo login email in the box, so
        // ticking someone usually needs no typing; saved staff keep whatever
        // email was saved for them (including none).
        var userRows = (_printavoUsers ?? [])
            .Select(u => new UserSettingRow { Id = u.Id, Name = u.Name, NotifyEmail = u.Email })
            .ToList();
        foreach (var staff in state.StaffMembers)
        {
            var row = userRows.FirstOrDefault(r => r.Id == staff.PrintavoUserId);
            if (row is null)
            {
                row = new UserSettingRow { Id = staff.PrintavoUserId, Name = staff.Name };
                userRows.Add(row);
            }

            row.IsStaff = true;
            row.NotifyEmail = staff.NotifyEmail ?? string.Empty;
        }

        var choices = new List<PrintavoUserChoice> { PrintavoUserChoice.None, PrintavoUserChoice.OrderOwner };
        choices.AddRange(userRows.Select(r => new PrintavoUserChoice(r.Id, r.Name)));
        PrintavoUserChoice Choice(string? id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return PrintavoUserChoice.None;
            }

            var choice = choices.FirstOrDefault(c => c.Id == id);
            if (choice is null)
            {
                choice = new PrintavoUserChoice(id, $"Printavo user #{id}");
                choices.Add(choice);
            }

            return choice;
        }

        var shippingCostAssignee = state.ShippingCostTaskToOrderOwner
            ? PrintavoUserChoice.OrderOwner
            : Choice(state.ShippingCostTaskAssigneeUserId);

        StatusRows = new ObservableCollection<StatusSettingRow>(statusRows);
        UserRows = new ObservableCollection<UserSettingRow>(userRows);
        UserChoices = choices;
        ShippingCostTaskAssignee = shippingCostAssignee;
    }

    /// <summary>Fetches the account's statuses and users; false (with the reason shown) if Printavo isn't connected or the fetch fails.</summary>
    private async Task<bool> TryLoadPrintavoListsAsync()
    {
        if (_settings.PrintavoApiTokenProtected is not { Length: > 0 })
        {
            PrintavoListsMessage = "Connect Printavo above to pick from your account's statuses and users.";
            return false;
        }

        PrintavoListsMessage = "Loading statuses and users from Printavo...";
        try
        {
            var statusesTask = _printavoClient.ListStatusesAsync();
            var usersTask = _printavoClient.ListUsersAsync();
            _printavoStatuses = await statusesTask;
            _printavoUsers = await usersTask;
            PrintavoListsMessage = $"Showing {_printavoStatuses.Count} statuses and {_printavoUsers.Count} users from Printavo.";
            return true;
        }
        catch (Exception ex)
        {
            PrintavoListsMessage = $"Couldn't load statuses/users from Printavo: {ex.Message}";
            return false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunSettingsCommand))]
    private async Task ReloadPrintavoListsAsync()
    {
        IsBusy = true;
        try
        {
            if (await TryLoadPrintavoListsAsync())
            {
                BuildWorkflowRows(CurrentWorkflowState());
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void AddBoxPreset() => BoxPresets.Add(new BoxPresetRow());

    [RelayCommand]
    private void RemoveBoxPreset(BoxPresetRow row) => BoxPresets.Remove(row);

    private static List<string> SplitNames(string names) =>
        names.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private bool CanRunSettingsCommand() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanRunSettingsCommand))]
    private async Task SaveAsync()
    {
        IsBusy = true;
        StatusMessage = string.Empty;
        ErrorMessage = string.Empty;
        try
        {
            if (ShipmentArchiveIntervalWeeks < 1)
            {
                ErrorMessage = "Archive interval must be at least 1 week.";
                IsBusy = false;
                return;
            }

            var workflow = CurrentWorkflowState();
            if (workflow.StaffMembers.FirstOrDefault(s => s.NotifyEmail is { } email && !email.Contains('@')) is { } badEmail)
            {
                ErrorMessage = $"\"{badEmail.NotifyEmail}\" (for {badEmail.Name}) isn't an email address.";
                IsBusy = false;
                return;
            }

            var boxPresets = BoxPresets
                .Where(p => !string.IsNullOrWhiteSpace(p.Name) || p.LengthIn != 0 || p.WidthIn != 0 || p.HeightIn != 0 || p.WeightLbs != 0)
                .ToList();
            if (boxPresets.Any(p => string.IsNullOrWhiteSpace(p.Name) || p.LengthIn <= 0 || p.WidthIn <= 0 || p.HeightIn <= 0 || p.WeightLbs <= 0))
            {
                ErrorMessage = "Each box size needs a name, all three dimensions, and a weight.";
                IsBusy = false;
                return;
            }

            if (ShippingFeeMarkupPercent < 0)
            {
                ErrorMessage = "Shipping markup can't be negative.";
                IsBusy = false;
                return;
            }

            _settings.ShippingStatuses = workflow.ShippingStatuses;
            _settings.PickupStatuses = workflow.PickupStatuses;
            _settings.HiddenOrderStatuses = workflow.HiddenOrderStatuses;
            _settings.StaffMembers = workflow.StaffMembers;
            _settings.ShippingCostTaskAssigneeUserId = workflow.ShippingCostTaskAssigneeUserId;
            _settings.ShippingCostTaskToOrderOwner = workflow.ShippingCostTaskToOrderOwner;
            _settings.UpsDeliveryMethodNames = SplitNames(UpsDeliveryMethodNames);
            _settings.FedExDeliveryMethodNames = SplitNames(FedExDeliveryMethodNames);
            _settings.PickupDeliveryMethodNames = SplitNames(PickupDeliveryMethodNames);
            _settings.DefaultCarrier = DefaultCarrier;
            _settings.BoxSizePresets = boxPresets
                .Select(p => new BoxSizePreset(p.Name.Trim(), p.LengthIn, p.WidthIn, p.HeightIn, p.WeightLbs))
                .ToList();
            _settings.ShippingFeeMarkupPercent = Math.Round((decimal)ShippingFeeMarkupPercent, 2);

            _settings.PrintavoEmail = PrintavoEmail;
            _settings.UpsEnvironment = UpsEnvironment;
            _settings.UpsMerchantId = UpsMerchantId;
            _settings.FedExEnvironment = FedExEnvironment;
            _settings.FedExAccountNumber = FedExAccountNumber;
            _settings.SelectedPrinterName = SelectedPrinterName;
            _settings.LabelPaperMode = LabelPaperMode;
            _settings.ShipmentArchiveIntervalWeeks = ShipmentArchiveIntervalWeeks;
            _settings.ShopLogoImage = ShopLogoImage;

            _settings.DefaultShipFromCompanyName = DefaultShipFromCompanyName;
            _settings.DefaultShipFromAttentionName = DefaultShipFromAttentionName;
            _settings.DefaultShipFromAddress1 = DefaultShipFromAddress1;
            _settings.DefaultShipFromAddress2 = DefaultShipFromAddress2;
            _settings.DefaultShipFromCity = DefaultShipFromCity;
            _settings.DefaultShipFromState = DefaultShipFromState;
            _settings.DefaultShipFromPostalCode = DefaultShipFromPostalCode;
            _settings.DefaultShipFromCountryCode = string.IsNullOrWhiteSpace(DefaultShipFromCountryCode) ? "US" : DefaultShipFromCountryCode;
            _settings.DefaultShipFromPhoneNumber = DefaultShipFromPhoneNumber;

            // Blank input means "leave the previously-saved secret alone" —
            // we never show a decrypted secret back in the UI, so a blank
            // box isn't "clear this", it's "unchanged".
            await _settingsService.SaveAsync(
                _settings,
                string.IsNullOrWhiteSpace(PrintavoApiTokenInput) ? null : PrintavoApiTokenInput,
                string.IsNullOrWhiteSpace(UpsClientIdInput) ? null : UpsClientIdInput,
                string.IsNullOrWhiteSpace(UpsClientSecretInput) ? null : UpsClientSecretInput,
                string.IsNullOrWhiteSpace(FedExClientIdInput) ? null : FedExClientIdInput,
                string.IsNullOrWhiteSpace(FedExClientSecretInput) ? null : FedExClientSecretInput);

            PrintavoApiTokenInput = string.Empty;
            UpsClientIdInput = string.Empty;
            UpsClientSecretInput = string.Empty;
            FedExClientIdInput = string.Empty;
            FedExClientSecretInput = string.Empty;

            ShopSettingsCache.Apply(_settings);
            await LoadAsync();
            StatusMessage = "Settings saved.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Save failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunSettingsCommand))]
    private async Task TestPrintavoConnectionAsync()
    {
        IsBusy = true;
        StatusMessage = string.Empty;
        ErrorMessage = string.Empty;
        try
        {
            var user = await _printavoClient.GetCurrentUserAsync();

            _settings.PrintavoCurrentUserId = user.Id;
            _settings.PrintavoCurrentUserName = user.Name;
            await _settingsService.SaveAsync(_settings, null, null, null);

            PrintavoCurrentUserName = user.Name;
            StatusMessage = $"Printavo connected as {user.Name} ({user.Email}).";

            if (await TryLoadPrintavoListsAsync())
            {
                BuildWorkflowRows(CurrentWorkflowState());
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Printavo connection failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunSettingsCommand))]
    private async Task TestUpsConnectionAsync()
    {
        IsBusy = true;
        StatusMessage = string.Empty;
        ErrorMessage = string.Empty;
        try
        {
            await _upsAuthService.GetAccessTokenAsync(forceRefresh: true);
            StatusMessage = "UPS OAuth token retrieved successfully.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"UPS connection failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunSettingsCommand))]
    private async Task TestFedExConnectionAsync()
    {
        IsBusy = true;
        StatusMessage = string.Empty;
        ErrorMessage = string.Empty;
        try
        {
            await _fedExAuthService.GetAccessTokenAsync(forceRefresh: true);
            StatusMessage = "FedEx OAuth token retrieved successfully.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"FedEx connection failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunSettingsCommand))]
    private async Task PrintTestLabelAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedPrinterName))
        {
            ErrorMessage = "Select a printer first.";
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;
        ErrorMessage = string.Empty;
        try
        {
            await _labelPrintService.PrintTestLabelAsync(SelectedPrinterName, LabelPaperMode);
            StatusMessage = $"Test label sent to {SelectedPrinterName}.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Print failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunSettingsCommand))]
    private async Task RunArchiveNowAsync()
    {
        if (!await _dialogService.ConfirmAsync(
            "Run Archive Now",
            "Export old shipments to a local JSON file and permanently remove them from Shipment History? This cannot be undone (though a backup file is written first)."))
        {
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;
        ErrorMessage = string.Empty;
        try
        {
            var result = await _shipmentArchiveService.RunNowAsync();
            StatusMessage = result.ArchivedCount > 0
                ? $"Archived {result.ArchivedCount} shipment(s) to {Path.GetFileName(result.FilePath)}."
                : "Nothing to archive.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Archive failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenArchiveFolder()
    {
        Process.Start(new ProcessStartInfo(DbPathProvider.GetArchiveFolder()) { UseShellExecute = true });
    }

    [RelayCommand]
    private void OpenArchiveViewer() => _navigationService.NavigateTo<ArchiveViewerViewModel>();

    private const int MaxShopLogoBytes = 2 * 1024 * 1024;

    [RelayCommand]
    private void ChooseShopLogo()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Choose shop logo for box labels",
            Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp"
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        ErrorMessage = string.Empty;
        var bytes = File.ReadAllBytes(dialog.FileName);
        if (bytes.Length > MaxShopLogoBytes)
        {
            ErrorMessage = "That logo file is over 2 MB — please pick a smaller image.";
            return;
        }

        if (TryCreatePreview(bytes) is null)
        {
            ErrorMessage = "That file couldn't be read as an image.";
            return;
        }

        ShopLogoImage = bytes;
        StatusMessage = "Logo chosen — click Save Settings to keep it.";
    }

    private bool CanRemoveShopLogo() => ShopLogoImage is not null;

    [RelayCommand(CanExecute = nameof(CanRemoveShopLogo))]
    private void RemoveShopLogo()
    {
        ShopLogoImage = null;
        StatusMessage = "Logo removed — click Save Settings to keep the change.";
    }

    partial void OnShopLogoImageChanged(byte[]? value) => ShopLogoPreview = TryCreatePreview(value);

    private static BitmapImage? TryCreatePreview(byte[]? bytes)
    {
        if (bytes is not { Length: > 0 })
        {
            return null;
        }

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = new MemoryStream(bytes);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is NotSupportedException or FileFormatException or ArgumentException)
        {
            return null;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunSettingsCommand))]
    private async Task AddOwnUpsAccountAsync()
    {
        if (string.IsNullOrWhiteSpace(NewOwnAccountLabel) || string.IsNullOrWhiteSpace(NewOwnAccountNumber))
        {
            ErrorMessage = "Give the account a label and account number.";
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var saved = await _upsAccountRepository.SaveAsync(new OwnUpsAccount
            {
                Label = NewOwnAccountLabel,
                AccountNumber = NewOwnAccountNumber,
                PostalCode = NewOwnAccountPostalCode,
                CountryCode = string.IsNullOrWhiteSpace(NewOwnAccountCountryCode) ? "US" : NewOwnAccountCountryCode,
                IsDefault = NewOwnAccountIsDefault
            });

            if (saved.IsDefault)
            {
                foreach (var existing in OwnUpsAccounts)
                {
                    existing.IsDefault = false;
                }
            }
            OwnUpsAccounts.Add(saved);

            NewOwnAccountLabel = string.Empty;
            NewOwnAccountNumber = string.Empty;
            NewOwnAccountPostalCode = string.Empty;
            NewOwnAccountCountryCode = "US";
            NewOwnAccountIsDefault = false;
            StatusMessage = $"Added UPS account \"{saved.Label}\".";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteOwnUpsAccountAsync(OwnUpsAccount account)
    {
        if (!await _dialogService.ConfirmAsync("Delete UPS Account", "Delete this UPS account?"))
        {
            return;
        }

        await _upsAccountRepository.DeleteAsync(account.Id);
        OwnUpsAccounts.Remove(account);
    }

    [RelayCommand(CanExecute = nameof(CanRunSettingsCommand))]
    private async Task AddOwnFedExAccountAsync()
    {
        if (string.IsNullOrWhiteSpace(NewOwnFedExAccountLabel) || string.IsNullOrWhiteSpace(NewOwnFedExAccountNumber))
        {
            ErrorMessage = "Give the account a label and account number.";
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var saved = await _fedExAccountRepository.SaveAsync(new OwnFedExAccount
            {
                Label = NewOwnFedExAccountLabel,
                AccountNumber = NewOwnFedExAccountNumber,
                PostalCode = NewOwnFedExAccountPostalCode,
                CountryCode = string.IsNullOrWhiteSpace(NewOwnFedExAccountCountryCode) ? "US" : NewOwnFedExAccountCountryCode,
                IsDefault = NewOwnFedExAccountIsDefault
            });

            if (saved.IsDefault)
            {
                foreach (var existing in OwnFedExAccounts)
                {
                    existing.IsDefault = false;
                }
            }
            OwnFedExAccounts.Add(saved);

            NewOwnFedExAccountLabel = string.Empty;
            NewOwnFedExAccountNumber = string.Empty;
            NewOwnFedExAccountPostalCode = string.Empty;
            NewOwnFedExAccountCountryCode = "US";
            NewOwnFedExAccountIsDefault = false;
            StatusMessage = $"Added FedEx account \"{saved.Label}\".";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteOwnFedExAccountAsync(OwnFedExAccount account)
    {
        if (!await _dialogService.ConfirmAsync("Delete FedEx Account", "Delete this FedEx account?"))
        {
            return;
        }

        await _fedExAccountRepository.DeleteAsync(account.Id);
        OwnFedExAccounts.Remove(account);
    }
}
