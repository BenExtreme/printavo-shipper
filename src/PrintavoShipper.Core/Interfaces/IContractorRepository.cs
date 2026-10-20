using PrintavoShipper.Core.Models;

namespace PrintavoShipper.Core.Interfaces;

public interface IContractorRepository
{
    Task<List<Contractor>> GetAllAsync(bool includeArchived = false, CancellationToken ct = default);

    Task<Contractor?> GetByIdAsync(string contractorId, CancellationToken ct = default);

    /// <summary>Every locally-known contractor id, archived or not — just the ids, without loading addresses/billing accounts.</summary>
    Task<HashSet<string>> GetAllIdsAsync(CancellationToken ct = default);

    /// <summary>
    /// Inserts a bare Contractor row (no ship-from/billing set up yet) if
    /// this Printavo Customer id hasn't been seen before, or bumps
    /// LastSeenAt if it has. Only ever called from the explicit "Import from
    /// Printavo" action (see IContractorProvisioningService.
    /// ImportFromPrintavoAsync) — never opportunistically when an order is
    /// opened, so a customer never silently becomes a contractor just
    /// because someone viewed their order.
    /// </summary>
    Task<Contractor> UpsertFromPrintavoAsync(string printavoCustomerId, string companyName, CancellationToken ct = default);

    Task<ContractorShipFromAddress> SaveShipFromAddressAsync(ContractorShipFromAddress address, CancellationToken ct = default);

    Task DeleteShipFromAddressAsync(int id, CancellationToken ct = default);

    Task<ContractorBillingAccount> SaveBillingAccountAsync(ContractorBillingAccount account, CancellationToken ct = default);

    Task DeleteBillingAccountAsync(int id, CancellationToken ct = default);

    Task<ContractorFedExBillingAccount> SaveFedExBillingAccountAsync(ContractorFedExBillingAccount account, CancellationToken ct = default);

    Task DeleteFedExBillingAccountAsync(int id, CancellationToken ct = default);

    /// <summary>Archives or unarchives a contractor. Never deletes — shipment history keeps its ContractorId intact either way.</summary>
    Task SetArchivedAsync(string contractorId, bool isArchived, CancellationToken ct = default);
}
