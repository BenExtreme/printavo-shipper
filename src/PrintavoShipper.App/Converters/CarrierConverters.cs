using System.Globalization;
using System.Windows.Data;
using PrintavoShipper.Core.Models;

namespace PrintavoShipper.App.Converters;

/// <summary>
/// Binds a ShipmentCarrier-valued property to a group of RadioButtons —
/// each RadioButton's IsChecked converts true/false against the carrier
/// named in its ConverterParameter ("Ups"/"FedEx"), and checking one writes
/// that carrier back to the source property.
/// </summary>
public class CarrierToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is ShipmentCarrier carrier && parameter is string target
        && Enum.TryParse<ShipmentCarrier>(target, out var parsed) && carrier == parsed;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true && parameter is string target && Enum.TryParse<ShipmentCarrier>(target, out var parsed)
            ? parsed
            : Binding.DoNothing;
}
