using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PhotoSort.Data.Repositories;
using PhotoSort.Models;

namespace PhotoSort.Services;

public sealed class FaceRecognitionPipeline : IFaceRecognitionPipeline
{
    private readonly IOnnxFaceDetector _detector;
    private readonly IOnnxFaceEmbeddingGenerator _embeddingGenerator;
    private readonly IFaceClusteringService _clusteringService;
    private readonly IFaceRepository _faceRepository;
    private readonly IFaceEmbeddingRepository _embeddingRepository;
    private readonly IFaceModelProvider _modelProvider;
    private readonly FaceRecognitionConfiguration _config;
    private readonly ILogger<FaceRecognitionPipeline> _logger;

    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _processingLock = new(1, 1);
    private volatile bool _isProcessing;
    private volatile bool _isPaused;
    private FaceProcessingMetrics _currentMetrics = new();

    public event EventHandler<FaceProcessingProgress>? ProgressChanged;

    public bool IsProcessing => _isProcessing;

    public FaceRecognitionPipeline(
        IOnnxFaceDetector detector,
        IOnnxFaceEmbeddingGenerator embeddingGenerator,
        IFaceClusteringService clusteringService,
        IFaceRepository faceRepository,
        IFaceEmbeddingRepository embeddingRepository,
        IFaceModelProvider modelProvider,
        IOptions<FaceRecognitionConfiguration> config,
        ILogger<FaceRecognitionPipeline> logger)
    {
        _detector = detector;
        _embeddingGenerator = embeddingGenerator;
        _clusteringService = clusteringService;
        _faceRepository = faceRepository;
        _embeddingRepository = embeddingRepository;
        _modelProvider = modelProvider;
        _config = config.Value;
        _logger = logger;
    }

    public bool IsInitialized => _detector.IsInitialized && _embeddingGenerator.IsInitialized;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing face recognition pipeline...");

        await _detector.InitializeAsync(cancellationToken);
        await _embeddingGenerator.InitializeAsync(cancellationToken);

        _currentMetrics = new FaceProcessingMetrics
        {
            IsGpuAvailable = _modelProvider.IsGpuAvailable(),
            IsUsingGpu = _modelProvider.IsGpuAvailable(),
            CurrentModelName = _modelProvider.GetDetectionModelName(),
            CurrentModelVersion = _modelProvider.GetDetectionModelVersion()
        };

        _logger.LogInformation(
            "Face recognition pipeline initialized. GPU: {Gpu}, Model: {Model}",
            _currentMetrics.IsUsingGpu ? "enabled" : "disabled",
            _currentMetrics.CurrentModelName);
    }

    public async Task<FaceProcessingMetrics> ProcessPhotosAsync(
        IReadOnlyList<(int PhotoId, string FilePath)> photos,
        CancellationToken cancellationToken = default)
    {
        if (!IsInitialized)
            throw new InvalidOperationException("Pipeline not initialized");

        if (_isProcessing)
            throw new InvalidOperationException("Pipeline is already processing");

        await _processingLock.WaitAsync(cancellationToken);
        try
        {
            _isProcessing = true;
            _isPaused = false;

            var metrics = new FaceProcessingMetrics
            {
                IsGpuAvailable = _modelProvider.IsGpuAvailable(),
                IsUsingGpu = _modelProvider.IsGpuAvailable(),
                CurrentModelName = _modelProvider.GetDetectionModelName(),
                CurrentModelVersion = _modelProvider.GetDetectionModelVersion()
            };

            var sw = Stopwatch.StartNew();
            var totalPhotos = photos.Count;
            var processedCount = 0;
            var totalFacesDetected = 0;
            var totalEmbeddingsGenerated = 0;

            _logger.LogInformation("Starting face recognition on {Count} photos", totalPhotos);

            var batchSize = _config.BatchSize;
            var batches = photos
                .Select((photo, index) => new { photo, index })
                .GroupBy(x => x.index / batchSize)
                .Select(g => g.Select(x => x.photo).ToList())
                .ToList();

            var allDetectedFaces = new List<DetectedFace>();
            var allPhotoFaces = new Dictionary<int, List<DetectedFace>>();

            foreach (var batch in batches)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CheckPaused(cancellationToken);

                foreach (var (photoId, filePath) in batch)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    CheckPaused(cancellationToken);

                    try
                    {
                        if (!File.Exists(filePath))
                        {
                            metrics.FailedPhotos++;
                            processedCount++;
                            continue;
                        }

                        var imageData = await File.ReadAllBytesAsync(filePath, cancellationToken);
                        var detectionResult = await _detector.DetectAsync(imageData, cancellationToken);

                        totalFacesDetected += detectionResult.Count;

                        if (detectionResult.Count > 0)
                        {
                            var photoFaces = new List<DetectedFace>();

                            foreach (var detectedFace in detectionResult)
                            {
                                var alignedFace = await ExtractAlignedFaceAsync(
                                    imageData, detectedFace, cancellationToken);

                                if (alignedFace != null)
                                {
                                    var embedding = await _embeddingGenerator.GenerateAsync(
                                        alignedFace, cancellationToken);

                                    if (embedding != null)
                                    {
                                        totalEmbeddingsGenerated++;
                                        photoFaces.Add(detectedFace);
                                    }
                                }
                            }

                            if (photoFaces.Count > 0)
                            {
                                allPhotoFaces[photoId] = photoFaces;
                                allDetectedFaces.AddRange(photoFaces);
                            }
                        }

                        processedCount++;
                        metrics.PhotosProcessed = processedCount;
                        metrics.TotalFacesDetected = totalFacesDetected;
                        metrics.TotalEmbeddingsGenerated = totalEmbeddingsGenerated;
                        metrics.AverageInferenceTimeMs = _detector.GetLastInferenceTimeMs();
                        metrics.AverageEmbeddingTimeMs = _embeddingGenerator.GetLastInferenceTimeMs();

                        var progress = new FaceProcessingProgress
                        {
                            Phase = FaceProcessingPhase.DetectingFaces,
                            TotalPhotos = totalPhotos,
                            PhotosProcessed = processedCount,
                            ProcessedPhotos = processedCount,
                            CurrentPhotoIndex = processedCount,
                            CurrentPhotoName = Path.GetFileName(filePath),
                            FacesDetected = totalFacesDetected,
                            EmbeddingsGenerated = totalEmbeddingsGenerated,
                            EstimatedTimeRemaining = CalculateEta(processedCount, totalPhotos, sw.Elapsed),
                            IsPaused = _isPaused,
                            AverageInferenceTimeMs = metrics.AverageInferenceTimeMs,
                            AverageEmbeddingTimeMs = metrics.AverageEmbeddingTimeMs,
                            IsGpuAvailable = metrics.IsGpuAvailable,
                            IsUsingGpu = metrics.IsUsingGpu,
                            CurrentModelName = metrics.CurrentModelName,
                            CurrentModelVersion = metrics.CurrentModelVersion
                        };

                        ProgressChanged?.Invoke(this, progress);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to process photo {PhotoId}: {Path}", photoId, filePath);
                        metrics.FailedPhotos++;
                        metrics.PhotosProcessed = ++processedCount;
                    }
                }
            }

            if (allDetectedFaces.Count > 0 && _config.EnableIncrementalProcessing)
            {
                _logger.LogInformation("Running clustering on {Count} detected faces", allDetectedFaces.Count);

                var existingPeople = await GetExistingPeopleCentroidsAsync(cancellationToken);

                var embeddings = new List<FaceEmbedding>();
                for (int i = 0; i < allDetectedFaces.Count; i++)
                {
                    var face = allDetectedFaces[i];
                    if (face.AlignedFaceData == null) continue;

                    var embedding = await _embeddingGenerator.GenerateAsync(face.AlignedFaceData, cancellationToken);
                    if (embedding != null)
                    {
                        embeddings.Add(new FaceEmbedding
                        {
                            FaceId = i,
                            Embedding = embedding,
                            ModelName = _modelProvider.GetEmbeddingModelName(),
                            EmbeddingDimension = _modelProvider.GetEmbeddingDimension(),
                            ModelVersion = _modelProvider.GetEmbeddingModelVersion()
                        });
                    }
                }

                if (embeddings.Count > 0)
                {
                    var clusters = await _clusteringService.ClusterWithExistingAsync(
                        embeddings,
                        existingPeople,
                        _config.SimilarityThreshold,
                        cancellationToken);

                    metrics.ClustersIdentified = clusters.GroupBy(c => c.PersonId).Count();

                    _logger.LogInformation("Clustering complete: {Clusters} clusters identified",
                        metrics.ClustersIdentified);
                }
            }

            sw.Stop();
            metrics.ProcessingTimeMs = sw.Elapsed.TotalMilliseconds;
            _currentMetrics = metrics;

            _logger.LogInformation(
                "Face recognition complete: {Photos} photos, {Faces} faces, {Embeddings} embeddings, {Clusters} clusters, {Time:F1}s, Failed: {Failed}",
                processedCount, totalFacesDetected, totalEmbeddingsGenerated,
                metrics.ClustersIdentified, sw.Elapsed.TotalSeconds, metrics.FailedPhotos);

            return metrics;
        }
        finally
        {
            _isProcessing = false;
            _processingLock.Release();
        }
    }

    public async Task<FaceDetectionResult> ProcessSinglePhotoAsync(
        int photoId,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (!IsInitialized)
            throw new InvalidOperationException("Pipeline not initialized");

        try
        {
            if (!File.Exists(filePath))
            {
                return new FaceDetectionResult
                {
                    PhotoId = photoId,
                    FilePath = filePath,
                    Success = false,
                    ErrorMessage = "File not found"
                };
            }

            var imageData = await File.ReadAllBytesAsync(filePath, cancellationToken);
            var detectionResult = await _detector.DetectAsync(imageData, cancellationToken);

            var faces = new List<DetectedFace>();
            foreach (var detectedFace in detectionResult)
            {
                var alignedFace = await ExtractAlignedFaceAsync(imageData, detectedFace, cancellationToken);

                if (alignedFace != null)
                {
                    var embedding = await _embeddingGenerator.GenerateAsync(alignedFace, cancellationToken);

                    if (embedding != null)
                    {
                        faces.Add(detectedFace);
                    }
                }
            }

            return new FaceDetectionResult
            {
                PhotoId = photoId,
                FilePath = filePath,
                FacesDetected = faces.Count,
                Faces = faces,
                Success = true,
                ModelVersion = _modelProvider.GetDetectionModelVersion()
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to process single photo {PhotoId}", photoId);
            return new FaceDetectionResult
            {
                PhotoId = photoId,
                FilePath = filePath,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public void PauseProcessing()
    {
        _isPaused = true;
        _logger.LogInformation("Face recognition pipeline paused");
    }

    public void ResumeProcessing()
    {
        _isPaused = false;
        _logger.LogInformation("Face recognition pipeline resumed");
    }

    public void CancelProcessing()
    {
        _cts.Cancel();
        _isPaused = false;
        _logger.LogInformation("Face recognition pipeline cancelled");
    }

    public FaceProcessingMetrics GetCurrentMetrics() => _currentMetrics;

    private void CheckPaused(CancellationToken cancellationToken)
    {
        while (_isPaused)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Thread.Sleep(100);
        }
    }

    private async Task<byte[]?> ExtractAlignedFaceAsync(
        byte[] imageData,
        DetectedFace detectedFace,
        CancellationToken cancellationToken)
    {
        try
        {
            return await Task.Run(() =>
            {
                using var stream = new MemoryStream(imageData);
                using var bitmap = new System.Drawing.Bitmap(stream);

                var x = (int)(detectedFace.BoundingBoxX * bitmap.Width);
                var y = (int)(detectedFace.BoundingBoxY * bitmap.Height);
                var width = (int)(detectedFace.BoundingBoxWidth * bitmap.Width);
                var height = (int)(detectedFace.BoundingBoxHeight * bitmap.Height);

                x = Math.Max(0, Math.Min(x, bitmap.Width - 1));
                y = Math.Max(0, Math.Min(y, bitmap.Height - 1));
                width = Math.Min(width, bitmap.Width - x);
                height = Math.Min(height, bitmap.Height - y);

                if (width <= 0 || height <= 0)
                    return null;

                using var faceBitmap = bitmap.Clone(
                    new System.Drawing.Rectangle(x, y, width, height),
                    bitmap.PixelFormat);

                var inputSize = _modelProvider.GetEmbeddingInputSize();
                using var resized = new System.Drawing.Bitmap(
                    inputSize, inputSize,
                    System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                using (var graphics = System.Drawing.Graphics.FromImage(resized))
                {
                    graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;
                    graphics.DrawImage(faceBitmap, 0, 0, inputSize, inputSize);
                }

                using var ms = new MemoryStream();
                resized.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                return ms.ToArray();
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to extract aligned face");
            return null;
        }
    }

    private async Task<List<(int PersonId, float[] Centroid)>> GetExistingPeopleCentroidsAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var centroids = await _embeddingRepository.GetAllPersonCentroidsAsync();
            return centroids.Select(c => (c.PersonId, c.Centroid)).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get existing people centroids");
            return [];
        }
    }

    private static TimeSpan CalculateEta(int processed, int total, TimeSpan elapsed)
    {
        if (processed == 0)
            return TimeSpan.Zero;

        var rate = processed / elapsed.TotalSeconds;
        var remaining = total - processed;
        return TimeSpan.FromSeconds(remaining / rate);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _processingLock.Dispose();
    }
}
