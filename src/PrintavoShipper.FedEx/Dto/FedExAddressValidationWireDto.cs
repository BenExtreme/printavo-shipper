namespace PrintavoShipper.FedEx.Dto;

// JSON wire format for POST /address/v1/addresses/resolve — FedEx's
// equivalent of UPS's XAV API. Same unverified-against-live-sandbox caveat
// as FedExShipWireDto.cs applies here.

public class FedExAddressValidationRequestWrapper
{
    public List<FedExAddressToValidate> AddressesToValidate { get; set; } = new();
}

public class FedExAddressToValidate
{
    public FedExWireAddress Address { get; set; } = new();
}

// ---- Response ----

public class FedExAddressValidationResponseWrapper
{
    public FedExAddressValidationOutput? Output { get; set; }
}

public class FedExAddressValidationOutput
{
    public List<FedExResolvedAddress> ResolvedAddresses { get; set; } = new();
}

public class FedExResolvedAddress
{
    public List<string>? StreetLinesToken { get; set; }
    public string? City { get; set; }
    public string? StateOrProvinceCode { get; set; }
    public string? PostalCode { get; set; }
    public string? CountryCode { get; set; }

    // BUSINESS | RESIDENTIAL | UNKNOWN — FedEx's residential/commercial
    // classification, the equivalent of UPS's AddressClassification.Code.
    public string? Classification { get; set; }

    public List<string>? AttributeFlags { get; set; }
}
