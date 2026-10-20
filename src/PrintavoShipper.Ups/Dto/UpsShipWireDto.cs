using System.Text.Json.Serialization;

namespace PrintavoShipper.Ups.Dto;

// JSON wire format for POST /api/shipments/{version}/ship, matching UPS's
// Shipping.yaml (SHIPRequestWrapper / ShipmentResponse) — confirmed against
// the official UPS-API/api-documentation repo, not guessed. Property names
// match the wire format exactly (PascalCase) so no JsonPropertyName
// attributes are needed for the common case.

public class ShipRequestWrapper
{
    public ShipmentRequestWire ShipmentRequest { get; set; } = new();
}

public class ShipmentRequestWire
{
    public ShipRequest Request { get; set; } = new();
    public WireShipment Shipment { get; set; } = new();
    public WireLabelSpecification LabelSpecification { get; set; } = new();
}

public class ShipRequest
{
    public string RequestOption { get; set; } = "nonvalidate";
    public string SubVersion { get; set; } = "1801";
    public WireTransactionReference? TransactionReference { get; set; }
}

public class WireTransactionReference
{
    public string? CustomerContext { get; set; }
}

public class WireShipment
{
    public string? Description { get; set; }
    public WireShipmentRatingOptions? ShipmentRatingOptions { get; set; }
    public WireShipper Shipper { get; set; } = new();
    public WireParty ShipTo { get; set; } = new();
    public WireParty ShipFrom { get; set; } = new();

    // No Shipment-level ReferenceNumber field: confirmed live (real UPS error
    // "120541: Shipment/ReferenceNumber is not allowed for this shipment")
    // and by UPS's own schema — Shipment_ReferenceNumber.Value is only valid
    // when the origin/destination pair is NOT US/US or PR/PR, the opposite of
    // this shop's domestic-only shipments. Reference numbers go on the first
    // Package instead (Package_ReferenceNumber.Value: valid when the pair IS
    // US/US or PR/PR) — see WirePackage.ReferenceNumber.
    public WirePaymentInformation PaymentInformation { get; set; } = new();
    public WireService Service { get; set; } = new();

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WireShipmentServiceOptions? ShipmentServiceOptions { get; set; }

    public List<WirePackage> Package { get; set; } = new();
}

public class WireShipmentRatingOptions
{
    public string? NegotiatedRatesIndicator { get; set; }
}

public class WireReferenceNumber
{
    // Code (Reference Number Code) is optional for domestic (US/US, PR/PR)
    // shipments per UPS's schema — left unset, so the two entries are read
    // positionally (REF1/REF2 on the label) instead.
    public string Value { get; set; } = string.Empty;
}

public class WireShipmentServiceOptions
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<WireNotification>? Notification { get; set; }
}

public class WireNotification
{
    // 6 = Quantum View Ship Notification, 8 = Quantum View Delivery Notification.
    public string NotificationCode { get; set; } = string.Empty;
    public WireNotificationEmail EMail { get; set; } = new();
}

public class WireNotificationEmail
{
    public List<string> EMailAddress { get; set; } = new();
}

public class WirePhone
{
    public string? Number { get; set; }
}

public class WireAddress
{
    public List<string> AddressLine { get; set; } = new();
    public string City { get; set; } = string.Empty;
    public string StateProvinceCode { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
}

public class WireShipper
{
    public string Name { get; set; } = string.Empty;
    public string? AttentionName { get; set; }
    public string ShipperNumber { get; set; } = string.Empty;
    public WirePhone? Phone { get; set; }
    public WireAddress Address { get; set; } = new();
}

public class WireParty
{
    public string Name { get; set; } = string.Empty;
    public string? AttentionName { get; set; }
    public WirePhone? Phone { get; set; }
    public WireAddress Address { get; set; } = new();

    // Presence/absence indicator (only meaningful on ShipTo) — omitted
    // entirely unless the address was classified residential.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Residential { get; set; }
}

public class WirePaymentInformation
{
    public WireShipmentCharge ShipmentCharge { get; set; } = new();
}

public class WireShipmentCharge
{
    // 01 = Transportation. Who pays is determined by which of the two
    // properties below is populated, not by this Type value.
    public string Type { get; set; } = "01";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WireBillShipper? BillShipper { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WireBillThirdParty? BillThirdParty { get; set; }
}

public class WireBillShipper
{
    public string AccountNumber { get; set; } = string.Empty;
}

public class WireBillThirdParty
{
    public string AccountNumber { get; set; } = string.Empty;
    public WireBillThirdPartyAddress Address { get; set; } = new();
}

public class WireBillThirdPartyAddress
{
    public string PostalCode { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
}

public class WireService
{
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class WirePackage
{
    public WirePackaging Packaging { get; set; } = new();
    public WireDimensions? Dimensions { get; set; }
    public WirePackageWeight PackageWeight { get; set; } = new();

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<WireReferenceNumber>? ReferenceNumber { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WirePackageServiceOptions? PackageServiceOptions { get; set; }
}

public class WirePackageServiceOptions
{
    // A single object here, not a list like ShipmentServiceOptions.Notification
    // (UPS schema: Package_PackageServiceOptions.Notification), so a package
    // can carry only one notification code.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WireNotification? Notification { get; set; }
}

public class WirePackaging
{
    // 02 = Customer Supplied Package.
    public string Code { get; set; } = "02";
}

public class WireUnitOfMeasurement
{
    public string Code { get; set; } = string.Empty;
}

public class WireDimensions
{
    public WireUnitOfMeasurement UnitOfMeasurement { get; set; } = new() { Code = "IN" };
    public string Length { get; set; } = string.Empty;
    public string Width { get; set; } = string.Empty;
    public string Height { get; set; } = string.Empty;
}

public class WirePackageWeight
{
    public WireUnitOfMeasurement UnitOfMeasurement { get; set; } = new() { Code = "LBS" };
    public string Weight { get; set; } = string.Empty;
}

public class WireLabelSpecification
{
    public WireLabelImageFormat LabelImageFormat { get; set; } = new();
}

public class WireLabelImageFormat
{
    public string Code { get; set; } = "GIF";
}

// ---- Response ----

public class ShipmentResponseWrapper
{
    public ShipmentResponseWire? ShipmentResponse { get; set; }
}

public class ShipmentResponseWire
{
    public ShipmentResults? ShipmentResults { get; set; }
}

public class ShipmentResults
{
    public string ShipmentIdentificationNumber { get; set; } = string.Empty;
    public WireShipmentCharges? ShipmentCharges { get; set; }
    public WireNegotiatedRateCharges? NegotiatedRateCharges { get; set; }
    public List<PackageResult> PackageResults { get; set; } = new();
}

public class WireShipmentCharges
{
    public MonetaryAmount? TotalCharges { get; set; }
}

public class WireNegotiatedRateCharges
{
    public MonetaryAmount? TotalCharge { get; set; }
}

public class MonetaryAmount
{
    public string? CurrencyCode { get; set; }
    public string? MonetaryValue { get; set; }
}

public class PackageResult
{
    public string TrackingNumber { get; set; } = string.Empty;
    public ShippingLabelResult? ShippingLabel { get; set; }
}

public class ShippingLabelResult
{
    public string GraphicImage { get; set; } = string.Empty;
}

// ---- Errors (both Ship and Void use this shape) ----

public class UpsErrorResponseWrapper
{
    public UpsErrorResponse? Response { get; set; }
}

public class UpsErrorResponse
{
    public List<UpsErrorMessage> Errors { get; set; } = new();
}

public class UpsErrorMessage
{
    public string? Code { get; set; }
    public string? Message { get; set; }
}

// ---- Void ----

public class VoidShipmentResponseWrapper
{
    public VoidShipmentResponseWire? VoidShipmentResponse { get; set; }
}

public class VoidShipmentResponseWire
{
    public VoidResponseStatus? Response { get; set; }
}

public class VoidResponseStatus
{
    public WireResponseStatus? ResponseStatus { get; set; }
}

public class WireResponseStatus
{
    public string? Code { get; set; }
    public string? Description { get; set; }
}
