using System.Globalization;
using System.Windows;
using System.Windows.Data;
using PrintavoShipper.Core.Models;

namespace PrintavoShipper.App.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class InverseNullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is null ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int count && count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Inverse of CountToVisibilityConverter — visible only when the count is exactly 0, for an empty-state message next to a list that otherwise renders nothing.</summary>
public class InverseCountToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int count && count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class BoolToDefaultLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? " (default)" : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Shows "Default" in place of the address's own label when it's the default — the label text (e.g. "From Printavo") isn't useful once flagged as default.</summary>
public class LabelOrDefaultConverter : IMultiValueConverter
{
    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
        => values.Length == 2 && values[1] is true ? "Default" : values[0] as string ?? string.Empty;

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Renders a ship-from address as a single line (e.g. "123 Main St, Springfield, IL 62704") for compact display under/inside the Ship From picker.</summary>
public class ShipFromAddressToOneLineConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ContractorShipFromAddress address)
        {
            return string.Empty;
        }

        var street = string.IsNullOrWhiteSpace(address.Address2)
            ? address.Address1
            : $"{address.Address1} {address.Address2}";

        return $"{street}, {address.City}, {address.State} {address.PostalCode}";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Shows Reprint/Void in Shipment History only for a shipment that was actually purchased — nothing to reprint/void for Draft/RateQuoted/Voided/Error rows.</summary>
public class PurchasedStatusToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ShipmentStatus.Purchased ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Turns an ItemsControl's zero-based AlternationIndex into a 1-based "N." row number label, for the package list's box-count-at-a-glance numbering.</summary>
public class IndexToRowNumberConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int index ? $"{index + 1}." : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>"(4 shipments)"/"(1 shipment)" suffix for Shipment History's date-group headers, bound to a CollectionViewGroup.ItemCount.</summary>
public class ItemCountToShipmentsLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int count ? $" ({count} shipment{(count == 1 ? "" : "s")})" : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Sets Shipment History's date-group headers expanded only for today's group, collapsed for every other date — bound OneWay to a CollectionViewGroup.Name (a ShippedDateGroupKey DateTime, already local — see ShipmentRecord.ShippedDateGroupKey); must stay OneWay since Name has no setter (see CLAUDE.md gotcha #8) and Expander.IsExpanded binds TwoWay by default.</summary>
public class DateIsTodayToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is DateTime date && date == DateTime.Today;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Two-way Visibility ↔ bool, for a ToggleButton.IsChecked bound to a DataGridRow's DetailsVisibility (Shipment History's per-row expand chevron) — unlike the other converters here, ConvertBack is real since that binding writes back.</summary>
public class VisibilityToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;
}

/// <summary>Primary (accent-colored) once a rate-quote button's own item equals ShipmentCardViewModel.AppliedRateQuote, Secondary otherwise — marks which quote was applied (records compare by value, so a re-fetched-but-identical quote still matches).</summary>
public class RateQuoteAppearanceConverter : IMultiValueConverter
{
    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
        => values.Length == 2 && Equals(values[0], values[1])
            ? Wpf.Ui.Controls.ControlAppearance.Primary
            : Wpf.Ui.Controls.ControlAppearance.Secondary;

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
