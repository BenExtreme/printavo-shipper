using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using PrintavoShipper.Core.Interfaces;
using PrintavoShipper.Core.Models;
using PrintavoShipper.Printavo.Dto;

namespace PrintavoShipper.App.Services;

/// <summary>
/// Builds and prints Pick-up box labels. Branding follows what Printavo's
/// own box label does: the order's Printavo contractor profile logo + phone
/// when it has one, otherwise the shop's own logo from Settings
/// + the shop phone from the default ship-from address.
/// </summary>
public class BoxLabelService : IBoxLabelService
{
    /// <summary>Downloaded contractor logos for the session, by URL — the same handful of contractors come up over and over.</summary>
    private static readonly ConcurrentDictionary<string, byte[]> LogoCache = new();

    private readonly HttpClient _httpClient;
    private readonly ISettingsService _settingsService;
    private readonly ILabelPrintService _labelPrintService;
    private readonly ILogger<BoxLabelService> _logger;

    public BoxLabelService(HttpClient httpClient, ISettingsService settingsService, ILabelPrintService labelPrintService, ILogger<BoxLabelService> logger)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(15);
        _settingsService = settingsService;
        _labelPrintService = labelPrintService;
        _logger = logger;
    }

    public async Task<string?> PrintBoxLabelsAsync(PrintavoOrderDetail order, int boxCount, CancellationToken ct = default)
    {
        var settings = await _settingsService.GetSettingsAsync(ct);
        if (string.IsNullOrWhiteSpace(settings.SelectedPrinterName))
        {
            throw new InvalidOperationException("No printer selected in Settings.");
        }

        string? warning = null;
        byte[]? logo;
        string? logoFallbackText;
        string? phone;
        string? customerName = null;

        if (!string.IsNullOrWhiteSpace(order.ContractorLogoUrl))
        {
            logo = await TryDownloadLogoAsync(order.ContractorLogoUrl, ct);
            if (logo is null)
            {
                warning = $"Couldn't download {order.ContractorProfileName ?? "the contractor"}'s logo from Printavo — printed its name instead.";
            }

            logoFallbackText = order.ContractorProfileName;
            phone = order.ContractorPhone;
        }
        else
        {
            logo = settings.ShopLogoImage;
            logoFallbackText = settings.DefaultShipFromCompanyName;
            phone = settings.DefaultShipFromPhoneNumber;

            // A shop-branded label would otherwise say nothing about whose box
            // it is, so it gets the customer's name in big type.
            customerName = new[] { order.CustomerCompanyName, order.BillingAddress?.CustomerName, order.ShippingAddress?.CustomerName }
                .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n))?.Trim();
        }

        var content = new BoxLabelContent(
            logo,
            logoFallbackText,
            phone,
            order.VisualId,
            order.Nickname,
            order.PoNumber,
            // dueAt is a UTC timestamp (e.g. 2026-10-17T00:00Z for what
            // Printavo shows as "Oct 16") — local time gives the right day.
            order.DueAt?.ToLocalTime().ToString("MMM d", CultureInfo.CurrentCulture),
            order.CustomerDueDate?.ToString("MMM d", CultureInfo.CurrentCulture),
            order.DeliveryMethodName,
            customerName);

        await _labelPrintService.PrintBoxLabelsAsync(content, boxCount, settings.SelectedPrinterName!, settings.LabelPaperMode, ct);
        return warning;
    }

    private async Task<byte[]?> TryDownloadLogoAsync(string url, CancellationToken ct)
    {
        if (LogoCache.TryGetValue(url, out var cached))
        {
            return cached;
        }

        try
        {
            var bytes = await _httpClient.GetByteArrayAsync(url, ct);
            LogoCache[url] = bytes;
            return bytes;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Couldn't download contractor logo {Url}", url);
            return null;
        }
    }
}
