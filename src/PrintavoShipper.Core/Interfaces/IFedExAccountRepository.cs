using PrintavoShipper.Core.Models;

namespace PrintavoShipper.Core.Interfaces;

public interface IFedExAccountRepository
{
    /// <summary>Returns the shop's own FedEx accounts. Unlike IUpsAccountRepository.GetAllAsync, there's no legacy single-account AppSettings field to seed from.</summary>
    Task<List<OwnFedExAccount>> GetAllAsync(CancellationToken ct = default);

    Task<OwnFedExAccount> SaveAsync(OwnFedExAccount account, CancellationToken ct = default);

    Task DeleteAsync(int id, CancellationToken ct = default);
}
