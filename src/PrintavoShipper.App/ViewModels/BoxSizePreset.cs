using PrintavoShipper.Core.Models;

namespace PrintavoShipper.App.ViewModels;

/// <summary>The shop's standard box sizes (Settings > Shipping Defaults, loaded via ShopSettingsCache), offered as quick-fill presets on a package row — picking one just fills the weight/dimension fields, which stay freely editable afterward.</summary>
public static class BoxSizePresets
{
    public static IReadOnlyList<BoxSizePreset> All { get; private set; } = new List<BoxSizePreset>();

    /// <summary>Each new package row starts on the first preset, or blank if none are configured.</summary>
    public static BoxSizePreset? Default => All.FirstOrDefault();

    public static void Load(IEnumerable<BoxSizePreset> presets) => All = presets.ToList();
}
