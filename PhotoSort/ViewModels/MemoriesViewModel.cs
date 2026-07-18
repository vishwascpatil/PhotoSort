using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PhotoSort.Models;
using PhotoSort.Services;
using PhotoSort.Services.Memories;

namespace PhotoSort.ViewModels;

public partial class MemoriesViewModel : ObservableObject
{
    private readonly IMemoriesService _memoriesService;
    private readonly INavigationService _navigationService;
    private readonly IThumbnailService _thumbnailService;
    private readonly ILogger<MemoriesViewModel> _logger;
    private bool _isLoading;
    private bool _pipelineTriggered;
    private CancellationTokenSource? _pollCts;
    private int _offset;

    [ObservableProperty]
    private bool _hasMore = true;

    [ObservableProperty]
    private bool _isEmpty = true;

    [ObservableProperty]
    private bool _isPipelineRunning;

    [ObservableProperty]
    private int _unreadCount;

    [ObservableProperty]
    private string _sectionTitle = "Your Memories";

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public bool ShowEmptyState => IsEmpty && !IsPipelineRunning;

    partial void OnIsEmptyChanged(bool value) => OnPropertyChanged(nameof(ShowEmptyState));
    partial void OnIsPipelineRunningChanged(bool value) => OnPropertyChanged(nameof(ShowEmptyState));

    public ObservableCollection<MemoryItemViewModel> Memories { get; } = [];

    public MemoriesViewModel(
        IMemoriesService memoriesService,
        INavigationService navigationService,
        IThumbnailService thumbnailService,
        ILogger<MemoriesViewModel> logger)
    {
        _memoriesService = memoriesService;
        _navigationService = navigationService;
        _thumbnailService = thumbnailService;
        _logger = logger;
    }

    [RelayCommand]
    private async Task LoadInitialAsync()
    {
        if (_isLoading) return;
        _isLoading = true;

        try
        {
            _offset = 0;
            Memories.Clear();

            var memories = await _memoriesService.GetMemoriesAsync(20, 0);

            if (memories.Count == 0 && !_pipelineTriggered)
            {
                _pipelineTriggered = true;
                _pollCts = new CancellationTokenSource();
                IsPipelineRunning = true;
                StatusMessage = "Generating memories from your photos...";
                var pollToken = _pollCts.Token;

                // Run pipeline in background — don't block UI
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _memoriesService.RunPipelineAsync(pollToken);
                        _logger.LogInformation("Memory pipeline completed successfully");
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInformation("Memory pipeline cancelled");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Memory pipeline failed");
                    }
                    finally
                    {
                        IsPipelineRunning = false;
                        StatusMessage = string.Empty;
                        _pollCts?.Cancel();
                    }
                });

                // Poll for new memories while pipeline runs
                _ = PollForNewMemoriesAsync(pollToken);
            }

            foreach (var m in memories)
            {
                var vm = new MemoryItemViewModel(m, _thumbnailService);
                Memories.Add(vm);
                _ = vm.LoadCoverAsync();
            }

            HasMore = memories.Count == 20;
            IsEmpty = Memories.Count == 0;
            UnreadCount = await _memoriesService.GetUnreadCountAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load memories");
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task PollForNewMemoriesAsync(CancellationToken ct)
    {
        int lastCount = 0;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(1500, ct);

                var memories = await _memoriesService.GetMemoriesAsync(20, 0);
                if (memories.Count > lastCount)
                {
                    lastCount = memories.Count;
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        Memories.Clear();
                        foreach (var m in memories)
                        {
                            var vm = new MemoryItemViewModel(m, _thumbnailService);
                            Memories.Add(vm);
                            _ = vm.LoadCoverAsync();
                        }
                        IsEmpty = Memories.Count == 0;
                        HasMore = memories.Count == 20;
                        if (lastCount > 0)
                            StatusMessage = $"Found {lastCount} memories so far...";
                    });
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Memory poll check failed");
            }
        }
    }

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (_isLoading || !HasMore) return;
        _isLoading = true;

        try
        {
            _offset = Memories.Count;
            var memories = await _memoriesService.GetMemoriesAsync(20, _offset);
            foreach (var m in memories)
            {
                var vm = new MemoryItemViewModel(m, _thumbnailService);
                Memories.Add(vm);
                _ = vm.LoadCoverAsync();
            }
            HasMore = memories.Count == 20;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load more memories");
        }
        finally
        {
            _isLoading = false;
        }
    }

    [RelayCommand]
    private void OpenMemory(MemoryItemViewModel? item)
    {
        if (item is null) return;
        var vm = App.Services.GetRequiredService<MemoryDetailViewModel>();
        vm.Initialize(item.Memory);
        _navigationService.NavigateTo(vm);
    }

    [RelayCommand]
    private async Task DismissMemoryAsync(Guid? memoryId)
    {
        if (memoryId is null) return;
        await _memoriesService.DismissMemoryAsync(memoryId.Value);
        var item = Memories.FirstOrDefault(m => m.MemoryId == memoryId.Value);
        if (item is not null)
            Memories.Remove(item);
        IsEmpty = Memories.Count == 0;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadInitialAsync();
    }
}
