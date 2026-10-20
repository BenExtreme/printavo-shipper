namespace PrintavoShipper.App.Services;

/// <summary>Which UPS rate "Add Shipping Cost To Invoice" bases the invoice's Shipping fee on.</summary>
public enum ShippingFeeRateBasis
{
    Negotiated = 0,
    Published = 1
}

/// <summary>
/// The shop's rule for what a customer is charged for shipping: the
/// negotiated rate plus the markup set in Settings > Shipping Defaults, or
/// UPS's published rate — except when the published rate isn't actually
/// higher than the negotiated one (UPS gave no discount on that quote), which
/// falls back to the same negotiated + markup.
/// </summary>
public static class ShippingFeeCalculator
{
    /// <summary>Loaded from AppSettings.ShippingFeeMarkupPercent by ShopSettingsCache.</summary>
    public static decimal MarkupPercent { get; set; }

    public static decimal Calculate(decimal negotiated, decimal? published, ShippingFeeRateBasis basis)
    {
        if (basis == ShippingFeeRateBasis.Published && published is { } p && p > negotiated)
        {
            return p;
        }

        return Math.Round(negotiated * (1 + MarkupPercent / 100m), 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>Whether Calculate would charge the published rate as-is (vs. the negotiated + markup fallback).</summary>
    public static bool UsesPublishedRate(decimal negotiated, decimal? published, ShippingFeeRateBasis basis) =>
        basis == ShippingFeeRateBasis.Published && published is { } p && p > negotiated;
}
