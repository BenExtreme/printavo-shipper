using PrintavoShipper.Core.Models;

namespace PrintavoShipper.Core.Interfaces;

public interface IUpsAccountRepository
{
    /// <summary>
    /// Returns the shop's own UPS accounts. The first time this is called
    /// with an empty table but a legacy AppSettings.UpsShipperAccountNumber
    /// already set, it seeds one row from that value so existing single-
    /// account setups keep working without a manual migration step.
    /// </summary>
    Task<List<OwnUpsAccount>> GetAllAsync(CancellationToken ct = default);

    Task<OwnUpsAccount> SaveAsync(OwnUpsAccount account, CancellationToken ct = default);

    Task DeleteAsync(int id, CancellationToken ct = default);
}
