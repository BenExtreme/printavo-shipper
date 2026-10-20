using PrintavoShipper.Core.Interfaces;
using PrintavoShipper.Core.Models;
using PrintavoShipper.Printavo;
using PrintavoShipper.Printavo.Dto;

namespace PrintavoShipper.App.Services;

public class ContractorProvisioningService : IContractorProvisioningService
{
    private readonly IContractorRepository _contractorRepository;
    private readonly IPrintavoClient _printavoClient;

    public ContractorProvisioningService(IContractorRepository contractorRepository, IPrintavoClient printavoClient)
    {
        _contractorRepository = contractorRepository;
        _printavoClient = printavoClient;
    }

    public async Task<Contractor> ImportFromPrintavoAsync(string printavoCustomerId, string companyName, CancellationToken ct = default)
    {
        var contractor = await _contractorRepository.UpsertFromPrintavoAsync(printavoCustomerId, companyName, ct);

        if (contractor.ShipFromAddresses.Count > 0)
        {
            return contractor;
        }

        PrintavoCustomerDetail? detail;
        try
        {
            detail = await _printavoClient.GetCustomerAsync(printavoCustomerId, ct);
        }
        catch
        {
            // Best-effort pre-fill — a Printavo hiccup here shouldn't block getting to the contractor at all.
            return contractor;
        }

        var source = detail?.ShippingAddress?.HasContent == true ? detail.ShippingAddress
            : detail?.BillingAddress?.HasContent == true ? detail.BillingAddress
            : null;

        if (source is null)
        {
            return contractor;
        }

        // Printavo doesn't length-constrain any of these fields, but UPS
        // does — clip defensively so a long Printavo record can't produce
        // a saved address that would later fail the UPS Shipping API call.
        var saved = await _contractorRepository.SaveShipFromAddressAsync(new ContractorShipFromAddress
        {
            ContractorId = contractor.Id,
            Label = "From Printavo",
            CompanyName = UpsAddressLimits.Truncate(
                string.IsNullOrWhiteSpace(source.CompanyName) ? companyName : source.CompanyName,
                UpsAddressLimits.NameMaxLength),
            Address1 = UpsAddressLimits.Truncate(source.Address1, UpsAddressLimits.AddressLineMaxLength) ?? string.Empty,
            Address2 = UpsAddressLimits.Truncate(source.Address2, UpsAddressLimits.AddressLineMaxLength),
            City = UpsAddressLimits.Truncate(source.City, UpsAddressLimits.CityMaxLength) ?? string.Empty,
            State = UpsAddressLimits.Truncate(
                string.IsNullOrWhiteSpace(source.StateIso) ? source.State : source.StateIso,
                UpsAddressLimits.StateProvinceCodeMaxLength) ?? string.Empty,
            PostalCode = UpsAddressLimits.Truncate(source.ZipCode, UpsAddressLimits.PostalCodeMaxLength) ?? string.Empty,
            CountryCode = UpsAddressLimits.Truncate(
                string.IsNullOrWhiteSpace(source.CountryIso) ? "US" : source.CountryIso,
                UpsAddressLimits.CountryCodeLength) ?? "US",
            IsDefault = true
        }, ct);

        contractor.ShipFromAddresses.Add(saved);

        return contractor;
    }
}
