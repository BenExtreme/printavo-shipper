using PrintavoShipper.Core.Models;
using PrintavoShipper.Printavo.Dto;
using PrintavoShipper.Ups;
using static PrintavoShipper.Core.Models.UpsAddressLimits;

namespace PrintavoShipper.App.Services;

/// <summary>
/// Maps Printavo/local address shapes to UPS's UpsPartyInput, truncating to
/// UPS's field-length limits along the way — Printavo doesn't enforce any of
/// these on ship-to data, so a long Printavo address could otherwise fail at
/// the UPS Shipping API call (see UpsAddressLimits).
/// </summary>
public static class UpsPartyMapper
{
    public static UpsPartyInput FromShipFrom(ContractorShipFromAddress address) => new(
        Truncate(address.CompanyName, NameMaxLength) ?? "Ship From",
        Truncate(address.AttentionName, AttentionNameMaxLength),
        Truncate(address.Address1, AddressLineMaxLength) ?? string.Empty,
        Truncate(address.Address2, AddressLineMaxLength),
        Truncate(address.City, CityMaxLength) ?? string.Empty,
        Truncate(address.State, StateProvinceCodeMaxLength) ?? string.Empty,
        Truncate(address.PostalCode, PostalCodeMaxLength) ?? string.Empty,
        Truncate(address.CountryCode, CountryCodeLength) ?? "US",
        Truncate(address.PhoneNumber, PhoneNumberMaxLength));

    // Neither PrintavoAddress nor PrintavoCustomAddress carries a phone
    // number of its own — the closest thing Printavo has is the order's
    // billing contact phone (PrintavoOrderDetail.ContactPhone), passed in
    // by the caller since it isn't part of either address shape.
    public static UpsPartyInput FromPrintavoAddress(PrintavoAddress a, string? phoneNumber = null) =>
        Build(a.CompanyName, a.CustomerName, a.Address1, a.Address2, a.City, a.State, a.StateIso, a.ZipCode, a.CountryIso, phoneNumber);

    public static UpsPartyInput FromCustomAddress(PrintavoCustomAddress a, string? phoneNumber = null) =>
        Build(a.CompanyName, a.CustomerName, a.Address1, a.Address2, a.City, a.State, a.StateIso, a.ZipCode, a.CountryIso, phoneNumber);

    private static UpsPartyInput Build(
        string? companyName, string? customerName, string address1, string? address2,
        string city, string state, string stateIso, string zip, string countryIso, string? phoneNumber)
    {
        var name = !string.IsNullOrWhiteSpace(companyName) ? companyName : customerName;
        var attention = !string.IsNullOrWhiteSpace(companyName) ? customerName : null;
        var stateCode = !string.IsNullOrWhiteSpace(stateIso) ? stateIso : state;
        var countryCode = !string.IsNullOrWhiteSpace(countryIso) ? countryIso : "US";

        return new UpsPartyInput(
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
