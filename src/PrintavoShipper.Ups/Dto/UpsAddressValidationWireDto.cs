namespace PrintavoShipper.Ups.Dto;

// JSON wire format for POST /api/addressvalidation/{version}/{requestoption},
// matching UPS's AddressValidation.yaml (XAVRequestWrapper / XAVResponseWrapper)
// — confirmed against the official UPS-API/api-documentation repo.

public class XavRequestWrapper
{
    public XavRequest XAVRequest { get; set; } = new();
}

public class XavRequest
{
    public XavAddressKeyFormat AddressKeyFormat { get; set; } = new();
}

public class XavAddressKeyFormat
{
    public string? ConsigneeName { get; set; }
    public List<string> AddressLine { get; set; } = new();
    public string? PoliticalDivision2 { get; set; }
    public string? PoliticalDivision1 { get; set; }
    public string? PostcodePrimaryLow { get; set; }
    public string? PostcodeExtendedLow { get; set; }
    public string CountryCode { get; set; } = string.Empty;
}

// ---- Response ----

public class XavResponseWrapper
{
    public XavResponse? XAVResponse { get; set; }
}

public class XavResponse
{
    // Presence/absence indicators: UPS sends these as an empty string when
    // true and omits the property entirely when false — non-null means
    // present/true regardless of the (usually empty) string content.
    public string? ValidAddressIndicator { get; set; }
    public string? AmbiguousAddressIndicator { get; set; }
    public string? NoCandidatesIndicator { get; set; }
    public XavAddressClassification? AddressClassification { get; set; }
    public List<XavCandidate> Candidate { get; set; } = new();
}

public class XavAddressClassification
{
    public string? Code { get; set; }
    public string? Description { get; set; }
}

public class XavCandidate
{
    public XavAddressClassification? AddressClassification { get; set; }
    public XavCandidateAddressKeyFormat AddressKeyFormat { get; set; } = new();
}

public class XavCandidateAddressKeyFormat
{
    public List<string> AddressLine { get; set; } = new();
    public string? PoliticalDivision2 { get; set; }
    public string? PoliticalDivision1 { get; set; }
    public string? PostcodePrimaryLow { get; set; }
    // The ZIP+4 extension — per the spec, only ever returned in the
    // candidate list (an exact match still comes back as one candidate).
    public string? PostcodeExtendedLow { get; set; }
    public string? CountryCode { get; set; }
}
