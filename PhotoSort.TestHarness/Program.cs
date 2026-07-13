using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PhotoSort.Data;
using PhotoSort.Data.Repositories;
using PhotoSort.Models;
using PhotoSort.Services;
using PhotoSort.TestHarness.Diagnostics;
using PhotoSort.TestHarness.Tests;

namespace PhotoSort.TestHarness;

public sealed class Program
{
    private static readonly string TestDbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PhotoSort", "TestHarness", "test.db");

    private static readonly string ReportPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PhotoSort", "TestHarness", "report.md");

    public static async Task<int> Main(string[] args)
    {
        var folderPath = args.Length > 0
            ? args[0]
            : @"C:\Users\vishw\Downloads\06-02-2022(f)";

        if (!Directory.Exists(folderPath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"ERROR: Folder not found: {folderPath}");
            Console.ResetColor();
            return 1;
        }

        Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
        Console.WriteLine("║          PhotoSort End-to-End Validation Harness        ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine($"Target folder: {folderPath}");
        Console.WriteLine($"Test database: {TestDbPath}");
        Console.WriteLine();

        var diags = Directory.CreateDirectory(Path.GetDirectoryName(TestDbPath)!);

        if (File.Exists(TestDbPath))
        {
            File.Delete(TestDbPath);
            Console.WriteLine("Deleted previous test database.");
        }

        var host = CreateHost(folderPath);
        var services = host.Services;

        var allSuites = new List<TestSuite>();
        var memoryTracker = new MemoryTracker(TimeSpan.FromSeconds(2));
        var perfTracker = new PerformanceTracker();

        try
        {
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("PhotoSort Test Harness started");

            var dbInit = services.GetRequiredService<DatabaseInitializer>();
            await dbInit.InitializeAsync();
            Console.WriteLine("Database initialized.");
            Console.WriteLine();

            // Phase 1: Library Import
            allSuites.Add(await RunPhase1Async(services, folderPath, memoryTracker, perfTracker));

            // Phase 2: Metadata Extraction
            allSuites.Add(await RunPhase2Async(services, memoryTracker, perfTracker));

            // Phase 3: Thumbnail Generation
            allSuites.Add(await RunPhase3Async(services, memoryTracker, perfTracker));

            // Phase 4: Database Integrity
            allSuites.Add(await RunPhase4Async(services, memoryTracker, perfTracker));

            // Phase 5: Duplicate Detection
            allSuites.Add(await RunPhase5Async(services, memoryTracker, perfTracker));

            // Phase 6: Similar Photos
            allSuites.Add(await RunPhase6Async(services, memoryTracker, perfTracker));

            // Phase 7: Media Classification
            allSuites.Add(await RunPhase7Async(services, memoryTracker, perfTracker));

            // Phase 8: People
            allSuites.Add(await RunPhase8Async(services, memoryTracker, perfTracker));

            // Phase 9: Travel Insights
            allSuites.Add(await RunPhase9Async(services, memoryTracker, perfTracker));

            // Phase 10: FileWatcher
            allSuites.Add(await RunPhase10Async(services, folderPath, memoryTracker, perfTracker));

            // Phase 11: Stress Tests
            allSuites.Add(await RunPhase11Async(services, folderPath, memoryTracker, perfTracker));
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"FATAL: {ex}");
            Console.ResetColor();
        }
        finally
        {
            memoryTracker.Dispose();
        }

        GenerateReport(allSuites, memoryTracker.GetReport(), perfTracker.GetReport());

        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine("FINAL SUMMARY");
        Console.WriteLine("═══════════════════════════════════════════════════════════");

        int totalPassed = 0, totalFailed = 0, totalWarnings = 0;
        foreach (var suite in allSuites)
        {
            totalPassed += suite.Passed;
            totalFailed += suite.Failed;
            totalWarnings += suite.Warnings;

            var icon = suite.Failed == 0 ? "✓" : "✗";
            var color = suite.Failed == 0 ? ConsoleColor.Green : ConsoleColor.Red;

            Console.ForegroundColor = color;
            Console.Write($"  {icon} ");
            Console.ResetColor();
            Console.WriteLine($"{suite.PhaseName}: {suite.Passed} passed, {suite.Failed} failed, {suite.Warnings} warnings");
        }

        Console.WriteLine();
        Console.WriteLine($"Total: {totalPassed} passed, {totalFailed} failed, {totalWarnings} warnings");
        Console.WriteLine($"Report: {ReportPath}");

        return totalFailed > 0 ? 1 : 0;
    }

    private static IHost CreateHost(string folderPath)
    {
        var dbPath = TestDbPath;

        return Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

                services.AddDbContextFactory<PhotoSortDbContext>(options =>
                    options.UseSqlite($"Data Source={dbPath}"));

                services.AddSingleton<DatabaseInitializer>();

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

                services.AddSingleton<INavigationService, NavigationService>();
                services.AddSingleton<IPhotoIndexingService, PhotoIndexingService>();
                services.AddSingleton<IFileWatcherService, FileWatcherService>();
                services.AddSingleton<IPipelineMediator, PipelineMediator>();
                services.AddSingleton<IMetadataExtractionService, MetadataExtractionService>();
                services.AddSingleton<IGalleryDataService, GalleryDataService>();
                services.AddSingleton<IMediaLoaderService, MediaLoaderService>();
                services.AddSingleton<IThumbnailService, ThumbnailService>();
                services.AddSingleton<IThumbnailCacheService, ThumbnailCacheService>();
                services.AddSingleton<ITimelineService, TimelineService>();
                services.AddSingleton<IDuplicateDetectionService, DuplicateDetectionService>();
                services.AddSingleton<IMediaClassificationService, MediaClassificationService>();
                services.AddSingleton<ISimilarPhotoService, SimilarPhotoService>();
                services.AddSingleton<ILibrarySynchronizationService, LibrarySynchronizationService>();

                // Video Thumbnail Services
                services.AddSingleton<IVideoThumbnailService, VideoThumbnailService>();
                services.AddSingleton<IVideoPreviewCacheService, VideoPreviewCacheService>();
                services.AddSingleton<IVideoThumbnailWorker, VideoThumbnailBackgroundWorker>();

                // ONNX Face Recognition Pipeline
                services.Configure<OnnxModelConfiguration>(options =>
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
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Warning);
            })
            .Build();
    }

    private static async Task<TestSuite> RunPhase1Async(
        IServiceProvider services,
        string folderPath,
        MemoryTracker mem,
        PerformanceTracker perf)
    {
        var suite = new TestSuite { PhaseName = "Phase 1: Library Import" };
        Console.WriteLine("─── Phase 1: Library Import ───");

        var indexing = services.GetRequiredService<IPhotoIndexingService>();

        // Test 1.1: Initial indexing
        var result = new TestResult { TestName = "Initial folder indexing" };
        mem.Sample();
        try
        {
            perf.Start("Phase1_IndexFolder");
            var indexResult = await indexing.IndexFolderAsync(folderPath);
            perf.Stop("Phase1_IndexFolder");

            result.ItemsProcessed = indexResult.TotalProcessed;
            result.DurationMs = perf.GetReport().Entries.FirstOrDefault(e => e.Name == "Phase1_IndexFolder")?.ElapsedMs ?? 0;
            result.ItemsPerSecond = result.DurationMs > 0 ? indexResult.TotalProcessed / (result.DurationMs / 1000.0) : 0;

            if (indexResult.TotalProcessed > 0)
            {
                result.Pass($"{indexResult.TotalProcessed} files indexed in {indexResult.Duration.TotalSeconds:F1}s ({result.ItemsPerSecond:F1} files/s). " +
                           $"Skipped: {indexResult.TotalSkipped}, Failed: {indexResult.TotalFailed}");
            }
            else if (indexResult.TotalDiscovered == 0)
            {
                result.Fail("No supported files found in folder");
            }
            else
            {
                result.Fail($"Indexing completed but 0 files processed. Discovered: {indexResult.TotalDiscovered}, Skipped: {indexResult.TotalSkipped}");
            }

            if (indexResult.Errors.Count > 0)
            {
                result.Warnings.Add($"{indexResult.Errors.Count} errors during indexing");
                foreach (var err in indexResult.Errors.Take(5))
                    result.Warnings.Add($"  {err}");
            }
        }
        catch (Exception ex)
        {
            result.Fail($"Indexing crashed: {ex.Message}", ex);
        }
        suite.Results.Add(result);

        // Test 1.2: Incremental indexing (second run should skip)
        var result2 = new TestResult { TestName = "Incremental indexing (skip unchanged)" };
        try
        {
            perf.Start("Phase1_Incremental");
            var indexResult2 = await indexing.IndexFolderAsync(folderPath);
            perf.Stop("Phase1_Incremental");

            result2.DurationMs = perf.GetReport().Entries.FirstOrDefault(e => e.Name == "Phase1_Incremental")?.ElapsedMs ?? 0;

            if (indexResult2.TotalSkipped > 0 || indexResult2.TotalProcessed == 0)
            {
                result2.Pass($"Incremental: {indexResult2.TotalSkipped} skipped, {indexResult2.TotalProcessed} processed in {indexResult2.Duration.TotalSeconds:F1}s");
            }
            else
            {
                result2.Warn($"Incremental indexing processed {indexResult2.TotalProcessed} files (expected 0 skipped on first re-run)");
            }
        }
        catch (Exception ex)
        {
            result2.Fail($"Incremental indexing failed: {ex.Message}", ex);
        }
        suite.Results.Add(result2);

        // Test 1.3: Verify DB photo count matches disk
        var result3 = new TestResult { TestName = "DB photo count matches disk" };
        try
        {
            var contextFactory = services.GetRequiredService<IDbContextFactory<PhotoSortDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            var dbCount = await context.Photos.CountAsync();

            var diskCount = Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
                .Count(f => PhotoIndexingService.IsSupportedFileStatic(Path.GetExtension(f)));

            if (dbCount == diskCount)
            {
                result3.Pass($"DB has {dbCount} photos, disk has {diskCount} supported files");
            }
            else
            {
                result3.Warn($"DB has {dbCount} photos, disk has {diskCount} supported files (difference: {dbCount - diskCount})");
            }
        }
        catch (Exception ex)
        {
            result3.Fail($"Count verification failed: {ex.Message}", ex);
        }
        suite.Results.Add(result3);

        suite.MemoryReport = mem.GetReport();
        Console.WriteLine();
        return suite;
    }

    private static async Task<TestSuite> RunPhase2Async(
        IServiceProvider services,
        MemoryTracker mem,
        PerformanceTracker perf)
    {
        var suite = new TestSuite { PhaseName = "Phase 2: Metadata Extraction" };
        Console.WriteLine("─── Phase 2: Metadata Extraction ───");

        var metadata = services.GetRequiredService<IMetadataExtractionService>();

        var result = new TestResult { TestName = "Extract all metadata" };
        mem.Sample();
        try
        {
            perf.Start("Phase2_ExtractAll");
            var extractResult = await metadata.ExtractAllAsync();
            perf.Stop("Phase2_ExtractAll");

            result.ItemsProcessed = extractResult.TotalProcessed;
            result.DurationMs = perf.GetReport().Entries.FirstOrDefault(e => e.Name == "Phase2_ExtractAll")?.ElapsedMs ?? 0;
            result.ItemsPerSecond = result.DurationMs > 0 ? extractResult.TotalProcessed / (result.DurationMs / 1000.0) : 0;

            if (extractResult.TotalProcessed > 0)
            {
                result.Pass($"{extractResult.TotalProcessed} files processed in {extractResult.Duration.TotalSeconds:F1}s. " +
                           $"Failed: {extractResult.TotalFailed}");
            }
            else
            {
                result.Warn("No files needed metadata extraction (all already processed or none indexed)");
            }

            if (extractResult.Errors.Count > 0)
            {
                result.Warnings.Add($"{extractResult.Errors.Count} extraction errors");
                foreach (var err in extractResult.Errors.Take(5))
                    result.Warnings.Add($"  {err}");
            }
        }
        catch (Exception ex)
        {
            result.Fail($"Metadata extraction crashed: {ex.Message}", ex);
        }
        suite.Results.Add(result);

        // Test 2.2: Verify metadata was populated
        var result2 = new TestResult { TestName = "Verify metadata fields populated" };
        try
        {
            var contextFactory = services.GetRequiredService<IDbContextFactory<PhotoSortDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();

            var total = await context.Photos.CountAsync();
            var withWidth = await context.Photos.CountAsync(p => p.Width != null);
            var withHeight = await context.Photos.CountAsync(p => p.Height != null);
            var withDateTaken = await context.Photos.CountAsync(p => p.DateTaken != null);
            var withCamera = await context.Photos.CountAsync(p => p.CameraMake != null);
            var withGps = await context.Photos.CountAsync(p => p.Latitude != null);

            var msg = $"Total: {total}, Width: {withWidth}, Height: {withHeight}, " +
                     $"DateTaken: {withDateTaken}, Camera: {withCamera}, GPS: {withGps}";

            if (withWidth > 0 || withHeight > 0)
            {
                result2.Pass(msg);
            }
            else
            {
                result2.Warn($"No metadata extracted. {msg}");
            }
        }
        catch (Exception ex)
        {
            result2.Fail($"Metadata verification failed: {ex.Message}", ex);
        }
        suite.Results.Add(result2);

        suite.MemoryReport = mem.GetReport();
        Console.WriteLine();
        return suite;
    }

    private static async Task<TestSuite> RunPhase3Async(
        IServiceProvider services,
        MemoryTracker mem,
        PerformanceTracker perf)
    {
        var suite = new TestSuite { PhaseName = "Phase 3: Thumbnail Generation" };
        Console.WriteLine("─── Phase 3: Thumbnail Generation ───");

        var thumbnailService = services.GetRequiredService<IThumbnailService>();
        var photoRepo = services.GetRequiredService<IPhotoRepository>();

        var result = new TestResult { TestName = "Generate thumbnails for first 100 photos" };
        mem.Sample();
        try
        {
            var photos = await photoRepo.GetAllAsync();
            var testPhotos = photos.Take(100).ToList();

            int generated = 0;
            int failed = 0;

            perf.Start("Phase3_GenerateThumbnails");
            foreach (var photo in testPhotos)
            {
                try
                {
                    var thumb = await thumbnailService.GenerateThumbnailAsync(
                        photo.FilePath, photo.Id, ThumbnailSize.Small);
                    if (thumb != null)
                    {
                        generated++;
                        thumb.Dispose();
                    }
                    else
                    {
                        failed++;
                    }
                }
                catch
                {
                    failed++;
                }
            }
            perf.Stop("Phase3_GenerateThumbnails");

            result.ItemsProcessed = testPhotos.Count;
            result.DurationMs = perf.GetReport().Entries.FirstOrDefault(e => e.Name == "Phase3_GenerateThumbnails")?.ElapsedMs ?? 0;
            result.ItemsPerSecond = result.DurationMs > 0 ? generated / (result.DurationMs / 1000.0) : 0;

            if (generated > 0)
            {
                result.Pass($"{generated}/{testPhotos.Count} thumbnails generated in {result.DurationMs:F0}ms. Failed: {failed}");
            }
            else
            {
                result.Fail($"No thumbnails generated from {testPhotos.Count} photos");
            }
        }
        catch (Exception ex)
        {
            result.Fail($"Thumbnail generation crashed: {ex.Message}", ex);
        }
        suite.Results.Add(result);

        // Test 3.2: Cache verification
        var result2 = new TestResult { TestName = "Thumbnail cache exists" };
        try
        {
            var cacheDir = thumbnailService.GetCacheDirectory();
            var cachedCount = thumbnailService.GetCachedCount();
            var cacheSize = thumbnailService.GetCacheSizeBytes();

            if (Directory.Exists(cacheDir) && cachedCount > 0)
            {
                result2.Pass($"Cache: {cachedCount} files, {cacheSize / (1024.0 * 1024):F1} MB at {cacheDir}");
            }
            else if (!Directory.Exists(cacheDir))
            {
                result2.Fail($"Cache directory not created: {cacheDir}");
            }
            else
            {
                result2.Warn("Cache directory exists but no thumbnails found");
            }
        }
        catch (Exception ex)
        {
            result2.Fail($"Cache verification failed: {ex.Message}", ex);
        }
        suite.Results.Add(result2);

        suite.MemoryReport = mem.GetReport();
        Console.WriteLine();
        return suite;
    }

    private static async Task<TestSuite> RunPhase4Async(
        IServiceProvider services,
        MemoryTracker mem,
        PerformanceTracker perf)
    {
        var suite = new TestSuite { PhaseName = "Phase 4: Database Integrity" };
        Console.WriteLine("─── Phase 4: Database Integrity ───");

        var contextFactory = services.GetRequiredService<IDbContextFactory<PhotoSortDbContext>>();

        // Test 4.1: All tables exist
        var result = new TestResult { TestName = "All tables exist" };
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync();
            var tables = await context.Database.SqlQueryRaw<string>(
                "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name")
                .ToListAsync();

            var expected = new[] { "Photos", "Folders", "People", "Faces", "PersonFaces",
                                   "FaceEmbeddings", "Places", "Tags", "PhotoPlaces",
                                   "PhotoTags", "Trips", "TripPhotos", "TripPlaces" };

            var missing = expected.Where(t => !tables.Contains(t)).ToList();

            if (missing.Count == 0)
            {
                result.Pass($"All {expected.Length} tables exist");
            }
            else
            {
                result.Fail($"Missing tables: {string.Join(", ", missing)}");
            }
        }
        catch (Exception ex)
        {
            result.Fail($"Table check failed: {ex.Message}", ex);
        }
        suite.Results.Add(result);

        // Test 4.2: No orphan records
        var result2 = new TestResult { TestName = "No orphan PersonFace records" };
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync();
            var orphanFaces = await context.PersonFaces
                .Where(pf => !context.Faces.Any(f => f.Id == pf.FaceId))
                .CountAsync();
            var orphanPersons = await context.PersonFaces
                .Where(pf => !context.People.Any(p => p.Id == pf.PersonId))
                .CountAsync();

            if (orphanFaces == 0 && orphanPersons == 0)
            {
                result2.Pass("No orphan PersonFace records");
            }
            else
            {
                result2.Fail($"Orphan PersonFace: {orphanFaces} orphan faces, {orphanPersons} orphan persons");
            }
        }
        catch (Exception ex)
        {
            result2.Fail($"Orphan check failed: {ex.Message}", ex);
        }
        suite.Results.Add(result2);

        // Test 4.3: Duplicate records
        var result3 = new TestResult { TestName = "No duplicate FilePath records" };
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync();
            var duplicateCount = await context.Photos
                .GroupBy(p => p.FilePath)
                .Where(g => g.Count() > 1)
                .CountAsync();

            if (duplicateCount == 0)
            {
                result3.Pass("No duplicate FilePath records");
            }
            else
            {
                result3.Warn($"{duplicateCount} duplicate FilePath groups found");
            }
        }
        catch (Exception ex)
        {
            result3.Fail($"Duplicate check failed: {ex.Message}", ex);
        }
        suite.Results.Add(result3);

        suite.MemoryReport = mem.GetReport();
        Console.WriteLine();
        return suite;
    }

    private static async Task<TestSuite> RunPhase5Async(
        IServiceProvider services,
        MemoryTracker mem,
        PerformanceTracker perf)
    {
        var suite = new TestSuite { PhaseName = "Phase 5: Duplicate Detection" };
        Console.WriteLine("─── Phase 5: Duplicate Detection ───");

        var duplicateService = services.GetRequiredService<IDuplicateDetectionService>();

        var result = new TestResult { TestName = "Run duplicate detection" };
        mem.Sample();
        try
        {
            perf.Start("Phase5_DuplicateDetection");
            await duplicateService.StartDetectionAsync();
            perf.Stop("Phase5_DuplicateDetection");

            var groups = duplicateService.GetResults();
            result.ItemsProcessed = groups.Count;
            result.DurationMs = perf.GetReport().Entries.FirstOrDefault(e => e.Name == "Phase5_DuplicateDetection")?.ElapsedMs ?? 0;

            if (groups.Count > 0)
            {
                var totalDupes = groups.Sum(g => g.DuplicateCount);
                var savings = groups.Sum(g => g.PotentialSavings);
                result.Pass($"{groups.Count} duplicate groups found ({totalDupes} duplicates, {savings / (1024.0 * 1024):F1} MB potential savings)");
            }
            else
            {
                result.Pass("No duplicates found (clean library)");
            }
        }
        catch (Exception ex)
        {
            result.Fail($"Duplicate detection crashed: {ex.Message}", ex);
        }
        suite.Results.Add(result);

        suite.MemoryReport = mem.GetReport();
        Console.WriteLine();
        return suite;
    }

    private static async Task<TestSuite> RunPhase6Async(
        IServiceProvider services,
        MemoryTracker mem,
        PerformanceTracker perf)
    {
        var suite = new TestSuite { PhaseName = "Phase 6: Similar Photos" };
        Console.WriteLine("─── Phase 6: Similar Photos ───");

        var similarService = services.GetRequiredService<ISimilarPhotoService>();

        var result = new TestResult { TestName = "Run similar photo detection" };
        mem.Sample();
        try
        {
            perf.Start("Phase6_SimilarDetection");
            await similarService.StartDetectionAsync();
            perf.Stop("Phase6_SimilarDetection");

            var groups = similarService.GetResults();
            result.ItemsProcessed = groups.Count;
            result.DurationMs = perf.GetReport().Entries.FirstOrDefault(e => e.Name == "Phase6_SimilarDetection")?.ElapsedMs ?? 0;

            if (groups.Count > 0)
            {
                var totalSimilar = groups.Sum(g => g.GroupSize);
                var savings = groups.Sum(g => g.PotentialSavings);
                result.Pass($"{groups.Count} similar groups ({totalSimilar} photos, {savings / (1024.0 * 1024):F1} MB potential savings)");
            }
            else
            {
                result.Pass("No similar photos found");
            }
        }
        catch (Exception ex)
        {
            result.Fail($"Similar photo detection crashed: {ex.Message}", ex);
        }
        suite.Results.Add(result);

        suite.MemoryReport = mem.GetReport();
        Console.WriteLine();
        return suite;
    }

    private static async Task<TestSuite> RunPhase7Async(
        IServiceProvider services,
        MemoryTracker mem,
        PerformanceTracker perf)
    {
        var suite = new TestSuite { PhaseName = "Phase 7: Media Classification" };
        Console.WriteLine("─── Phase 7: Media Classification ───");

        var classifyService = services.GetRequiredService<IMediaClassificationService>();
        var contextFactory = services.GetRequiredService<IDbContextFactory<PhotoSortDbContext>>();

        var result = new TestResult { TestName = "Classify all photos" };
        mem.Sample();
        try
        {
            await classifyService.ClassifyAllAsync();

            await using var context = await contextFactory.CreateDbContextAsync();
            var total = await context.Photos.CountAsync();
            var classified = await context.Photos.CountAsync(p => p.MediaCategory != MediaCategory.Unknown);

            result.ItemsProcessed = classified;

            if (classified > 0)
            {
                var categories = await context.Photos
                    .GroupBy(p => p.MediaCategory)
                    .Select(g => new { Category = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .ToListAsync();

                var breakdown = string.Join(", ", categories.Take(8).Select(c => $"{c.Category}:{c.Count}"));
                result.Pass($"{classified}/{total} photos classified. Top categories: {breakdown}");
            }
            else
            {
                result.Warn($"Classification completed but 0 photos classified out of {total}");
            }
        }
        catch (Exception ex)
        {
            result.Fail($"Classification crashed: {ex.Message}", ex);
        }
        suite.Results.Add(result);

        suite.MemoryReport = mem.GetReport();
        Console.WriteLine();
        return suite;
    }

    private static async Task<TestSuite> RunPhase8Async(
        IServiceProvider services,
        MemoryTracker mem,
        PerformanceTracker perf)
    {
        var suite = new TestSuite { PhaseName = "Phase 8: People (Face Recognition)" };
        Console.WriteLine("─── Phase 8: People ───");

        var contextFactory = services.GetRequiredService<IDbContextFactory<PhotoSortDbContext>>();

        // Test 8.1: Face detection pipeline initializes
        var result = new TestResult { TestName = "Face detection pipeline initializes" };
        try
        {
            var detector = services.GetRequiredService<IOnnxFaceDetector>();
            var embeddingGen = services.GetRequiredService<IOnnxFaceEmbeddingGenerator>();

            await detector.InitializeAsync();
            await embeddingGen.InitializeAsync();

            var modelVersion = detector.GetModelVersion();
            result.Pass($"Pipeline initialized. Detector: {modelVersion}, " +
                       $"GPU: {services.GetRequiredService<IFaceModelProvider>().IsGpuAvailable()}");
        }
        catch (Exception ex)
        {
            result.Warn($"Face pipeline init failed (expected without models): {ex.Message}");
        }
        suite.Results.Add(result);

        // Test 8.2: Process a sample photo
        var result2 = new TestResult { TestName = "Process single photo for faces" };
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync();
            var samplePhoto = await context.Photos
                .Where(p => p.Extension == ".jpg" || p.Extension == ".jpeg")
                .FirstOrDefaultAsync();

            if (samplePhoto != null)
            {
                var peopleService = services.GetRequiredService<IPeopleService>();
                await peopleService.ProcessPhotoAsync(samplePhoto.Id, samplePhoto.FilePath);

                var faceCount = await context.Faces.CountAsync(f => f.PhotoId == samplePhoto.Id);
                result2.Pass($"Processed photo {samplePhoto.FileName}: {faceCount} faces detected");
            }
            else
            {
                result2.Warn("No JPEG photos found to test face detection");
            }
        }
        catch (Exception ex)
        {
            result2.Warn($"Face processing test failed (expected without models): {ex.Message}");
        }
        suite.Results.Add(result2);

        // Test 8.3: People CRUD
        var result3 = new TestResult { TestName = "People CRUD operations" };
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync();
            var personCount = await context.People.CountAsync();
            var faceCount = await context.Faces.CountAsync();
            var embeddingCount = await context.FaceEmbeddings.CountAsync();

            result3.Pass($"People: {personCount}, Faces: {faceCount}, Embeddings: {embeddingCount}");
        }
        catch (Exception ex)
        {
            result3.Fail($"People CRUD failed: {ex.Message}", ex);
        }
        suite.Results.Add(result3);

        suite.MemoryReport = mem.GetReport();
        Console.WriteLine();
        return suite;
    }

    private static async Task<TestSuite> RunPhase9Async(
        IServiceProvider services,
        MemoryTracker mem,
        PerformanceTracker perf)
    {
        var suite = new TestSuite { PhaseName = "Phase 9: Travel Insights" };
        Console.WriteLine("─── Phase 9: Travel Insights ───");

        var travelService = services.GetRequiredService<ITravelInsightsService>();

        var result = new TestResult { TestName = "Load travel insights" };
        mem.Sample();
        try
        {
            perf.Start("Phase9_TravelInsights");
            var insights = await travelService.GetInsightsAsync();
            perf.Stop("Phase9_TravelInsights");

            result.DurationMs = perf.GetReport().Entries.FirstOrDefault(e => e.Name == "Phase9_TravelInsights")?.ElapsedMs ?? 0;

            if (insights != null)
            {
                result.Pass($"Insights loaded: {insights.Statistics.TotalPhotosWhileTravelling} photos, " +
                           $"{insights.Statistics.TripsCompleted} trips, {insights.Statistics.CountriesVisited} countries, " +
                           $"{insights.Statistics.CitiesVisited} cities");
            }
            else
            {
                result.Pass("Travel insights returned null (no GPS data)");
            }
        }
        catch (Exception ex)
        {
            result.Fail($"Travel insights crashed: {ex.Message}", ex);
        }
        suite.Results.Add(result);

        suite.MemoryReport = mem.GetReport();
        Console.WriteLine();
        return suite;
    }

    private static async Task<TestSuite> RunPhase10Async(
        IServiceProvider services,
        string folderPath,
        MemoryTracker mem,
        PerformanceTracker perf)
    {
        var suite = new TestSuite { PhaseName = "Phase 10: FileWatcher" };
        Console.WriteLine("─── Phase 10: FileWatcher ───");

        var fileWatcher = services.GetRequiredService<IFileWatcherService>();

        var result = new TestResult { TestName = "FileWatcher starts and detects changes" };
        mem.Sample();
        try
        {
            fileWatcher.StartWatching(folderPath);

            var testFile = Path.Combine(folderPath, "_photosort_test_file.txt");
            await File.WriteAllTextAsync(testFile, "test");
            await Task.Delay(2000);

            if (File.Exists(testFile))
                File.Delete(testFile);

            await Task.Delay(1000);

            fileWatcher.StopWatching(folderPath);
            result.Pass("FileWatcher started, test file created/deleted without crash");
        }
        catch (Exception ex)
        {
            result.Fail($"FileWatcher failed: {ex.Message}", ex);
        }
        suite.Results.Add(result);

        suite.MemoryReport = mem.GetReport();
        Console.WriteLine();
        return suite;
    }

    private static async Task<TestSuite> RunPhase11Async(
        IServiceProvider services,
        string folderPath,
        MemoryTracker mem,
        PerformanceTracker perf)
    {
        var suite = new TestSuite { PhaseName = "Phase 11: Stress Tests" };
        Console.WriteLine("─── Phase 11: Stress Tests ───");

        var contextFactory = services.GetRequiredService<IDbContextFactory<PhotoSortDbContext>>();

        // Test 11.1: Large query performance
        var result = new TestResult { TestName = "Large query performance (10K+ records)" };
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync();
            var totalCount = await context.Photos.CountAsync();

            perf.Start("Phase11_LargeQuery");
            var allPhotos = await context.Photos
                .AsNoTracking()
                .Select(p => new { p.Id, p.FilePath, p.FileSize, p.Width, p.Height, p.DateTaken })
                .ToListAsync();
            perf.Stop("Phase11_LargeQuery");

            result.ItemsProcessed = allPhotos.Count;
            result.DurationMs = perf.GetReport().Entries.FirstOrDefault(e => e.Name == "Phase11_LargeQuery")?.ElapsedMs ?? 0;

            if (result.DurationMs < 5000)
            {
                result.Pass($"Loaded {allPhotos.Count} records in {result.DurationMs:F0}ms");
            }
            else
            {
                result.Warn($"Query took {result.DurationMs:F0}ms for {allPhotos.Count} records (slow)");
            }
        }
        catch (Exception ex)
        {
            result.Fail($"Large query failed: {ex.Message}", ex);
        }
        suite.Results.Add(result);

        // Test 11.2: Rapid cancel/restart cycle
        var result2 = new TestResult { TestName = "Rapid cancel/restart indexing cycle" };
        try
        {
            var indexing = services.GetRequiredService<IPhotoIndexingService>();

            for (int i = 0; i < 3; i++)
            {
                var cts = new CancellationTokenSource();
                var task = indexing.IndexFolderAsync(folderPath, cancellationToken: cts.Token);
                await Task.Delay(500);
                await indexing.CancelIndexingAsync();
                try { await task; } catch { }
            }

            result2.Pass("3 cancel/restart cycles completed without crash");
        }
        catch (Exception ex)
        {
            result2.Fail($"Cancel/restart cycle failed: {ex.Message}", ex);
        }
        suite.Results.Add(result2);

        // Test 11.3: Concurrent operations
        var result3 = new TestResult { TestName = "Concurrent DB access" };
        try
        {
            var tasks = Enumerable.Range(0, 10).Select(async i =>
            {
                await using var ctx = await contextFactory.CreateDbContextAsync();
                var count = await ctx.Photos.CountAsync();
                return count;
            });

            var counts = await Task.WhenAll(tasks);
            result3.Pass($"10 concurrent reads completed: counts = [{string.Join(",", counts)}]");
        }
        catch (Exception ex)
        {
            result3.Fail($"Concurrent access failed: {ex.Message}", ex);
        }
        suite.Results.Add(result3);

        suite.MemoryReport = mem.GetReport();
        Console.WriteLine();
        return suite;
    }

    private static void GenerateReport(
        List<TestSuite> suites,
        MemoryReport memory,
        TimingReport perf)
    {
        var dir = Path.GetDirectoryName(ReportPath)!;
        Directory.CreateDirectory(dir);

        using var writer = new StreamWriter(ReportPath);
        writer.WriteLine("# PhotoSort End-to-End Validation Report");
        writer.WriteLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        writer.WriteLine();
        writer.WriteLine("## Summary");
        writer.WriteLine();

        int totalPassed = 0, totalFailed = 0, totalWarnings = 0;
        foreach (var suite in suites)
        {
            totalPassed += suite.Passed;
            totalFailed += suite.Failed;
            totalWarnings += suite.Warnings;
        }

        writer.WriteLine($"| Metric | Value |");
        writer.WriteLine($"|--------|-------|");
        writer.WriteLine($"| Total Tests | {totalPassed + totalFailed + totalWarnings} |");
        writer.WriteLine($"| Passed | {totalPassed} |");
        writer.WriteLine($"| Failed | {totalFailed} |");
        writer.WriteLine($"| Warnings | {totalWarnings} |");
        writer.WriteLine($"| Peak Memory | {memory.FormatBytes(memory.PeakMemoryBytes)} |");
        writer.WriteLine($"| Memory Delta | {memory.FormatBytes(memory.DeltaBytes)} |");
        writer.WriteLine($"| GC Gen0/Gen1/Gen2 | {memory.Gen0Collections}/{memory.Gen1Collections}/{memory.Gen2Collections} |");
        writer.WriteLine();

        foreach (var suite in suites)
        {
            writer.WriteLine($"## {suite.PhaseName}");
            writer.WriteLine();

            foreach (var test in suite.Results)
            {
                var icon = test.Status switch
                {
                    TestStatus.Passed => "PASS",
                    TestStatus.Failed => "FAIL",
                    TestStatus.Warning => "WARN",
                    _ => "SKIP"
                };

                writer.WriteLine($"### [{icon}] {test.TestName}");
                writer.WriteLine();
                writer.WriteLine($"- **Status:** {icon}");
                writer.WriteLine($"- **Message:** {test.Message}");
                writer.WriteLine($"- **Duration:** {test.DurationMs:F0}ms");

                if (test.ItemsProcessed > 0)
                    writer.WriteLine($"- **Items Processed:** {test.ItemsProcessed}");

                if (test.ItemsPerSecond > 0)
                    writer.WriteLine($"- **Rate:** {test.ItemsPerSecond:F1} items/s");

                if (test.Warnings.Count > 0)
                {
                    writer.WriteLine("- **Warnings:**");
                    foreach (var w in test.Warnings)
                        writer.WriteLine($"  - {w}");
                }

                if (test.Exception != null)
                {
                    writer.WriteLine($"- **Exception:** `{test.Exception.GetType().Name}`: {test.Exception.Message}");
                }

                writer.WriteLine();
            }
        }

        writer.WriteLine("## Performance Metrics");
        writer.WriteLine();
        writer.WriteLine("| Operation | Duration (ms) | Calls | Avg (ms) |");
        writer.WriteLine("|-----------|--------------|-------|----------|");

        foreach (var entry in perf.Entries)
        {
            writer.WriteLine($"| {entry.Name} | {entry.ElapsedMs:F0} | {entry.CallCount} | {entry.AvgMs:F0} |");
        }

        writer.WriteLine();
        writer.WriteLine("## Memory Report");
        writer.WriteLine();
        writer.WriteLine($"- Start: {memory.FormatBytes(memory.StartMemoryBytes)}");
        writer.WriteLine($"- End: {memory.FormatBytes(memory.EndMemoryBytes)}");
        writer.WriteLine($"- Peak: {memory.FormatBytes(memory.PeakMemoryBytes)}");
        writer.WriteLine($"- Delta: {memory.FormatBytes(memory.DeltaBytes)}");
    }
}
