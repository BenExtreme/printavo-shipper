using PrintavoShipper.Core.Models;
using PrintavoShipper.FedEx;
using PrintavoShipper.Printavo.Dto;
using static PrintavoShipper.Core.Models.UpsAddressLimits;

namespace PrintavoShipper.App.Services;

/// <summary>
/// Maps Printavo/local address shapes to FedEx's FedExPartyInput, mirroring
/// UpsPartyMapper. Reuses UpsAddressLimits for truncation — FedEx's own
/// field-length limits haven't been confirmed against a live sandbox call
/// yet (see the header comment on FedExShipWireDto.cs), so this is a
/// reasonable starting point, not a confirmed FedEx-specific limit.
/// </summary>
public static class FedExPartyMapper
{
    public static FedExPartyInput FromShipFrom(ContractorShipFromAddress address) => new(
        Truncate(address.CompanyName, NameMaxLength) ?? "Ship From",
        Truncate(address.AttentionName, AttentionNameMaxLength),
        Truncate(address.Address1, AddressLineMaxLength) ?? string.Empty,
        Truncate(address.Address2, AddressLineMaxLength),
        Truncate(address.City, CityMaxLength) ?? string.Empty,
        Truncate(address.State, StateProvinceCodeMaxLength) ?? string.Empty,
        Truncate(address.PostalCode, PostalCodeMaxLength) ?? string.Empty,
        Truncate(address.CountryCode, CountryCodeLength) ?? "US",
        Truncate(address.PhoneNumber, PhoneNumberMaxLength));

    public static FedExPartyInput FromPrintavoAddress(PrintavoAddress a, string? phoneNumber = null) =>
        Build(a.CompanyName, a.CustomerName, a.Address1, a.Address2, a.City, a.State, a.StateIso, a.ZipCode, a.CountryIso, phoneNumber);

    public static FedExPartyInput FromCustomAddress(PrintavoCustomAddress a, string? phoneNumber = null) =>
        Build(a.CompanyName, a.CustomerName, a.Address1, a.Address2, a.City, a.State, a.StateIso, a.ZipCode, a.CountryIso, phoneNumber);

    private static FedExPartyInput Build(
        string? companyName, string? customerName, string address1, string? address2,
        string city, string state, string stateIso, string zip, string countryIso, string? phoneNumber)
    {
        var name = !string.IsNullOrWhiteSpace(companyName) ? companyName : customerName;
        var attention = !string.IsNullOrWhiteSpace(companyName) ? customerName : null;
        var stateCode = !string.IsNullOrWhiteSpace(stateIso) ? stateIso : state;
        var countryCode = !string.IsNullOrWhiteSpace(countryIso) ? countryIso : "US";

        return new FedExPartyInput(
            Truncate(name, NameMaxLength) ?? "Customer",
            Truncate(attention, AttentionNameMaxLength),
            Truncate(address1, AddressLineMaxLength) ?? string.Empty,
            Truncate(address2, AddressLineMaxLength),
            Truncate(city, CityMaxLength) ?? string.Empty,
            Truncate(stateCode, StateProvinceCodeMaxLength) ?? string.Empty,
            Truncate(zip, PostalCodeMaxLength) ?? string.Empty,
            Truncate(countryCode, CountryCodeLength) ?? "US",
            Truncate(phoneNumber, PhoneNumberMaxLength));
    }
}
