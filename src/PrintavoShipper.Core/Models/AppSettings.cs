namespace PrintavoShipper.Core.Models;

public enum UpsEnvironment
{
    Cie = 0,
    Production = 1
}

public enum FedExEnvironment
{
    Sandbox = 0,
    Production = 1
}

/// <summary>
/// Which physical stock a label prints onto. Thermal4x6 targets a
/// dedicated 4x6in label printer (the original/default behavior).
/// Letter8x11Centered targets a standard office printer loaded with
/// 8.5x11in paper — the 4x6 label image is drawn at true size, centered
/// on the sheet, rather than stretched to fill it.
/// </summary>
public enum LabelPaperMode
{
    Thermal4x6 = 0,
    Letter8x11Centered = 1
}

/// <summary>
/// Single-row table (Id is always 1) holding app-wide configuration and
/// encrypted API credentials.
/// </summary>
public class AppSettings
{
    public int Id { get; set; } = 1;

    public string? PrintavoEmail { get; set; }
    public byte[]? PrintavoApiTokenProtected { get; set; }
    public string? PrintavoCurrentUserId { get; set; }
    public string? PrintavoCurrentUserName { get; set; }

    public byte[]? UpsClientIdProtected { get; set; }
    public byte[]? UpsClientSecretProtected { get; set; }
    public UpsEnvironment UpsEnvironment { get; set; } = UpsEnvironment.Cie;
    public string? UpsShipperAccountNumber { get; set; }
    public string? UpsShipperAccountPostalCode { get; set; }
    public string? UpsShipperAccountCountryCode { get; set; } = "US";
    public string? UpsMerchantId { get; set; }

    public byte[]? FedExClientIdProtected { get; set; }
    public byte[]? FedExClientSecretProtected { get; set; }
    public FedExEnvironment FedExEnvironment { get; set; } = FedExEnvironment.Sandbox;

    /// <summary>
    /// The FedEx account number tied to this app's developer-portal API
    /// project — required by FedEx's Ship/Rate/Cancel request shape as the
    /// top-level "accountNumber" identifying who is calling the API, which
    /// is a separate concept from who pays for a given shipment (see
    /// FedExShipmentRequest.BillingType/ThirdPartyAccountNumber). Not
    /// secret, so stored in plain text like UpsMerchantId.
    /// </summary>
    public string? FedExAccountNumber { get; set; }

    public string? SelectedPrinterName { get; set; }
    public string LabelImageFormat { get; set; } = "GIF";
    public LabelPaperMode LabelPaperMode { get; set; } = LabelPaperMode.Thermal4x6;

    /// <summary>
    /// Ship-from address used when a customer isn't tied to a configured
    /// contractor (no ship-from address of its own yet) — the shop ships
    /// as itself in that case rather than blocking on a contractor setup
    /// step. Editable in Settings.
    /// </summary>
    public string DefaultShipFromCompanyName { get; set; } = string.Empty;
    public string? DefaultShipFromAttentionName { get; set; }
    public string DefaultShipFromAddress1 { get; set; } = string.Empty;
    public string? DefaultShipFromAddress2 { get; set; }
    public string DefaultShipFromCity { get; set; } = string.Empty;
    public string DefaultShipFromState { get; set; } = string.Empty;
    public string DefaultShipFromPostalCode { get; set; } = string.Empty;
    public string DefaultShipFromCountryCode { get; set; } = "US";
    public string? DefaultShipFromPhoneNumber { get; set; }

    /// <summary>Whether the shop's own ship-from address has the fields UPS/FedEx require — blank on a fresh install until filled in under Settings.</summary>
    public bool IsDefaultShipFromConfigured =>
        !string.IsNullOrWhiteSpace(DefaultShipFromCompanyName)
        && !string.IsNullOrWhiteSpace(DefaultShipFromAddress1)
        && !string.IsNullOrWhiteSpace(DefaultShipFromCity)
        && !string.IsNullOrWhiteSpace(DefaultShipFromState)
        && !string.IsNullOrWhiteSpace(DefaultShipFromPostalCode);

    // ---- Printavo workflow (Settings > Printavo Workflow) ----

    /// <summary>Statuses offered in the order screen's status picker for shipping (non-Pick-up) orders, in picker order.</summary>
    public List<ConfiguredPrintavoStatus> ShippingStatuses { get; set; } = new();

    /// <summary>Statuses offered in the order screen's status picker for Pick-up orders, in picker order.</summary>
    public List<ConfiguredPrintavoStatus> PickupStatuses { get; set; } = new();

    /// <summary>Statuses left out of the Orders list (quotes, already shipped, on hold...), excluded server-side.</summary>
    public List<ConfiguredPrintavoStatus> HiddenOrderStatuses { get; set; } = new();

    /// <summary>Shop staff, in picker order — the Add Task "Assign to" list and (those with an email) the UPS "also notify on delivery" list.</summary>
    public List<StaffMember> StaffMembers { get; set; } = new();

    /// <summary>Who own-account shipping-cost tasks are assigned to — null leaves them unassigned. Ignored when ShippingCostTaskToOrderOwner is set.</summary>
    public string? ShippingCostTaskAssigneeUserId { get; set; }

    /// <summary>Assign own-account shipping-cost tasks to the order's own Printavo owner (Invoice.owner) instead of a fixed user.</summary>
    public bool ShippingCostTaskToOrderOwner { get; set; }

    /// <summary>Printavo deliveryMethod.name values (matched case-insensitively) that ship via UPS.</summary>
    public List<string> UpsDeliveryMethodNames { get; set; } = new() { "UPS", "Shipping" };

    /// <summary>Printavo deliveryMethod.name values that ship via FedEx.</summary>
    public List<string> FedExDeliveryMethodNames { get; set; } = new() { "FedEx" };

    /// <summary>Printavo deliveryMethod.name values that are picked up — box labels instead of shipping labels.</summary>
    public List<string> PickupDeliveryMethodNames { get; set; } = new() { "Pick-up" };

    // ---- Shipping defaults (Settings > Shipping Defaults) ----

    /// <summary>Quick-fill box sizes on package rows; the first one is each new row's default.</summary>
    public List<BoxSizePreset> BoxSizePresets { get; set; } = new();

    /// <summary>"Add Shipping Cost To Invoice": percent added on top of the negotiated rate (see ShippingFeeCalculator).</summary>
    public decimal ShippingFeeMarkupPercent { get; set; }

    /// <summary>
    /// The shop's own logo (raw PNG/JPG/BMP file bytes), printed on Pick-up
    /// box labels for orders with no Printavo contractor profile logo.
    /// Uploaded in Settings; null = the company name is printed as text.
    /// </summary>
    public byte[]? ShopLogoImage { get; set; }

    /// <summary>
    /// Last-used main window geometry, restored on next launch — null on a
    /// fresh install, in which case MainWindow keeps its XAML default
    /// (720x1100, centered). Persisted from MainWindow.OnClosing.
    /// </summary>
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public bool WindowIsMaximized { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Archive cadence and age threshold, in weeks — the same number drives
    /// both "how often the sweep runs" and "how old a completed
    /// (Purchased/Voided) shipment must be before it's exported and deleted"
    /// (see ShipmentArchiveService). Draft/RateQuoted/Error shipments are
    /// never auto-archived regardless of age. Editable in Settings.
    /// </summary>
    public int ShipmentArchiveIntervalWeeks { get; set; } = 2;

    /// <summary>
    /// UTC timestamp of the last time the shipment archive sweep actually
    /// ran (found due or triggered manually). Null means it has never run —
    /// the next check will treat it as due. Set by ShipmentArchiveService.
    /// </summary>
    public DateTimeOffset? ShipmentArchiveLastRunAt { get; set; }
}
