using PrintavoShipper.Core.Models;

namespace PrintavoShipper.App.Services;

public interface IContractorProvisioningService
{
    /// <summary>
    /// Explicitly imports a Printavo Customer as a local Contractor (B2B
    /// reseller) — only ever called from the Contractors screen's "Import
    /// from Printavo" action, never opportunistically when an order is
    /// opened. Opening an order for a customer that hasn't been imported
    /// this way treats it as a direct B2C customer instead (see
    /// OrderDetailViewModel.LoadAsync), which is the whole point of keeping
    /// this an explicit, deliberate action rather than automatic.
    ///
    /// If this is a brand-new contractor with no ship-from address yet, and
    /// Printavo has an address on file for the customer, auto-creates a
    /// default ship-from address from it (staff can edit/replace it
    /// afterward) — otherwise leaves it for manual entry.
    /// </summary>
    Task<Contractor> ImportFromPrintavoAsync(string printavoCustomerId, string companyName, CancellationToken ct = default);
}
