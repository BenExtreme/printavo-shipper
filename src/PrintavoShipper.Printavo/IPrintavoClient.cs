using PrintavoShipper.Printavo.Dto;

namespace PrintavoShipper.Printavo;

/// <summary>
/// Printavo GraphQL API v2 client. Queries only ever target the `invoices`/
/// `invoice` root fields (not `orders`/`order`), which are typed directly as
/// Invoice rather than the Quote|Invoice union — confirmed live that Quotes
/// carry the same fields, but only Invoices are shippable, so this sidesteps
/// the union/inline-fragment complexity entirely and Quotes simply never
/// show up in search results.
/// </summary>
public interface IPrintavoClient
{
    Task<PrintavoUser> GetCurrentUserAsync(CancellationToken ct = default);

    // Printavo caps pagination `first` at 25 (confirmed live: "'first' must not be greater than 25").
    // excludeStatusIds filters server-side (confirmed live against the `invoices` field's
    // own excludeStatusIds: [ID!] arg) — pass status ids to exclude, e.g. quoting/already-fulfilled
    // statuses that would otherwise crowd out active orders within the 25-row page.
    // Returns a page + cursor (rather than a flat list) so a caller can tell whether more
    // results exist, or walk further pages via `after`.
    Task<PrintavoOrderSearchPage> SearchOrdersAsync(string? query, IReadOnlyList<string>? excludeStatusIds = null, int first = 25, string? after = null, CancellationToken ct = default);

    /// <summary>Free-text searches for an exact visualId match (there's no direct "get by visual id" query).</summary>
    Task<PrintavoOrderDetail?> GetOrderByVisualIdAsync(string visualId, CancellationToken ct = default);

    Task<PrintavoOrderDetail?> GetOrderByIdAsync(string id, CancellationToken ct = default);

    Task<IReadOnlyList<PrintavoCustomerSummary>> ListCustomersAsync(int first = 25, CancellationToken ct = default);

    /// <summary>Fetches one customer's own address(es) — used to pre-fill a new contractor's default ship-from address.</summary>
    Task<PrintavoCustomerDetail?> GetCustomerAsync(string customerId, CancellationToken ct = default);

    /// <summary>Just the order's current customerNote HTML (empty if none) — a much lighter read than GetOrderByIdAsync for the read-then-write append. Throws if the order no longer exists.</summary>
    Task<string> GetCustomerNoteAsync(string orderId, CancellationToken ct = default);

    /// <summary>Replaces customerNote outright (invoiceUpdate is a true partial patch — other Invoice fields are untouched). Callers that want to append must read the current HTML first (e.g. via GetCustomerNoteAsync) and build the combined string themselves — Printavo has no server-side append.</summary>
    Task UpdateCustomerNoteAsync(string orderId, string newCustomerNoteHtml, CancellationToken ct = default);

    /// <summary>Changes an order's Printavo status (e.g. to one of the shipping/pick-up statuses picked in Settings). Returns the resulting status name for immediate UI feedback without a full order reload.</summary>
    Task<string> UpdateStatusAsync(string orderId, string statusId, CancellationToken ct = default);

    /// <summary>Creates a Task linked to the order via taskable (used both as the shipping-cost expense substitute and for the order screen's "Add Task" panel), and returns its id. assignedToUserId is optional — omitted if unknown rather than failing the whole call.</summary>
    Task<string> CreateTaskAsync(string name, DateTimeOffset dueAt, string? assignedToUserId, string taskableOrderId, CancellationToken ct = default);

    /// <summary>Adds a single, non-taxable fee line (quantity 1) to an invoice via feeCreate, and returns the new fee's id.</summary>
    Task<string> CreateInvoiceFeeAsync(string invoiceId, string description, decimal unitPrice, CancellationToken ct = default);

    /// <summary>Removes a fee from its invoice/quote via feeDelete.</summary>
    Task DeleteFeeAsync(string feeId, CancellationToken ct = default);

    /// <summary>Every user on the shop's Printavo account (Account.users) — the choices for who a task gets assigned to. Fetched once and cached for the app session.</summary>
    Task<IReadOnlyList<PrintavoUser>> ListUsersAsync(CancellationToken ct = default);

    /// <summary>The account's own configured order statuses (ids are account-specific) — the choices in Settings > Printavo Workflow.</summary>
    Task<IReadOnlyList<PrintavoOrderStatusOption>> ListStatusesAsync(CancellationToken ct = default);

    /// <summary>The Printavo user id of the order's owner (Invoice.owner, normally the CSR), or null if the order has none.</summary>
    Task<string?> GetOrderOwnerIdAsync(string orderId, CancellationToken ct = default);
}
