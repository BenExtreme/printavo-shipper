using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Data;

namespace PrintavoShipper.App.Converters;

/// <summary>
/// Prepares text (Printavo status names) for an emoji:TextBlock (Emoji.Wpf),
/// which draws emoji in color from the system emoji font — WPF's own text
/// rendering can't paint color glyphs at all (colored squares show as empty
/// "tofu" boxes, everything else as black-and-white outlines).
/// </summary>
public static class EmojiDisplayText
{
    // U+FE0F (emoji variation selector) after a character like ❗ ⭐ ⚫ makes
    // Emoji.Wpf draw a stray tofu box after the emoji — confirmed rendering
    // real status names. The emoji draws in color without it, so it's
    // dropped, except inside a ZWJ sequence (FE0F followed by U+200D), where
    // it's part of a combined emoji.
    private static readonly Regex StrayVariationSelector = new("️(?!‍)", RegexOptions.Compiled);

    public static string Prepare(string? text) =>
        string.IsNullOrEmpty(text) ? string.Empty : StrayVariationSelector.Replace(text, string.Empty);
}

public class EmojiDisplayTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => EmojiDisplayText.Prepare(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
