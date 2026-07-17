using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoSort.Models;
using PhotoSort.Services;

namespace PhotoSort.ViewModels;

public partial class WelcomeViewModel : ObservableObject
{
    private readonly IFolderPickerService _folderPickerService;
    private readonly ILibraryImportOrchestrator _importOrchestrator;
    private readonly INavigationService _navigationService;

    public WelcomeViewModel(
        IFolderPickerService folderPickerService,
        ILibraryImportOrchestrator importOrchestrator,
        INavigationService navigationService)
    {
        _folderPickerService = folderPickerService;
        _importOrchestrator = importOrchestrator;
        _navigationService = navigationService;
    }

    [ObservableProperty]
    private string _title = "Rediscover your cherished moments.";

    [ObservableProperty]
    private string _subtitle = "PhotoSort helps you organize, explore, and relive your memories — all stored safely on your device.";

    [ObservableProperty]
    private string _importButtonText = "Import Folder";

    [ObservableProperty]
    private string _tourButtonText = "Take a Guided Tour";

    [ObservableProperty]
    private string? _selectedFolderPath;

    [ObservableProperty]
    private int _importedFolderCount;

    [ObservableProperty]
    private bool _isImporting;

    [ObservableProperty]
    private string _importStatusText = string.Empty;

    [ObservableProperty]
    private double _importPercentage;

    [ObservableProperty]
    private string _importStageText = string.Empty;

    public ObservableCollection<string> ImportedFolders { get; } = [];

    [RelayCommand]
    private async Task ImportFolderAsync()
    {
        if (IsImporting)
            return;

        var folderPath = _folderPickerService.PickFolder();

        if (string.IsNullOrEmpty(folderPath))
            return;

        var normalizedPath = Path.GetFullPath(folderPath);

        if (ImportedFolders.Contains(normalizedPath, StringComparer.OrdinalIgnoreCase))
            return;

        ImportedFolders.Add(normalizedPath);
        SelectedFolderPath = normalizedPath;
        ImportedFolderCount = ImportedFolders.Count;

        await StartImportAsync(normalizedPath);
    }

    private async Task StartImportAsync(string folderPath)
    {
        IsImporting = true;
        ImportButtonText = "Importing...";
        ImportStatusText = "Discovering files...";

        try
        {
            var progress = new Progress<LibraryImportProgress>(p =>
            {
                ImportStatusText = p.StatusMessage;
                ImportPercentage = p.Percentage;
                ImportStageText = p.CurrentStage switch
                {
                    "Discovering" => "Discovering files...",
                    "Indexing" => "Indexing files...",
                    "Metadata" => "Extracting metadata...",
                    "Complete" => "Import complete!",
                    "Paused" => "Paused",
                    _ => "Preparing..."
                };
            });

            var result = await _importOrchestrator.ImportFolderAsync(
                folderPath, progress).ConfigureAwait(true);

            if (result.WasCancelled)
            {
                ImportStatusText = "Import cancelled.";
                ImportPercentage = 0;
                ImportStageText = string.Empty;
            }
            else if (result.TotalFailed > 0)
            {
                ImportStatusText = $"Import done with {result.TotalFailed} errors.";
                ImportStageText = string.Empty;
            }
            else
            {
                ImportPercentage = 100;
                ImportStageText = "Loading gallery...";
                ImportStatusText = "Import complete! Loading gallery...";
                await Task.Delay(500).ConfigureAwait(true);
                _navigationService.NavigateTo<GalleryViewModel>();
            }
        }
        catch (Exception ex)
        {
            ImportStatusText = $"Import failed: {ex.Message}";
            ImportPercentage = 0;
            ImportStageText = string.Empty;
        }
        finally
        {
            IsImporting = false;
            ImportButtonText = "Import Folder";
        }
    }

    [RelayCommand]
    private void RemoveFolder(string? folderPath)
    {
        if (string.IsNullOrEmpty(folderPath))
            return;

        ImportedFolders.Remove(folderPath);

        if (string.Equals(SelectedFolderPath, folderPath, StringComparison.OrdinalIgnoreCase))
        {
            SelectedFolderPath = ImportedFolders.Count > 0
                ? ImportedFolders[^1]
                : null;
        }

        ImportedFolderCount = ImportedFolders.Count;
    }

    [RelayCommand]
    private void TakeTour()
    {
    }

}
