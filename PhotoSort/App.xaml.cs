using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PhotoSort.Data;
using PhotoSort.Data.Repositories;
using PhotoSort.Models;
using PhotoSort.Services;
using PhotoSort.ViewModels;

namespace PhotoSort;

public partial class App : Application
{
    private readonly IHost _host;
    private static readonly Uri LightThemeUri = new("Themes/LightTheme.xaml", UriKind.Relative);

    public App()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                // Database
                var dbPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PhotoSort",
                    "photosort.db");

                Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

                services.AddDbContextFactory<PhotoSortDbContext>(options =>
                    options.UseSqlite($"Data Source={dbPath}"));

                services.AddSingleton<DatabaseInitializer>();

                // Repositories
                services.AddSingleton<IRepository<Folder>, Repository<Folder>>();
                services.AddSingleton<IRepository<Photo>, Repository<Photo>>();
                services.AddSingleton<IRepository<Person>, Repository<Person>>();
                services.AddSingleton<IRepository<Face>, Repository<Face>>();
                services.AddSingleton<IRepository<FaceEmbedding>, Repository<FaceEmbedding>>();
                services.AddSingleton<IRepository<Place>, Repository<Place>>();
                services.AddSingleton<IRepository<Tag>, Repository<Tag>>();
                services.AddSingleton<IFolderRepository, FolderRepository>();
                services.AddSingleton<IPhotoRepository, PhotoRepository>();
                services.AddSingleton<IPersonRepository, PersonRepository>();
                services.AddSingleton<IFaceRepository, FaceRepository>();
                services.AddSingleton<IFaceEmbeddingRepository, FaceEmbeddingRepository>();
                services.AddSingleton<IPlaceRepository, PlaceRepository>();
                services.AddSingleton<ITripRepository, TripRepository>();
                services.AddSingleton<ITagRepository, TagRepository>();

                // Services
                services.AddSingleton<INavigationService, NavigationService>();
                services.AddSingleton<IFolderPickerService, FolderPickerService>();
                services.AddSingleton<IPhotoIndexingService, PhotoIndexingService>();
                services.AddSingleton<IFileWatcherService, FileWatcherService>();
                services.AddSingleton<IPipelineMediator, PipelineMediator>();
                services.AddSingleton<IMetadataExtractionService, MetadataExtractionService>();
                services.AddSingleton<ILibraryImportOrchestrator, LibraryImportOrchestrator>();
                services.AddSingleton<IGalleryDataService, GalleryDataService>();
                services.AddSingleton<IMediaLoaderService, MediaLoaderService>();
                services.AddSingleton<IThumbnailService, ThumbnailService>();
                services.AddSingleton<IThumbnailCacheService, ThumbnailCacheService>();
                services.AddSingleton<ITimelineService, TimelineService>();
                services.AddSingleton<IDuplicateDetectionService, DuplicateDetectionService>();
                services.AddSingleton<IMediaClassificationService, MediaClassificationService>();
                services.AddSingleton<ISimilarPhotoService, SimilarPhotoService>();
                services.AddSingleton<ILibrarySynchronizationService, LibrarySynchronizationService>();

                // ONNX Face Recognition Pipeline
                services.Configure<Models.OnnxModelConfiguration>(options =>
                {
                    options.ModelsDirectory = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "PhotoSort", "Models");
                });
                services.Configure<FaceRecognitionConfiguration>(options =>
                {
                    options.SimilarityThreshold = 0.6;
                    options.BatchSize = 50;
                    options.MaxConcurrentInference = 4;
                    options.EnableIncrementalProcessing = true;
                    options.EnableRetryOnFailure = true;
                    options.MaxRetries = 3;
                });

                services.AddSingleton<IFaceModelProvider, FaceModelProvider>();
                services.AddSingleton<IOnnxFaceDetector, OnnxFaceDetector>();
                services.AddSingleton<IOnnxFaceEmbeddingGenerator, OnnxFaceEmbeddingGenerator>();
                services.AddSingleton<IFaceClusteringService, FaceClusteringService>();
                services.AddSingleton<IFaceRecognitionPipeline, FaceRecognitionPipeline>();
                services.AddSingleton<IFaceDetectionService, FaceDetectionService>();
                services.AddSingleton<IFaceEmbeddingService, FaceEmbeddingService>();
                services.AddSingleton<IFaceRecognitionService, FaceRecognitionService>();
                services.AddSingleton<IPeopleService, PeopleService>();
                services.AddSingleton<ITravelInsightsService, TravelInsightsService>();

                services.AddSingleton<IVideoThumbnailService, VideoThumbnailService>();
                services.AddSingleton<IVideoPreviewCacheService, VideoPreviewCacheService>();
                services.AddSingleton<IVideoThumbnailWorker, VideoThumbnailBackgroundWorker>();

                // ViewModels
                services.AddSingleton<MainViewModel>();
                services.AddTransient<WelcomeViewModel>();
                services.AddTransient<GalleryViewModel>();
                services.AddTransient<PhotoViewerViewModel>();
                services.AddTransient<DuplicateDetectionViewModel>();
                services.AddTransient<CleanupViewModel>();
                services.AddTransient<SimilarPhotosViewModel>();
                services.AddTransient<SyncViewModel>();
                services.AddTransient<PeopleViewModel>();
                services.AddTransient<TravelInsightsViewModel>();
            })
            .Build();
    }

    public static IServiceProvider Services { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            System.Diagnostics.Debug.WriteLine($"[FATAL] Unhandled exception: {ex}");
            File.WriteAllText(
                Path.Combine(Path.GetTempPath(), "photosort_crash.log"),
                $"Timestamp: {DateTime.Now}\n{ex}\n");
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            System.Diagnostics.Debug.WriteLine($"[FATAL] Unobserved task exception: {args.Exception}");
            File.WriteAllText(
                Path.Combine(Path.GetTempPath(), "photosort_crash.log"),
                $"Timestamp: {DateTime.Now}\n{args.Exception}\n");
        };

        DispatcherUnhandledException += (_, args) =>
        {
            System.Diagnostics.Debug.WriteLine($"[FATAL] Dispatcher exception: {args.Exception}");
            File.WriteAllText(
                Path.Combine(Path.GetTempPath(), "photosort_crash.log"),
                $"Timestamp: {DateTime.Now}\n[Dispatcher]\n{args.Exception}\n");
            args.Handled = true;
        };

        try
        {
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "photosort_startup.log"), "Starting host...\n");
            await _host.StartAsync();
            File.AppendAllText(Path.Combine(Path.GetTempPath(), "photosort_startup.log"), "Host started.\n");

            Services = _host.Services;
            File.AppendAllText(Path.Combine(Path.GetTempPath(), "photosort_startup.log"), "Services resolved.\n");

            var dbInitializer = Services.GetRequiredService<DatabaseInitializer>();
            await dbInitializer.InitializeAsync();
            File.AppendAllText(Path.Combine(Path.GetTempPath(), "photosort_startup.log"), "DB initialized.\n");

            var mainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainViewModel>()
            };
            File.AppendAllText(Path.Combine(Path.GetTempPath(), "photosort_startup.log"), "MainWindow created.\n");

            mainWindow.Show();
            File.AppendAllText(Path.Combine(Path.GetTempPath(), "photosort_startup.log"), "MainWindow shown.\n");

            _ = Task.Run(async () =>
            {
                try
                {
                    var modelProvider = Services.GetRequiredService<IFaceModelProvider>();
                    await modelProvider.InitializeAsync();
                }
                catch (Exception ex)
                {
                    var logger = Services.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PhotoSort.App>>();
                    logger.LogWarning(ex, "Face model download failed at startup — models will be downloaded on first use");
                }
            });
        }
        catch (Exception ex)
        {
            var log = $"Timestamp: {DateTime.Now}\n[Startup]\n{ex}\n";
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "photosort_crash.log"), log);
            File.AppendAllText(Path.Combine(Path.GetTempPath(), "photosort_startup.log"), $"ERROR: {ex}\n");
            MessageBox.Show($"Startup error: {ex.Message}", "PhotoSort", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        var thumbnailCache = Services.GetRequiredService<IThumbnailCacheService>();
        thumbnailCache.CancelAll();
        await thumbnailCache.StopAsync();
        thumbnailCache.Dispose();

        var videoWorker = Services.GetRequiredService<IVideoThumbnailWorker>();
        videoWorker.CancelAll();
        await videoWorker.StopAsync();
        videoWorker.Dispose();

        var fileWatcher = Services.GetRequiredService<IFileWatcherService>();
        fileWatcher.Dispose();

        await _host.StopAsync();
        _host.Dispose();

        base.OnExit(e);
    }
}
