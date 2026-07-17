using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PhotoSort.Data.Repositories;
using PhotoSort.Models;
using PhotoSort.Services;

namespace PhotoSort.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly INavigationService _navigationService;
    private readonly IFolderRepository _folderRepository;
    private readonly IFolderPickerService _folderPickerService;
    private readonly IGalleryDataService _galleryDataService;
    private readonly ILogger<MainViewModel> _logger;

    public MainViewModel(
        INavigationService navigationService,
        IFolderRepository folderRepository,
        IFolderPickerService folderPickerService,
        IGalleryDataService galleryDataService,
        ILogger<MainViewModel> logger)
    {
        _navigationService = navigationService;
        _folderRepository = folderRepository;
        _folderPickerService = folderPickerService;
        _galleryDataService = galleryDataService;
        _logger = logger;

        _navigationService.NavigationChanged += OnNavigationChanged;
        _navigationService.NavigateTo<WelcomeViewModel>();

        _ = LoadFoldersAsync();
    }

    [ObservableProperty]
    private object? _currentViewModel;

    public ObservableCollection<SidebarFolderItem> Folders { get; } = [];

    public event EventHandler<SidebarFolderItem?>? FolderSelected;

    public async Task LoadFoldersAsync()
    {
        try
        {
            var folders = await _folderRepository.GetAllAsync();
            Folders.Clear();
            foreach (var folder in folders)
            {
                Folders.Add(new SidebarFolderItem
                {
                    Id = folder.Id,
                    DisplayName = System.IO.Path.GetFileName(folder.FolderPath),
                    FolderPath = folder.FolderPath,
                    PhotoCount = folder.Photos.Count
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load folders");
        }
    }

    [RelayCommand]
    private async Task AddFolderAsync()
    {
        var path = _folderPickerService.PickFolder();
        if (string.IsNullOrEmpty(path))
            return;

        try
        {
            if (await _folderRepository.ExistsByPathAsync(path))
                return;

            var folder = new Folder { FolderPath = path };
            await _folderRepository.AddAsync(folder);

            Folders.Add(new SidebarFolderItem
            {
                Id = folder.Id,
                DisplayName = System.IO.Path.GetFileName(path),
                FolderPath = path,
                PhotoCount = 0
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add folder");
        }
    }

    [RelayCommand]
    private void SelectFolder(SidebarFolderItem? folder)
    {
        _galleryDataService.CurrentFolderId = folder?.Id;
        FolderSelected?.Invoke(this, folder);

        if (_navigationService.CurrentViewModel is not GalleryViewModel)
        {
            _navigationService.NavigateTo<GalleryViewModel>();
        }
    }

    [RelayCommand]
    private void NavigateToWelcome()
    {
        _navigationService.NavigateTo<WelcomeViewModel>();
    }

    public void ClearFolderSelection()
    {
        _galleryDataService.CurrentFolderId = null;
        foreach (var f in Folders)
            f.IsSelected = false;
    }

    private void OnNavigationChanged(object? sender, EventArgs e)
    {
        CurrentViewModel = _navigationService.CurrentViewModel;
    }
}
