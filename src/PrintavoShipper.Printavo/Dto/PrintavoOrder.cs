namespace PrintavoShipper.Printavo.Dto;

public record PrintavoAddress(
    string? CompanyName,
    string? CustomerName,
    string Address1,
    string? Address2,
    string City,
    string State,
    string StateIso,
    string ZipCode,
    string Country,
    string CountryIso)
{
    public bool HasContent => !string.IsNullOrWhiteSpace(Address1) || !string.IsNullOrWhiteSpace(City);
}

public record PrintavoCustomAddress(
    string Id,
    string? Name,
    string? CompanyName,
    string? CustomerName,
    string Address1,
    string? Address2,
    string City,
    string State,
    string StateIso,
    string ZipCode,
    string Country,
    string CountryIso);

public record PrintavoOrderSummary(
    string Id,
    string VisualId,
    string? Nickname,
    string StatusName,
    string CustomerId,
    string CustomerCompanyName,
    decimal? Total,
    DateTimeOffset? DueAt,
    string? DeliveryMethodName);

/// <summary>
/// One page of a SearchOrdersAsync scan. EndCursor/HasNextPage mirror the
/// `invoices` connection's own pageInfo so callers can keep paging.
/// </summary>
public record PrintavoOrderSearchPage(
    IReadOnlyList<PrintavoOrderSummary> Orders,
    bool HasNextPage,
    string? EndCursor);

public record PrintavoOrderDetail(
    string Id,
    string VisualId,
    string? Nickname,
    string StatusName,
    string StatusId,
    string ContactId,
    string CustomerId,
    string CustomerCompanyName,
    string CustomerNoteHtml,
    string ProductionNoteHtml,
    PrintavoAddress? ShippingAddress,
    PrintavoAddress? BillingAddress,
    IReadOnlyList<PrintavoCustomAddress> CustomAddresses,
    decimal? Total,
    DateTimeOffset? DueAt,
    // "PO #" and the billing contact's phone/email as shown on Printavo's
    // own invoice page — visualPoNumber, contact.phone, contact.email.
    // Confirmed live via introspection: Customer has no phone field at all,
    // so the contact-level phone is the closest thing Printavo has.
    string? PoNumber,
    string? ContactEmail,
    string? ContactPhone,
    string? DeliveryMethodName,
    // Invoice.amountOutstanding / paidInFull — the unpaid balance, shown on
    // the order screen since some customers' orders don't ship until paid.
    decimal? AmountOutstanding = null,
    bool PaidInFull = false,
    // Invoice.customerDueAt — a plain date, unlike dueAt (the production due
    // date, a UTC datetime).
    DateOnly? CustomerDueDate = null,
    // Invoice.contractorProfile — the white-label identity Printavo prints on
    // its own box labels/paperwork. Confirmed live: logo is an absolute
    // Filestack URL serving a PNG. Not the same thing as this app's
    // Customer-based "contractor" used for shipping.
    string? ContractorProfileName = null,
    string? ContractorLogoUrl = null,
    string? ContractorPhone = null)
{
    public bool HasBalanceDue => !PaidInFull && AmountOutstanding > 0;
}

public record PrintavoCustomerSummary(string Id, string CompanyName);

/// <summary>
/// Customer.shippingAddress/billingAddress are the plainer "Address" type
/// (no companyName/customerName fields, unlike CustomerAddress/CustomAddress
/// on orders) — confirmed live via schema introspection.
/// </summary>
public record PrintavoCustomerDetail(
    string Id,
    string CompanyName,
    PrintavoAddress? ShippingAddress,
    PrintavoAddress? BillingAddress);
