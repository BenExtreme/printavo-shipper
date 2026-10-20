namespace PrintavoShipper.App.ViewModels;

/// <summary>XAML-friendly wrapper around a UPS service level code/name pair (ComboBox binding doesn't play well with ValueTuple).</summary>
public record ServiceLevelOption(string Code, string Name)
{
    public override string ToString() => Name;
}
