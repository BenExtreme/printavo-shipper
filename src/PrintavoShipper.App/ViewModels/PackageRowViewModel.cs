using CommunityToolkit.Mvvm.ComponentModel;
using PrintavoShipper.Core.Models;

namespace PrintavoShipper.App.ViewModels;

/// <summary>One editable package row (box size preset + weight/dimensions) within a ShipmentCardViewModel.</summary>
public partial class PackageRowViewModel : ObservableObject
{
    private bool _suppressPresetFill;

    [ObservableProperty]
    private BoxSizePreset? selectedBoxSize = BoxSizePresets.Default;

    [ObservableProperty]
    private decimal weightLbs = BoxSizePresets.Default?.WeightLbs ?? 0;

    [ObservableProperty]
    private decimal? lengthIn = BoxSizePresets.Default?.LengthIn;

    [ObservableProperty]
    private decimal? widthIn = BoxSizePresets.Default?.WidthIn;

    [ObservableProperty]
    private decimal? heightIn = BoxSizePresets.Default?.HeightIn;

    /// <summary>How many extra copies to add when Clone is clicked on this row — 1 clone by default (the prior fixed behavior).</summary>
    [ObservableProperty]
    private int cloneCount = 1;

    // Picking a preset just fills the fields below — they stay freely
    // editable afterward and won't snap back if the box size dropdown
    // still shows the last-picked preset.
    partial void OnSelectedBoxSizeChanged(BoxSizePreset? value)
    {
        if (_suppressPresetFill || value is null)
        {
            return;
        }

        WeightLbs = value.WeightLbs;
        LengthIn = value.LengthIn;
        WidthIn = value.WidthIn;
        HeightIn = value.HeightIn;
    }

    /// <summary>Copies this row's exact current values — including any manual edits made after a preset was picked — into a new row, without the preset-fill side effect above overwriting them.</summary>
    public PackageRowViewModel Clone()
    {
        var clone = new PackageRowViewModel { _suppressPresetFill = true };
        clone.SelectedBoxSize = SelectedBoxSize;
        clone.WeightLbs = WeightLbs;
        clone.LengthIn = LengthIn;
        clone.WidthIn = WidthIn;
        clone.HeightIn = HeightIn;
        clone._suppressPresetFill = false;
        return clone;
    }
}
