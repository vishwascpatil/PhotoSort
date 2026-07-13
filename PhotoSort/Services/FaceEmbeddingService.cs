using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Microsoft.Extensions.Logging;
using PhotoSort.Models;

namespace PhotoSort.Services;

public sealed class FaceEmbeddingService : IFaceEmbeddingService
{
    private readonly IOnnxFaceEmbeddingGenerator _onnxEmbeddingGenerator;
    private readonly IFaceModelProvider _modelProvider;
    private readonly ILogger<FaceEmbeddingService> _logger;
    private bool _disposed;
    private bool _isInitialized;

    public bool IsInitialized => _isInitialized;

    public FaceEmbeddingService(
        IOnnxFaceEmbeddingGenerator onnxEmbeddingGenerator,
        IFaceModelProvider modelProvider,
        ILogger<FaceEmbeddingService> logger)
    {
        _onnxEmbeddingGenerator = onnxEmbeddingGenerator;
        _modelProvider = modelProvider;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized)
            return;

        try
        {
            _logger.LogInformation("Initializing face embedding service (ONNX)...");
            await _onnxEmbeddingGenerator.InitializeAsync(cancellationToken);
            _isInitialized = true;
            _logger.LogInformation(
                "Face embedding service initialized. Model: {Model}, Version: {Version}, Dimension: {Dim}",
                _modelProvider.GetEmbeddingModelName(),
                _modelProvider.GetEmbeddingModelVersion(),
                _modelProvider.GetEmbeddingDimension());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize face embedding service");
            throw;
        }
    }

    public async Task<float[]?> GenerateEmbeddingAsync(
        byte[] alignedFaceData,
        string modelVersion = "1.0",
        CancellationToken cancellationToken = default)
    {
        if (!_isInitialized)
            throw new InvalidOperationException("Face embedding service not initialized");

        try
        {
            return await _onnxEmbeddingGenerator.GenerateAsync(alignedFaceData, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate embedding");
            return null;
        }
    }

    public async Task<IReadOnlyList<FaceEmbedding>> GenerateEmbeddingsBatchAsync(
        IReadOnlyList<(int FaceId, byte[] AlignedFaceData)> faces,
        IProgress<FaceProcessingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<FaceEmbedding>();
        int processed = 0;

        foreach (var (faceId, alignedFaceData) in faces)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var embedding = await GenerateEmbeddingAsync(alignedFaceData, _modelProvider.GetEmbeddingModelVersion(), cancellationToken);

            if (embedding is not null)
            {
                results.Add(new FaceEmbedding
                {
                    FaceId = faceId,
                    Embedding = embedding,
                    ModelName = _modelProvider.GetEmbeddingModelName(),
                    ModelVersion = _modelProvider.GetEmbeddingModelVersion(),
                    EmbeddingDimension = _modelProvider.GetEmbeddingDimension(),
                    Confidence = 1.0,
                    CreatedDate = DateTime.UtcNow
                });
            }

            processed++;

            if (processed % 10 == 0 || processed == faces.Count)
            {
                progress?.Report(new FaceProcessingProgress
                {
                    Phase = FaceProcessingPhase.GeneratingEmbeddings,
                    TotalPhotos = faces.Count,
                    PhotosProcessed = processed,
                    EmbeddingsGenerated = results.Count
                });
            }
        }

        return results;
    }

    public Task<int> GetEmbeddingDimensionAsync()
    {
        return Task.FromResult(_modelProvider.GetEmbeddingDimension());
    }

    public Task<string> GetModelVersionAsync()
    {
        return Task.FromResult(_modelProvider.GetEmbeddingModelVersion());
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
    }
}
