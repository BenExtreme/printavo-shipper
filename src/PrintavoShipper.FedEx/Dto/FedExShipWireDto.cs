namespace PrintavoShipper.FedEx.Dto;

// JSON wire format for POST /ship/v1/shipments (create) and PUT
// /ship/v1/shipments/cancel (void), matching FedEx's published Ship API
// request/response shape (requestedShipment / output.transactionShipments).
//
// IMPORTANT: unlike the UPS DTOs in PrintavoShipper.Ups (each confirmed
// against UPS's official downloaded OpenAPI schema), these shapes are built
// from FedEx's publicly documented Ship API conventions but have NOT been
// confirmed against a live FedEx sandbox call yet — no FedEx developer
// credentials were available during this session. Field names/nesting here
// are a best-effort starting point; verify every shape against a real
// sandbox request/response (or FedEx's downloaded OpenAPI spec) before
// trusting this for a real shipment, the same way the UPS integration's
// wire format was pinned down empirically in its own Phase 3.
//
// FedEx uses camelCase JSON — FedExClient's JsonSerializerOptions sets
// PropertyNamingPolicy = JsonNamingPolicy.CamelCase, so these PascalCase C#
// properties serialize/deserialize as camelCase automatically; no
// JsonPropertyName attributes needed for the common case.

public class FedExShipRequestWrapper
{
    public string LabelResponseOptions { get; set; } = "LABEL";
    public FedExRequestedShipment RequestedShipment { get; set; } = new();
    public FedExAccountNumber AccountNumber { get; set; } = new();
}

public class FedExAccountNumber
{
    public string Value { get; set; } = string.Empty;
}

public class FedExRequestedShipment
{
    public string ShipDatestamp { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd");
    public string ServiceType { get; set; } = string.Empty;
    public string PackagingType { get; set; } = "YOUR_PACKAGING";
    public string PickupType { get; set; } = "DROPOFF_AT_FEDEX_LOCATION";
    public FedExShipParty Shipper { get; set; } = new();
    public List<FedExShipParty> Recipients { get; set; } = new();
    public FedExShippingChargesPayment ShippingChargesPayment { get; set; } = new();
    public FedExLabelSpecification LabelSpecification { get; set; } = new();
    public List<FedExRequestedPackageLineItem> RequestedPackageLineItems { get; set; } = new();
}

public class FedExShipParty
{
    public FedExContact Contact { get; set; } = new();
    public FedExWireAddress Address { get; set; } = new();
}

public class FedExContact
{
    public string? PersonName { get; set; }
    public string? CompanyName { get; set; }
    public string? PhoneNumber { get; set; }
}

public class FedExWireAddress
{
    public List<string> StreetLines { get; set; } = new();
    public string City { get; set; } = string.Empty;
    public string StateOrProvinceCode { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;

    // Only meaningful on a recipient address — omitted (left null) unless
    // the address was classified residential, mirroring WireParty.Residential's
    // presence/absence convention in the UPS client.
    public bool? Residential { get; set; }
}

public class FedExShippingChargesPayment
{
    // SENDER | THIRD_PARTY | RECIPIENT | ACCOUNT — this app only ever sends
    // SENDER (own account) or THIRD_PARTY.
    public string PaymentType { get; set; } = "SENDER";
    public FedExPayor? Payor { get; set; }
}

public class FedExPayor
{
    public FedExResponsibleParty ResponsibleParty { get; set; } = new();
}

public class FedExResponsibleParty
{
    public FedExAccountNumber AccountNumber { get; set; } = new();
    public FedExPayorAddress? Address { get; set; }
}

public class FedExPayorAddress
{
    public string PostalCode { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
}

public class FedExLabelSpecification
{
    // PDF — FedEx's own default and generally sharper than their PNG at
    // print time. LabelPrintService rasterizes it (via PDFium) to a
    // high-DPI bitmap right before printing rather than relying on
    // whatever fixed resolution FedEx would bake into a PNG.
    public string ImageType { get; set; } = "PDF";
    public string LabelStockType { get; set; } = "PAPER_4X6";
}

public class FedExRequestedPackageLineItem
{
    public FedExWeight Weight { get; set; } = new();
    public FedExDimensions? Dimensions { get; set; }

    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public List<FedExCustomerReference>? CustomerReferences { get; set; }
}

public class FedExWeight
{
    public string Units { get; set; } = "LB";
    public decimal Value { get; set; }
}

public class FedExDimensions
{
    public int Length { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string Units { get; set; } = "IN";
}

public class FedExCustomerReference
{
    public string CustomerReferenceType { get; set; } = "CUSTOMER_REFERENCE";
    public string Value { get; set; } = string.Empty;
}

// ---- Response ----

public class FedExShipResponseWrapper
{
    public FedExShipOutput? Output { get; set; }
}

public class FedExShipOutput
{
    public List<FedExTransactionShipment> TransactionShipments { get; set; } = new();
}

public class FedExTransactionShipment
{
    public string? MasterTrackingNumber { get; set; }
    public List<FedExPieceResponse> PieceResponses { get; set; } = new();
    public FedExCompletedShipmentDetail? CompletedShipmentDetail { get; set; }
}

public class FedExPieceResponse
{
    public string TrackingNumber { get; set; } = string.Empty;
    public List<FedExPackageDocument> PackageDocuments { get; set; } = new();
}

public class FedExPackageDocument
{
    public string? ContentType { get; set; }
    public string EncodedLabel { get; set; } = string.Empty;
}

public class FedExCompletedShipmentDetail
{
    public FedExShipmentRating? ShipmentRating { get; set; }
}

public class FedExShipmentRating
{
    public List<FedExShipmentRateDetail> ShipmentRateDetails { get; set; } = new();
}

public class FedExShipmentRateDetail
{
    public decimal? TotalNetCharge { get; set; }
    public string? Currency { get; set; }
}

// ---- Errors (Ship, Rate, Address Validation, and Void all use this shape) ----

public class FedExErrorResponseWrapper
{
    public List<FedExErrorMessage>? Errors { get; set; }
}

public class FedExErrorMessage
{
    public string? Code { get; set; }
    public string? Message { get; set; }

    // The specific field-level reason (e.g. which shipment/package field
    // failed validation) lives one or more levels deeper than this — FedEx
    // wraps it in a nested "errors" array under a generic outer message
    // like "Validation failed for object='shipShipmentInputVO'. Error
    // count: 1", sometimes with the concrete detail in "parameterList"
    // instead. Both are optional here so EnsureSuccess can walk down to
    // whichever leaf actually holds the useful text.
    public List<FedExErrorMessage>? Errors { get; set; }
    public List<FedExErrorParameter>? ParameterList { get; set; }
}

public class FedExErrorParameter
{
    public string? Key { get; set; }
    public string? Value { get; set; }
}

// ---- Void (PUT /ship/v1/shipments/cancel) ----

public class FedExCancelShipmentRequest
{
    public FedExAccountNumber AccountNumber { get; set; } = new();
    public string TrackingNumber { get; set; } = string.Empty;
    public string DeletionControl { get; set; } = "DELETE_ALL_PACKAGES";
}
