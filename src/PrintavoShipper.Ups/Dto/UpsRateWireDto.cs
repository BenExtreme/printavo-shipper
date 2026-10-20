namespace PrintavoShipper.Ups.Dto;

// JSON wire format for POST /api/rating/{version}/Shop, matching UPS's
// Rating.yaml (RATERequestWrapper / RateResponse). Reuses the same address/
// package shapes conceptually as the Ship API, but the Rating API's field
// names differ slightly (PaymentDetails vs PaymentInformation, an array of
// ShipmentCharge, PackagingType vs Packaging) so it gets its own DTOs rather
// than sharing UpsShipWireDto's types.

public class RateRequestWrapper
{
    public RateRequestWire RateRequest { get; set; } = new();
}

public class RateRequestWire
{
    public RateRequest Request { get; set; } = new();
    public RateShipment Shipment { get; set; } = new();
}

public class RateRequest
{
    public WireTransactionReference? TransactionReference { get; set; }
}

public class RateShipment
{
    // Without NegotiatedRatesIndicator UPS omits NegotiatedRateCharges from
    // the response entirely and only the published rate comes back.
    public WireShipmentRatingOptions? ShipmentRatingOptions { get; set; }
    public WireShipper Shipper { get; set; } = new();
    public WireParty ShipTo { get; set; } = new();
    public WireParty ShipFrom { get; set; } = new();
    public RatePaymentDetails PaymentDetails { get; set; } = new();
    public List<RatePackage> Package { get; set; } = new();
    // Required (with the Shoptimeintransit request option) to get transit
    // days for non-guaranteed services like Ground — GuaranteedDelivery is
    // only populated for services UPS guarantees.
    public RateDeliveryTimeInformation? DeliveryTimeInformation { get; set; }
}

public class RateDeliveryTimeInformation
{
    // 03 = Non-Document. Pickup date omitted → UPS defaults it to today.
    public string PackageBillType { get; set; } = "03";
}

public class RatePaymentDetails
{
    public List<WireShipmentCharge> ShipmentCharge { get; set; } = new();
}

public class RatePackage
{
    public RatePackagingType PackagingType { get; set; } = new();
    public WireDimensions? Dimensions { get; set; }
    public WirePackageWeight PackageWeight { get; set; } = new();
}

public class RatePackagingType
{
    public string Code { get; set; } = "02";
}

// ---- Response ----

public class RateResponseWrapper
{
    public RateResponseWire? RateResponse { get; set; }
}

public class RateResponseWire
{
    public List<RatedShipment> RatedShipment { get; set; } = new();
}

public class RatedShipment
{
    public WireService? Service { get; set; }
    public MonetaryAmount? TotalCharges { get; set; }
    public RatedShipmentNegotiatedRateCharges? NegotiatedRateCharges { get; set; }
    public RatedShipmentGuaranteedDelivery? GuaranteedDelivery { get; set; }
    public RatedShipmentTimeInTransit? TimeInTransit { get; set; }
}

public class RatedShipmentTimeInTransit
{
    public RatedShipmentServiceSummary? ServiceSummary { get; set; }
}

public class RatedShipmentServiceSummary
{
    public RatedShipmentEstimatedArrival? EstimatedArrival { get; set; }
}

public class RatedShipmentEstimatedArrival
{
    public string? BusinessDaysInTransit { get; set; }
}

public class RatedShipmentNegotiatedRateCharges
{
    public MonetaryAmount? TotalCharge { get; set; }
}

public class RatedShipmentGuaranteedDelivery
{
    public string? BusinessDaysInTransit { get; set; }
}
