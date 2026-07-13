using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Microsoft.Extensions.Logging;
using PhotoSort.Models;

namespace PhotoSort.Services;

public sealed class FaceDetectionService : IFaceDetectionService
{
    private readonly IOnnxFaceDetector _onnxDetector;
    private readonly IFaceModelProvider _modelProvider;
    private readonly ILogger<FaceDetectionService> _logger;
    private bool _disposed;
    private bool _isInitialized;

    private static readonly HashSet<string> SupportedExtensions =
    [
        ".jpg", ".jpeg", ".png", ".bmp", ".webp", ".heic", ".heif"
    ];

    public bool IsInitialized => _isInitialized;

    public FaceDetectionService(
        IOnnxFaceDetector onnxDetector,
        IFaceModelProvider modelProvider,
        ILogger<FaceDetectionService> logger)
    {
        _onnxDetector = onnxDetector;
        _modelProvider = modelProvider;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized)
            return;

        try
        {
            _logger.LogInformation("Initializing face detection service (ONNX)...");
            await _onnxDetector.InitializeAsync(cancellationToken);
            _isInitialized = true;
            _logger.LogInformation(
                "Face detection service initialized. Model: {Model}, Version: {Version}, GPU: {Gpu}",
                _modelProvider.GetDetectionModelName(),
                _modelProvider.GetDetectionModelVersion(),
                _modelProvider.IsGpuAvailable());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize face detection service");
            throw;
        }
    }

    public async Task<FaceDetectionResult> DetectFacesAsync(
        string filePath,
        int photoId,
        CancellationToken cancellationToken = default)
    {
        if (!_isInitialized)
            throw new InvalidOperationException("Face detection service not initialized");

        try
        {
            if (!File.Exists(filePath))
            {
                return new FaceDetectionResult
                {
                    PhotoId = photoId,
                    FilePath = filePath,
                    FacesDetected = 0,
                    Faces = [],
                    Success = false,
                    ErrorMessage = "File not found"
                };
            }

            var extension = Path.GetExtension(filePath);
            if (!SupportedExtensions.Contains(extension.ToLowerInvariant()))
            {
                return new FaceDetectionResult
                {
                    PhotoId = photoId,
                    FilePath = filePath,
                    FacesDetected = 0,
                    Faces = [],
                    Success = false,
                    ErrorMessage = "Unsupported file format"
                };
            }

            var imageData = await File.ReadAllBytesAsync(filePath, cancellationToken);
            var detectedFaces = await _onnxDetector.DetectAsync(imageData, cancellationToken);

            var faces = detectedFaces.Select(df => new DetectedFace
            {
                BoundingBoxX = df.BoundingBoxX,
                BoundingBoxY = df.BoundingBoxY,
                BoundingBoxWidth = df.BoundingBoxWidth,
                BoundingBoxHeight = df.BoundingBoxHeight,
                Confidence = df.Confidence,
                Landmarks = df.Landmarks,
                FaceAngle = df.FaceAngle,
                FaceSize = df.FaceSize,
                ModelVersion = _modelProvider.GetDetectionModelVersion()
            }).ToList();

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
            _logger.LogError(ex, "Failed to detect faces in {FilePath}", filePath);
            return new FaceDetectionResult
            {
                PhotoId = photoId,
                FilePath = filePath,
                FacesDetected = 0,
                Faces = [],
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<IReadOnlyList<FaceDetectionResult>> DetectFacesBatchAsync(
        IReadOnlyList<(int PhotoId, string FilePath)> photos,
        IProgress<FaceProcessingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<FaceDetectionResult>();
        int processed = 0;

        foreach (var (photoId, filePath) in photos)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await DetectFacesAsync(filePath, photoId, cancellationToken);
            results.Add(result);

            processed++;

            if (processed % 10 == 0 || processed == photos.Count)
            {
                progress?.Report(new FaceProcessingProgress
                {
                    Phase = FaceProcessingPhase.DetectingFaces,
                    TotalPhotos = photos.Count,
                    PhotosProcessed = processed,
                    FacesDetected = results.Sum(r => r.FacesDetected)
                });
            }
        }

        return results;
    }

    public async Task<byte[]?> ExtractAlignedFaceAsync(
        string filePath,
        DetectedFace face,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(filePath))
                return null;

            var imageData = await File.ReadAllBytesAsync(filePath, cancellationToken);
            return await ExtractAlignedFaceAsync(imageData, face, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract aligned face from {FilePath}", filePath);
            return null;
        }
    }

    public async Task<byte[]?> ExtractAlignedFaceAsync(
        byte[] imageData,
        DetectedFace face,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await Task.Run(() =>
            {
                using var stream = new MemoryStream(imageData);
                using var bitmap = new Bitmap(stream);

                var faceRect = new Rectangle(
                    (int)(face.BoundingBoxX * bitmap.Width),
                    (int)(face.BoundingBoxY * bitmap.Height),
                    (int)(face.BoundingBoxWidth * bitmap.Width),
                    (int)(face.BoundingBoxHeight * bitmap.Height));

                faceRect.Intersect(new Rectangle(0, 0, bitmap.Width, bitmap.Height));

                if (faceRect.Width <= 0 || faceRect.Height <= 0)
                    return null;

                using var faceBitmap = bitmap.Clone(faceRect, bitmap.PixelFormat);

                var targetSize = _modelProvider.GetEmbeddingInputSize();
                using var resized = new Bitmap(faceBitmap, targetSize, targetSize);

                using var ms = new MemoryStream();
                resized.Save(ms, ImageFormat.Jpeg);
                return ms.ToArray();
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract aligned face");
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
    }
}
