namespace PrintavoShipper.FedEx.Dto;

// JSON wire format for POST /oauth/token. Unlike UPS (HTTP Basic auth
// header, expires_in returned as a string), FedEx's OAuth token endpoint
// takes client_id/client_secret in the form-urlencoded body and returns
// expires_in as a JSON number — both confirmed against FedEx's published
// Authorization API documentation.

public class FedExTokenResponse
{
    public string? AccessToken { get; set; }
    public string? TokenType { get; set; }
    public int ExpiresIn { get; set; }
    public string? Scope { get; set; }
}
