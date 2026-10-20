using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PrintavoShipper.Core.Data;
using PrintavoShipper.Core.Models;

namespace PrintavoShipper.App.ViewModels;

/// <summary>
/// Read-only browser for a saved shipment archive JSON file (written by
/// ShipmentArchiveService) — pick a file, search/view its orders. Never
/// writes back to the live database; the archive is a frozen snapshot.
/// </summary>
public partial class ArchiveViewerViewModel : ObservableObject
{
    public record ArchiveFileOption(string FileName, string FullPath);

    private readonly ILogger<ArchiveViewerViewModel> _logger;

    private List<ShipmentRecord> _allRecords = new();

    [ObservableProperty]
    private ObservableCollection<ArchiveFileOption> archiveFiles = new();

    [ObservableProperty]
    private ArchiveFileOption? selectedArchiveFile;

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<ShipmentRecord> shipments = new();

    [ObservableProperty]
    private ICollectionView? shipmentsView;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private string? statusMessage;

    public ArchiveViewerViewModel(ILogger<ArchiveViewerViewModel> logger)
    {
        _logger = logger;
        RefreshArchiveFiles();
    }

    [RelayCommand]
    private void RefreshArchiveFiles()
    {
        var previouslySelected = SelectedArchiveFile;
        ErrorMessage = null;

        try
        {
            var folder = DbPathProvider.GetArchiveFolder();
            var files = Directory.GetFiles(folder, "*.json")
                .OrderByDescending(f => f)
                .Select(f => new ArchiveFileOption(Path.GetFileName(f), f))
                .ToList();

            ArchiveFiles = new ObservableCollection<ArchiveFileOption>(files);

            if (files.Count == 0)
            {
                StatusMessage = $"No archive files found in {folder}.";
                SelectedArchiveFile = null;
                return;
            }

            var stillPresent = previouslySelected is not null
                ? files.FirstOrDefault(f => f.FullPath == previouslySelected.FullPath)
                : null;
            SelectedArchiveFile = stillPresent ?? files[0];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Couldn't list archive files.");
            ErrorMessage = $"Couldn't list archive files: {ex.Message}";
        }
    }

    partial void OnSelectedArchiveFileChanged(ArchiveFileOption? value)
    {
        _ = LoadSelectedFileAsync(value);
    }

    private async Task LoadSelectedFileAsync(ArchiveFileOption? file)
    {
        IsLoading = true;
        ErrorMessage = null;
        StatusMessage = null;
        try
        {
            if (file is null)
            {
                _allRecords = new List<ShipmentRecord>();
            }
            else
            {
                var json = await File.ReadAllTextAsync(file.FullPath);
                _allRecords = JsonSerializer.Deserialize<List<ShipmentRecord>>(json) ?? new List<ShipmentRecord>();
            }

            ApplySearchFilter();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Couldn't read archive file {FilePath}.", file?.FullPath);
            ErrorMessage = $"Couldn't read archive file: {ex.Message}";
            _allRecords = new List<ShipmentRecord>();
            ApplySearchFilter();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void Search() => ApplySearchFilter();

    private void ApplySearchFilter()
    {
        var query = SearchText.Trim();
        var filtered = string.IsNullOrWhiteSpace(query)
            ? _allRecords
            : _allRecords.Where(r =>
                Matches(r.PrintavoVisualId, query) ||
                Matches(r.FirstTrackingNumber, query) ||
                Matches(r.ShipToCompanyName, query) ||
                Matches(r.ShipToLabel, query));

        Shipments = new ObservableCollection<ShipmentRecord>(filtered);
    }

    private static bool Matches(string? field, string query) =>
        field is not null && field.Contains(query, StringComparison.OrdinalIgnoreCase);

    partial void OnShipmentsChanged(ObservableCollection<ShipmentRecord> value)
    {
        var view = CollectionViewSource.GetDefaultView(value);
        view.SortDescriptions.Add(new SortDescription(nameof(ShipmentRecord.ShippedDateGroupKey), ListSortDirection.Descending));
        view.SortDescriptions.Add(new SortDescription(nameof(ShipmentRecord.Id), ListSortDirection.Descending));
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ShipmentRecord.ShippedDateGroupKey)));
        ShipmentsView = view;
    }
}
