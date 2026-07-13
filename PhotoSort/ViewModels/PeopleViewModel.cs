using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PhotoSort.Models;
using PhotoSort.Services;

namespace PhotoSort.ViewModels;

public partial class PeopleViewModel : ObservableObject, IDisposable
{
    private readonly IPeopleService _peopleService;
    private readonly INavigationService _navigationService;
    private readonly ILogger<PeopleViewModel> _logger;

    private bool _disposed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentPhaseDisplay))]
    [NotifyPropertyChangedFor(nameof(CurrentPhotosProcessed))]
    [NotifyPropertyChangedFor(nameof(CurrentFacesDetected))]
    [NotifyPropertyChangedFor(nameof(CurrentEmbeddingsGenerated))]
    [NotifyPropertyChangedFor(nameof(CurrentPeopleIdentified))]
    [NotifyPropertyChangedFor(nameof(CurrentProgressPercent))]
    [NotifyPropertyChangedFor(nameof(CurrentTotalPhotos))]
    private FaceProcessingProgress _currentProgress = new()
    {
        Phase = FaceProcessingPhase.Idle
    };

    public string CurrentPhaseDisplay => CurrentProgress.PhaseDisplay;
    public int CurrentPhotosProcessed => CurrentProgress.PhotosProcessed;
    public int CurrentFacesDetected => CurrentProgress.FacesDetected;
    public int CurrentEmbeddingsGenerated => CurrentProgress.EmbeddingsGenerated;
    public int CurrentPeopleIdentified => CurrentProgress.PeopleIdentified;
    public double CurrentProgressPercent => CurrentProgress.ProgressPercent;
    public int CurrentTotalPhotos => CurrentProgress.TotalPhotos;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private bool _hasPeople;

    [ObservableProperty]
    private bool _hasSelectedPerson;

    [ObservableProperty]
    private int _selectedPersonIndex = -1;

    [ObservableProperty]
    private PersonInfo? _selectedPerson;

    [ObservableProperty]
    private string _statusMessage = "Ready to detect faces";

    [ObservableProperty]
    private string _renameText = string.Empty;

    [ObservableProperty]
    private int _totalPeopleCount;

    [ObservableProperty]
    private int _totalFaceCount;

    [ObservableProperty]
    private int _unprocessedPhotoCount;

    [ObservableProperty]
    private int _embeddedFaceCount;

    [ObservableProperty]
    private bool _isRenameMode;

    [ObservableProperty]
    private bool _isSplitMode;

    [ObservableProperty]
    private string _newPersonName = string.Empty;

    public ObservableCollection<PersonInfo> People { get; } = [];
    public ObservableCollection<FaceInfo> SelectedPersonFaces { get; } = [];
    public ObservableCollection<int> SelectedPersonIds { get; } = [];
    public ObservableCollection<int> SplitFaceIds { get; } = [];

    public PeopleViewModel(
        IPeopleService peopleService,
        INavigationService navigationService,
        ILogger<PeopleViewModel> logger)
    {
        _peopleService = peopleService;
        _navigationService = navigationService;
        _logger = logger;

        _peopleService.ProgressChanged += OnProgressChanged;
        _peopleService.PeopleChanged += OnPeopleChanged;

        _ = LoadPeopleAsync();
    }

    [RelayCommand]
    private async Task StartProcessingAsync()
    {
        if (_peopleService.IsProcessing)
            return;

        IsProcessing = true;
        IsPaused = false;
        StatusMessage = "Initializing face detection...";

        try
        {
            await _peopleService.StartProcessingAsync();
            await LoadPeopleAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start face processing");
            StatusMessage = $"Processing failed: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
            IsPaused = false;
        }
    }

    [RelayCommand]
    private void PauseProcessing()
    {
        if (!_peopleService.IsProcessing)
            return;

        _peopleService.PauseProcessing();
        IsPaused = true;
        StatusMessage = "Processing paused";
    }

    [RelayCommand]
    private void ResumeProcessing()
    {
        _peopleService.ResumeProcessing();
        IsPaused = false;
        StatusMessage = "Resuming...";
    }

    [RelayCommand]
    private void CancelProcessing()
    {
        _peopleService.CancelProcessing();
        IsProcessing = false;
        IsPaused = false;
        StatusMessage = "Processing cancelled";
    }

    [RelayCommand]
    private async Task LoadPeopleAsync()
    {
        try
        {
            StatusMessage = "Loading people...";

            var people = await _peopleService.GetPeopleAsync();
            People.Clear();
            foreach (var person in people)
            {
                People.Add(person);
            }

            HasPeople = People.Count > 0;

            TotalPeopleCount = People.Count;
            TotalFaceCount = People.Sum(p => p.FaceCount);
            UnprocessedPhotoCount = await _peopleService.GetUnprocessedPhotoCountAsync();
            EmbeddedFaceCount = await _peopleService.GetEmbeddedFaceCountAsync();

            StatusMessage = HasPeople
                ? $"Found {TotalPeopleCount} people with {TotalFaceCount} faces"
                : "No people detected yet. Start processing to detect faces.";

            if (HasPeople && SelectedPersonIndex < 0)
            {
                SelectedPersonIndex = 0;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load people");
            StatusMessage = "Failed to load people";
        }
    }

    [RelayCommand]
    private async Task SelectPersonAsync(PersonInfo? person)
    {
        if (person is null)
            return;

        SelectedPerson = person;
        HasSelectedPerson = true;
        RenameText = person.Name;
        IsRenameMode = false;
        IsSplitMode = false;
        SplitFaceIds.Clear();

        StatusMessage = $"Loading faces for {person.Name}...";

        try
        {
            var faces = await _peopleService.GetPersonFacesAsync(person.PersonId);
            SelectedPersonFaces.Clear();
            foreach (var face in faces)
            {
                SelectedPersonFaces.Add(face);
            }

            StatusMessage = $"Showing {SelectedPersonFaces.Count} faces for {person.Name}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load faces for person");
            StatusMessage = "Failed to load faces";
        }
    }

    [RelayCommand]
    private async Task RenamePersonAsync()
    {
        if (SelectedPerson is null || string.IsNullOrWhiteSpace(RenameText))
            return;

        try
        {
            await _peopleService.RenamePersonAsync(SelectedPerson.PersonId, RenameText);
            IsRenameMode = false;
            await LoadPeopleAsync();
            StatusMessage = $"Renamed to {RenameText}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rename person");
            StatusMessage = "Failed to rename person";
        }
    }

    [RelayCommand]
    private async Task MergePeopleAsync()
    {
        if (SelectedPerson is null || SelectedPersonIds.Count < 2)
            return;

        try
        {
            var mergeIds = SelectedPersonIds.Where(id => id != SelectedPerson.PersonId).ToList();
            await _peopleService.MergePeopleAsync(SelectedPerson.PersonId, mergeIds);
            SelectedPersonIds.Clear();
            await LoadPeopleAsync();
            StatusMessage = $"Merged {mergeIds.Count} people";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to merge people");
            StatusMessage = "Failed to merge people";
        }
    }

    [RelayCommand]
    private async Task SplitPersonAsync()
    {
        if (SelectedPerson is null || SplitFaceIds.Count == 0)
            return;

        try
        {
            var name = string.IsNullOrWhiteSpace(NewPersonName) ? null : NewPersonName;
            await _peopleService.SplitPersonAsync(SelectedPerson.PersonId, SplitFaceIds.ToList(), name);
            IsSplitMode = false;
            SplitFaceIds.Clear();
            NewPersonName = string.Empty;
            await LoadPeopleAsync();
            StatusMessage = $"Split {SplitFaceIds.Count} faces into new person";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to split person");
            StatusMessage = "Failed to split person";
        }
    }

    [RelayCommand]
    private async Task IgnoreFaceAsync(FaceInfo? face)
    {
        if (face is null)
            return;

        try
        {
            await _peopleService.IgnoreFaceAsync(face.FaceId);
            SelectedPersonFaces.Remove(face);
            StatusMessage = "Face ignored";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ignore face");
            StatusMessage = "Failed to ignore face";
        }
    }

    [RelayCommand]
    private async Task DeletePersonAsync()
    {
        if (SelectedPerson is null)
            return;

        try
        {
            await _peopleService.DeletePersonAsync(SelectedPerson.PersonId);
            SelectedPerson = null;
            HasSelectedPerson = false;
            SelectedPersonFaces.Clear();
            await LoadPeopleAsync();
            StatusMessage = "Person deleted";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete person");
            StatusMessage = "Failed to delete person";
        }
    }

    [RelayCommand]
    private async Task DeleteFaceAsync(FaceInfo? face)
    {
        if (face is null)
            return;

        try
        {
            await _peopleService.DeleteFaceAsync(face.FaceId);
            SelectedPersonFaces.Remove(face);
            StatusMessage = "Face deleted";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete face");
            StatusMessage = "Failed to delete face";
        }
    }

    [RelayCommand]
    private async Task ReprocessPersonAsync()
    {
        if (SelectedPerson is null)
            return;

        try
        {
            StatusMessage = $"Reprocessing {SelectedPerson.Name}...";
            await _peopleService.ReprocessPersonAsync(SelectedPerson.PersonId);
            await LoadPeopleAsync();
            StatusMessage = $"Reprocessed {SelectedPerson.Name}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reprocess person");
            StatusMessage = "Failed to reprocess person";
        }
    }

    [RelayCommand]
    private void TogglePersonSelection(PersonInfo? person)
    {
        if (person is null)
            return;

        if (SelectedPersonIds.Contains(person.PersonId))
        {
            SelectedPersonIds.Remove(person.PersonId);
        }
        else
        {
            SelectedPersonIds.Add(person.PersonId);
        }
    }

    [RelayCommand]
    private void ToggleSplitMode()
    {
        IsSplitMode = !IsSplitMode;
        SplitFaceIds.Clear();
        NewPersonName = string.Empty;
    }

    [RelayCommand]
    private void ToggleFaceSplitSelection(FaceInfo? face)
    {
        if (face is null)
            return;

        if (SplitFaceIds.Contains(face.FaceId))
        {
            SplitFaceIds.Remove(face.FaceId);
        }
        else
        {
            SplitFaceIds.Add(face.FaceId);
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        _navigationService.NavigateTo<GalleryViewModel>();
    }

    partial void OnSelectedPersonIndexChanged(int value)
    {
        if (value >= 0 && value < People.Count)
        {
            SelectedPerson = People[value];
            _ = SelectPersonAsync(SelectedPerson);
        }
        else
        {
            SelectedPerson = null;
            HasSelectedPerson = false;
            SelectedPersonFaces.Clear();
        }
    }

    private void OnProgressChanged(object? sender, FaceProcessingProgress progress)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            CurrentProgress = progress;

            OnPropertyChanged(nameof(CurrentPhaseDisplay));
            OnPropertyChanged(nameof(CurrentPhotosProcessed));
            OnPropertyChanged(nameof(CurrentFacesDetected));
            OnPropertyChanged(nameof(CurrentEmbeddingsGenerated));
            OnPropertyChanged(nameof(CurrentPeopleIdentified));
            OnPropertyChanged(nameof(CurrentProgressPercent));
            OnPropertyChanged(nameof(CurrentTotalPhotos));

            if (!string.IsNullOrEmpty(progress.ErrorMessage))
            {
                StatusMessage = progress.ErrorMessage;
            }
            else if (progress.Phase == FaceProcessingPhase.Completed)
            {
                StatusMessage = $"Completed: {progress.PhotosProcessed} photos, {progress.FacesDetected} faces, {progress.PeopleIdentified} people";
            }
            else if (progress.Phase != FaceProcessingPhase.Paused)
            {
                StatusMessage = progress.PhaseDisplay;
            }
        });
    }

    private void OnPeopleChanged(object? sender, EventArgs e)
    {
        Application.Current?.Dispatcher.BeginInvoke(() => _ = LoadPeopleAsync());
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _peopleService.ProgressChanged -= OnProgressChanged;
        _peopleService.PeopleChanged -= OnPeopleChanged;
        People.Clear();
        SelectedPersonFaces.Clear();
        SelectedPersonIds.Clear();
        SplitFaceIds.Clear();
    }
}
