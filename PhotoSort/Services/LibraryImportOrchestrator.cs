using System.Diagnostics;
using Microsoft.Extensions.Logging;
using PhotoSort.Data.Repositories;
using PhotoSort.Models;

namespace PhotoSort.Services;

public sealed class LibraryImportOrchestrator : ILibraryImportOrchestrator
{
    private readonly IPhotoIndexingService _indexingService;
    private readonly IMetadataExtractionService _metadataService;
    private readonly IFolderRepository _folderRepository;
    private readonly IPhotoRepository _photoRepository;
    private readonly ILogger<LibraryImportOrchestrator> _logger;

    private CancellationTokenSource? _cts;
    private volatile bool _pauseRequested;

    public bool IsRunning { get; private set; }
    public bool IsPaused => _pauseRequested;
    public LibraryImportProgress CurrentProgress { get; } = new();

    public event EventHandler<LibraryImportProgress>? ProgressChanged;
    public event EventHandler<string>? StageChanged;

    public LibraryImportOrchestrator(
        IPhotoIndexingService indexingService,
        IMetadataExtractionService metadataService,
        IFolderRepository folderRepository,
        IPhotoRepository photoRepository,
        ILogger<LibraryImportOrchestrator> logger)
    {
        _indexingService = indexingService;
        _metadataService = metadataService;
        _folderRepository = folderRepository;
        _photoRepository = photoRepository;
        _logger = logger;
    }

    public async Task<ImportResult> ImportFolderAsync(
        string folderPath,
        IProgress<LibraryImportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (IsRunning)
            throw new InvalidOperationException("An import is already in progress.");

        IsRunning = true;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var stopwatch = Stopwatch.StartNew();
        var allErrors = new List<string>();

        try
        {
            // ── Stage 1: File Discovery & DB Indexing (0-50%) ──
            _logger.LogInformation("Starting import for folder: {FolderPath}", folderPath);
            RaiseStageChanged("Discovering");
            CurrentProgress.CurrentStage = "Discovering";
            CurrentProgress.Percentage = 0;
            RaiseProgressChanged(progress);

            var indexingResult = await RunIndexingStageAsync(folderPath, progress, _cts.Token);
            allErrors.AddRange(indexingResult.Errors);

            CurrentProgress.FilesIndexed = indexingResult.TotalProcessed;
            CurrentProgress.FilesDiscovered = indexingResult.TotalDiscovered;
            CurrentProgress.TotalFailed = indexingResult.TotalFailed;
            CurrentProgress.Percentage = CurrentProgress.FilesDiscovered > 0
                ? (double)CurrentProgress.FilesIndexed / CurrentProgress.FilesDiscovered * 50
                : 50;
            RaiseProgressChanged(progress);

            if (_cts.Token.IsCancellationRequested)
            {
                stopwatch.Stop();
                return BuildResult(allErrors, stopwatch.Elapsed, wasCancelled: true);
            }

            // ── Stage 2: Metadata Extraction (50-100%) ──
            RaiseStageChanged("Metadata");
            CurrentProgress.CurrentStage = "Metadata";
            RaiseProgressChanged(progress);

            var metadataResult = await RunMetadataStageAsync(progress, _cts.Token);
            allErrors.AddRange(metadataResult.Errors);

            CurrentProgress.MetadataExtracted = metadataResult.TotalProcessed;
            CurrentProgress.TotalFailed += metadataResult.TotalFailed;
            CurrentProgress.Percentage = CurrentProgress.FilesIndexed > 0
                ? 50 + (double)CurrentProgress.MetadataExtracted / CurrentProgress.FilesIndexed * 50
                : 100;
            RaiseProgressChanged(progress);

            stopwatch.Stop();

            if (_cts.Token.IsCancellationRequested)
                return BuildResult(allErrors, stopwatch.Elapsed, wasCancelled: true);

            // ── Complete ──
            CurrentProgress.CurrentStage = "Complete";
            CurrentProgress.Percentage = 100;
            RaiseStageChanged("Complete");
            RaiseProgressChanged(progress);

            _logger.LogInformation(
                "Import completed: {Indexed} indexed, {Metadata} metadata, {Failed} failed in {Elapsed}",
                CurrentProgress.FilesIndexed, CurrentProgress.MetadataExtracted,
                CurrentProgress.TotalFailed, stopwatch.Elapsed);

            return BuildResult(allErrors, stopwatch.Elapsed);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return BuildResult(allErrors, stopwatch.Elapsed, wasCancelled: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Import failed for folder: {FolderPath}", folderPath);
            allErrors.Add($"Import failed: {ex.Message}");
            stopwatch.Stop();
            return BuildResult(allErrors, stopwatch.Elapsed);
        }
        finally
        {
            IsRunning = false;
            _pauseRequested = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    public Task PauseAsync()
    {
        if (!IsRunning || _metadataService.IsExtracting == false)
            return Task.CompletedTask;

        _pauseRequested = true;
        _cts?.Cancel();
        CurrentProgress.IsPaused = true;
        CurrentProgress.CurrentStage = "Paused";
        RaiseStageChanged("Paused");
        return Task.CompletedTask;
    }

    public Task ResumeAsync()
    {
        if (!IsRunning || !_pauseRequested)
            return Task.CompletedTask;

        _pauseRequested = false;
        return Task.CompletedTask;
    }

    public Task CancelAsync()
    {
        if (!IsRunning)
            return Task.CompletedTask;

        _cts?.Cancel();
        return Task.CompletedTask;
    }

    public async Task RecoverIncompleteImportsAsync()
    {
        _logger.LogInformation("Checking for incomplete imports...");

        var incomplete = await _photoRepository.GetAllIncompleteAsync();
        if (incomplete.Count == 0)
        {
            _logger.LogInformation("No incomplete imports found.");
            return;
        }

        _logger.LogInformation("Found {Count} incomplete photos. Metadata extraction will resume on next import.",
            incomplete.Count);
    }

    private async Task<IndexingResult> RunIndexingStageAsync(
        string folderPath,
        IProgress<LibraryImportProgress>? progress,
        CancellationToken ct)
    {
        var indexingProgress = new Progress<IndexingProgress>(p =>
        {
            CurrentProgress.FilesDiscovered = p.FilesDiscovered;
            CurrentProgress.FilesIndexed = p.FilesProcessed;
            CurrentProgress.CurrentFolder = p.CurrentFolder;
            CurrentProgress.CurrentFile = p.CurrentFile;
            CurrentProgress.Elapsed = p.Elapsed;
            RaiseProgressChanged(progress);
        });

        return await _indexingService.IndexFolderAsync(folderPath, indexingProgress, ct);
    }

    private async Task<ExtractionResult> RunMetadataStageAsync(
        IProgress<LibraryImportProgress>? progress,
        CancellationToken ct)
    {
        var metadataProgress = new Progress<MetadataExtractionProgress>(p =>
        {
            CurrentProgress.MetadataExtracted = p.FilesProcessed;
            CurrentProgress.CurrentFile = p.CurrentFile;
            RaiseProgressChanged(progress);
        });

        return await _metadataService.ExtractAllAsync(metadataProgress, ct);
    }

    private void RaiseProgressChanged(IProgress<LibraryImportProgress>? progress)
    {
        progress?.Report(CurrentProgress);
        ProgressChanged?.Invoke(this, CurrentProgress);
    }

    private void RaiseStageChanged(string stage)
    {
        StageChanged?.Invoke(this, stage);
    }

    private static ImportResult BuildResult(
        List<string> errors,
        TimeSpan elapsed,
        bool wasCancelled = false)
    {
        return new ImportResult
        {
            TotalFailed = errors.Count,
            Duration = elapsed,
            Errors = errors.AsReadOnly(),
            WasCancelled = wasCancelled
        };
    }
}
