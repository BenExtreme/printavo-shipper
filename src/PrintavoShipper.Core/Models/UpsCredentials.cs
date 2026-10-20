namespace PrintavoShipper.Core.Models;

// Which UPS shipper account manifests/pays for a given shipment is chosen
// per-shipment now (see OwnUpsAccount) rather than fixed here — this record
// only carries what's needed to authenticate and pick an environment.
public record UpsCredentials(
    string ClientId,
    string ClientSecret,
    UpsEnvironment Environment,
    string? MerchantId);

public record PrintavoCredentials(string Email, string ApiToken);
