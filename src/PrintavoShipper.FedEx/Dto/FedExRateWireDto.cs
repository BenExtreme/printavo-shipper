namespace PrintavoShipper.FedEx.Dto;

// JSON wire format for POST /rate/v1/rates/quotes. Same unverified-against-
// live-sandbox caveat as FedExShipWireDto.cs applies here.

public class FedExRateRequestWrapper
{
    public FedExAccountNumber AccountNumber { get; set; } = new();
    public FedExRateRequestedShipment RequestedShipment { get; set; } = new();
}

public class FedExRateRequestedShipment
{
    public FedExShipParty Shipper { get; set; } = new();
    public FedExShipParty Recipient { get; set; } = new();
    public string PickupType { get; set; } = "DROPOFF_AT_FEDEX_LOCATION";
    public List<string> RateRequestType { get; set; } = new() { "ACCOUNT", "LIST" };
    // Mirrors the Ship request's ShippingChargesPayment (same type) — needed
    // so rate quotes reflect third-party-billed surcharges/discounts
    // correctly rather than always rating as if the shop's own account pays.
    public FedExShippingChargesPayment ShippingChargesPayment { get; set; } = new();
    public List<FedExRequestedPackageLineItem> RequestedPackageLineItems { get; set; } = new();
}

// ---- Response ----

public class FedExRateResponseWrapper
{
    public FedExRateOutput? Output { get; set; }
}

public class FedExRateOutput
{
    public List<FedExRateReplyDetail> RateReplyDetails { get; set; } = new();
}

public class FedExRateReplyDetail
{
    public string? ServiceType { get; set; }
    public string? ServiceName { get; set; }
    public List<FedExRatedShipmentDetail> RatedShipmentDetails { get; set; } = new();
    public FedExCommit? Commit { get; set; }
}

public class FedExRatedShipmentDetail
{
    public decimal? TotalNetCharge { get; set; }
    public string? Currency { get; set; }
    public string? RateType { get; set; }
}

public class FedExCommit
{
    public FedExTransitTime? TransitTime { get; set; }
}

public class FedExTransitTime
{
    public string? Description { get; set; }
}
