namespace PrintavoShipper.Core.Models;

// Which FedEx account pays for a given shipment is chosen per-shipment (see
// OwnFedExAccount / a contractor's ContractorFedExBillingAccount) rather than
// fixed here — this record only carries what's needed to authenticate and
// pick an environment. Mirrors UpsCredentials.
public record FedExCredentials(
    string ClientId,
    string ClientSecret,
    FedExEnvironment Environment,
    string? AccountNumber);
