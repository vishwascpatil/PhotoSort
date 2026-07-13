using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PhotoSort.Models;

namespace PhotoSort.Services;

public sealed class FaceModelProvider : IFaceModelProvider
{
    private readonly OnnxModelConfiguration _config;
    private readonly ILogger<FaceModelProvider> _logger;
    private readonly ConcurrentDictionary<string, string> _modelPathCache = new();
    private bool _disposed;
    private bool _isInitialized;
    private bool _gpuAvailable;

    private static readonly HttpClient SharedHttpClient = new() { Timeout = TimeSpan.FromMinutes(5) };

    private static readonly Dictionary<string, (string Url, long ExpectedMinBytes)> ModelUrls = new()
    {
        ["face_detection.onnx"] = (
            "https://github.com/opencv/opencv_zoo/raw/main/models/face_detection_yunet/face_detection_yunet_2023mar.onnx",
            200_000),
        ["face_embedding.onnx"] = (
            "https://huggingface.co/onnx-community/arcface-onnx/resolve/main/arcface.onnx?download=true",
            100_000_000),
    };

    public bool IsInitialized => _isInitialized;

    public FaceModelProvider(
        IOptions<OnnxModelConfiguration> config,
        ILogger<FaceModelProvider> logger)
    {
        _config = config.Value;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized)
            return;

        try
        {
            var modelsDir = Path.GetFullPath(_config.ModelsDirectory);

            if (!Directory.Exists(modelsDir))
            {
                Directory.CreateDirectory(modelsDir);
                _logger.LogInformation("Created models directory: {Path}", modelsDir);
            }

            await DownloadModelIfNeededAsync(modelsDir, _config.DetectionModelFileName, cancellationToken);
            await DownloadModelIfNeededAsync(modelsDir, _config.EmbeddingModelFileName, cancellationToken);

            var detectionPath = Path.Combine(modelsDir, _config.DetectionModelFileName);
            var embeddingPath = Path.Combine(modelsDir, _config.EmbeddingModelFileName);

            if (!File.Exists(detectionPath))
                _logger.LogWarning("Detection model not found at {Path}. Face detection will use fallback.", detectionPath);

            if (!File.Exists(embeddingPath))
                _logger.LogWarning("Embedding model not found at {Path}. Face embedding will use fallback.", embeddingPath);

            _gpuAvailable = DetectGpuAvailability();
            _isInitialized = true;

            _logger.LogInformation(
                "Face model provider initialized. Detection: {Det}, Embedding: {Emb}, GPU: {Gpu}",
                File.Exists(detectionPath) ? "OK" : "missing",
                File.Exists(embeddingPath) ? "OK" : "missing",
                _gpuAvailable ? "available" : "not available");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize face model provider");
            _isInitialized = true;
        }
    }

    private async Task DownloadModelIfNeededAsync(string modelsDir, string fileName, CancellationToken ct)
    {
        var targetPath = Path.Combine(modelsDir, fileName);

        if (File.Exists(targetPath))
        {
            var fileInfo = new FileInfo(targetPath);
            if (ModelUrls.TryGetValue(fileName, out var expected) && fileInfo.Length >= expected.ExpectedMinBytes)
            {
                _logger.LogInformation("Model already exists: {Path} ({Size} bytes)", targetPath, fileInfo.Length);
                return;
            }
        }

        if (!ModelUrls.TryGetValue(fileName, out var urlInfo))
        {
            _logger.LogWarning("No download URL configured for model {FileName}", fileName);
            return;
        }

        _logger.LogInformation("Downloading {FileName} from {Url}...", fileName, urlInfo.Url);

        try
        {
            var tempPath = targetPath + ".tmp";
            using var response = await SharedHttpClient.GetAsync(urlInfo.Url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
            await using var fileStream = File.Create(tempPath);
            await contentStream.CopyToAsync(fileStream, ct);
            await fileStream.FlushAsync(ct);
            fileStream.Close();

            var downloadedSize = new FileInfo(tempPath).Length;
            if (downloadedSize < urlInfo.ExpectedMinBytes)
            {
                _logger.LogWarning("Downloaded model {FileName} is too small ({Size} bytes, expected >= {Expected}). Deleting.",
                    fileName, downloadedSize, urlInfo.ExpectedMinBytes);
                File.Delete(tempPath);
                return;
            }

            File.Move(tempPath, targetPath, overwrite: true);
            _logger.LogInformation("Successfully downloaded {FileName} ({Size} bytes)", fileName, downloadedSize);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to download model {FileName}. Face detection will use fallback.", fileName);

            var tempPath = targetPath + ".tmp";
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* ignore */ }
            }
        }
    }

    public string GetDetectionModelPath()
    {
        return Path.GetFullPath(Path.Combine(_config.ModelsDirectory, _config.DetectionModelFileName));
    }

    public string GetEmbeddingModelPath()
    {
        return Path.GetFullPath(Path.Combine(_config.ModelsDirectory, _config.EmbeddingModelFileName));
    }

    public string GetDetectionModelVersion() => _config.DetectionModelVersion;
    public string GetEmbeddingModelVersion() => _config.EmbeddingModelVersion;
    public string GetDetectionModelName() => _config.DetectionModelName;
    public string GetEmbeddingModelName() => _config.EmbeddingModelName;
    public int GetEmbeddingDimension() => _config.EmbeddingDimension;
    public int GetDetectionInputWidth() => _config.DetectionInputWidth;
    public int GetDetectionInputHeight() => _config.DetectionInputHeight;
    public int GetEmbeddingInputSize() => _config.EmbeddingInputSize;
    public float GetDetectionConfidenceThreshold() => _config.DetectionConfidenceThreshold;
    public float GetNmsIouThreshold() => _config.NmsIouThreshold;
    public bool IsGpuAvailable() => _gpuAvailable;
    public OnnxModelConfiguration GetConfiguration() => _config;

    private static bool DetectGpuAvailability()
    {
        try
        {
            using var options = new Microsoft.ML.OnnxRuntime.SessionOptions();
            try { options.AppendExecutionProvider_CUDA(0); return true; } catch { }
            try { options.AppendExecutionProvider_DML(0); return true; } catch { }
            return false;
        }
        catch { return false; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _modelPathCache.Clear();
    }
}
