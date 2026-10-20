using PrintavoShipper.Printavo.Dto;

namespace PrintavoShipper.App.Services;

public interface IBoxLabelService
{
    /// <summary>
    /// Prints boxCount box labels for a Pick-up order to the Settings label
    /// printer. Throws if there's no printer or printing fails. Returns a
    /// non-fatal warning (e.g. the contractor logo couldn't be downloaded,
    /// so the labels went out with its name in text instead), or null.
    /// </summary>
    Task<string?> PrintBoxLabelsAsync(PrintavoOrderDetail order, int boxCount, CancellationToken ct = default);
}
