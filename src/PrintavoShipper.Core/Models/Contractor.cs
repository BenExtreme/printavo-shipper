using System.Collections.ObjectModel;
using System.ComponentModel;

namespace PrintavoShipper.Core.Models;

/// <summary>
/// Local cache of a Printavo Customer (the "contractor"/reseller who is
/// billed for the order in Printavo — see order.contact.customer). Printavo
/// itself doesn't store ship-from addresses or third-party billing accounts,
/// so those are configured here, keyed off the Printavo Customer id.
///
/// Implements INotifyPropertyChanged (for IsArchived and the computed
/// IsConfigured) and uses ObservableCollection for the nested lists so the
/// WPF UI can mutate this object in place after a CRUD operation — add/
/// remove one row, flip one flag — instead of reloading and replacing the
/// whole Contractors list and SelectedContractor reference, which fights
/// with DataGrid's SelectedItem tracking and [ObservableProperty]'s
/// equality-based change suppression.
/// </summary>
public class Contractor : INotifyPropertyChanged
{
    private bool isArchived;

    public Contractor()
    {
        // IsConfigured is derived live from these collections rather than a
        // separately persisted bit, so it can never go stale (the old
        // persisted flag stayed true forever once set, even after every
        // ship-from address/billing account was later deleted).
        ShipFromAddresses.CollectionChanged += (_, _) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsConfigured)));
        BillingAccounts.CollectionChanged += (_, _) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsConfigured)));
        FedExBillingAccounts.CollectionChanged += (_, _) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsConfigured)));
    }

    /// <summary>Printavo Customer.id (opaque string, not necessarily numeric).</summary>
    public string Id { get; set; } = string.Empty;

    public string CompanyName { get; set; } = string.Empty;

    public bool IsConfigured => ShipFromAddresses.Count > 0 || BillingAccounts.Count > 0 || FedExBillingAccounts.Count > 0;

    /// <summary>
    /// Archived contractors are hidden from the main Contractors list but
    /// never deleted — ShipmentRecord.ContractorId keeps pointing at them,
    /// so their profile must stay intact for shipment history. Never reset
    /// automatically by discovery/import; only an explicit user action
    /// changes this.
    /// </summary>
    public bool IsArchived
    {
        get => isArchived;
        set
        {
            if (isArchived != value)
            {
                isArchived = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsArchived)));
            }
        }
    }

    public DateTimeOffset? ArchivedAt { get; set; }

    public DateTimeOffset FirstSeenAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;

    public ObservableCollection<ContractorShipFromAddress> ShipFromAddresses { get; set; } = new();
    public ObservableCollection<ContractorBillingAccount> BillingAccounts { get; set; } = new();
    public ObservableCollection<ContractorFedExBillingAccount> FedExBillingAccounts { get; set; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;
}

public class ContractorShipFromAddress
{
    public int Id { get; set; }
    public string ContractorId { get; set; } = string.Empty;
    public Contractor? Contractor { get; set; }

    public string Label { get; set; } = string.Empty;
    public string? AttentionName { get; set; }
    public string? CompanyName { get; set; }
    public string Address1 { get; set; } = string.Empty;
    public string? Address2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string CountryCode { get; set; } = "US";
    public string? PhoneNumber { get; set; }
    public bool IsDefault { get; set; }
}

public class ContractorBillingAccount
{
    public int Id { get; set; }
    public string ContractorId { get; set; } = string.Empty;
    public Contractor? Contractor { get; set; }

    public string Label { get; set; } = string.Empty;
    public string UpsAccountNumber { get; set; } = string.Empty;
    public string UpsAccountPostalCode { get; set; } = string.Empty;
    public string UpsAccountCountryCode { get; set; } = "US";
    public bool IsDefault { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// A contractor's saved third-party FedEx billing account — the FedEx
/// equivalent of ContractorBillingAccount, kept as its own table rather than
/// generalizing the UPS one (which is live, UPS-field-named, and shouldn't be
/// touched for this). This is the primary FedEx billing path for this shop,
/// since FedEx shipments here are almost always third-party billed.
/// </summary>
public class ContractorFedExBillingAccount
{
    public int Id { get; set; }
    public string ContractorId { get; set; } = string.Empty;
    public Contractor? Contractor { get; set; }

    public string Label { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountPostalCode { get; set; } = string.Empty;
    public string AccountCountryCode { get; set; } = "US";
    public bool IsDefault { get; set; }
    public string? Notes { get; set; }
}
