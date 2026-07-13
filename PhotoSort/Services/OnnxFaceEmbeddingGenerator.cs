using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using PhotoSort.Models;

namespace PhotoSort.Services;

public sealed class OnnxFaceEmbeddingGenerator : IOnnxFaceEmbeddingGenerator
{
    private readonly IFaceModelProvider _modelProvider;
    private readonly ILogger<OnnxFaceEmbeddingGenerator> _logger;

    private readonly ConcurrentBag<InferenceSession> _sessionPool = new();
    private readonly SemaphoreSlim _sessionLock;
    private bool _disposed;
    private bool _isInitialized;
    private double _lastInferenceTimeMs;
    private string _modelVersion = "";
    private string _inputName = "input";

    public bool IsInitialized => _isInitialized;

    public OnnxFaceEmbeddingGenerator(
        IFaceModelProvider modelProvider,
        ILogger<OnnxFaceEmbeddingGenerator> logger)
    {
        _modelProvider = modelProvider;
        _logger = logger;
        _sessionLock = new SemaphoreSlim(1, 1);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized)
            return;

        await _modelProvider.InitializeAsync(cancellationToken);

        var modelPath = _modelProvider.GetEmbeddingModelPath();

        if (!File.Exists(modelPath))
        {
            _logger.LogWarning("Embedding model not found at {Path}. Will use fallback embeddings.", modelPath);
            _modelVersion = "fallback-1.0";
            _isInitialized = true;
            return;
        }

        try
        {
            var sessionOptions = CreateSessionOptions();
            var session = new InferenceSession(modelPath, sessionOptions);
            _sessionPool.Add(session);

            _inputName = session.InputMetadata.Keys.FirstOrDefault() ?? "input";
            _modelVersion = _modelProvider.GetEmbeddingModelVersion();
            _isInitialized = true;

            _logger.LogInformation(
                "ONNX face embedding generator initialized: {Model}, Dimension: {Dim}, Input: {Input}, Inputs: {Inputs}",
                Path.GetFileName(modelPath),
                _modelProvider.GetEmbeddingDimension(),
                _inputName,
                string.Join(", ", session.InputMetadata.Keys));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize ONNX embedding generator from {Path}", modelPath);
            _modelVersion = "fallback-1.0";
            _isInitialized = true;
        }
    }

    public async Task<float[]?> GenerateAsync(byte[] alignedFaceData, CancellationToken cancellationToken = default)
    {
        if (!_isInitialized)
            throw new InvalidOperationException("Embedding generator not initialized");

        return await Task.Run(() => GenerateEmbedding(alignedFaceData, cancellationToken), cancellationToken);
    }

    public async Task<float[]?> GenerateAsync(float[] pixelData, CancellationToken cancellationToken = default)
    {
        if (!_isInitialized)
            throw new InvalidOperationException("Embedding generator not initialized");

        return await Task.Run(() => GenerateEmbeddingFromPixels(pixelData, cancellationToken), cancellationToken);
    }

    public async Task<IReadOnlyList<float[]>> GenerateBatchAsync(
        IReadOnlyList<byte[]> alignedFaces,
        CancellationToken cancellationToken = default)
    {
        if (!_isInitialized)
            throw new InvalidOperationException("Embedding generator not initialized");

        if (_sessionPool.IsEmpty || alignedFaces.Count == 0)
            return alignedFaces.Select(_ => GenerateFallbackEmbedding()).ToList();

        return await Task.Run(() =>
        {
            var results = new List<float[]>();
            foreach (var face in alignedFaces)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var embedding = GenerateEmbedding(face, cancellationToken);
                if (embedding != null)
                    results.Add(embedding);
            }
            return (IReadOnlyList<float[]>)results;
        }, cancellationToken);
    }

    public bool IsModelLoaded() => !_sessionPool.IsEmpty;

    public string GetModelVersion() => _modelVersion;

    public int GetEmbeddingDimension() => _modelProvider.GetEmbeddingDimension();

    public double GetLastInferenceTimeMs() => _lastInferenceTimeMs;

    private float[]? GenerateEmbedding(byte[] faceData, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            using var stream = new MemoryStream(faceData);
            using var bitmap = new Bitmap(stream);

            var inputSize = _modelProvider.GetEmbeddingInputSize();
            using var resized = ResizeForEmbedding(bitmap, inputSize);
            var inputTensor = BitmapToEmbeddingTensor(resized);

            var session = GetSession();
            try
            {
                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor(_inputName, inputTensor)
                };

                using var results = session.Run(inputs);
                var embedding = ExtractEmbedding(results);

                sw.Stop();
                _lastInferenceTimeMs = sw.Elapsed.TotalMilliseconds;

                return NormalizeEmbedding(embedding);
            }
            finally
            {
                ReturnSession(session);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "ONNX embedding generation failed, using fallback");
            sw.Stop();
            _lastInferenceTimeMs = sw.Elapsed.TotalMilliseconds;
            return GenerateFallbackEmbedding();
        }
    }

    private float[]? GenerateEmbeddingFromPixels(float[] pixelData, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            var inputSize = _modelProvider.GetEmbeddingInputSize();
            var tensor = new DenseTensor<float>(pixelData, new[] { 1, 3, inputSize, inputSize });

            var session = GetSession();
            try
            {
                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor(_inputName, tensor)
                };

                using var results = session.Run(inputs);
                var embedding = ExtractEmbedding(results);

                sw.Stop();
                _lastInferenceTimeMs = sw.Elapsed.TotalMilliseconds;

                return NormalizeEmbedding(embedding);
            }
            finally
            {
                ReturnSession(session);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "ONNX embedding from pixels failed");
            sw.Stop();
            _lastInferenceTimeMs = sw.Elapsed.TotalMilliseconds;
            return GenerateFallbackEmbedding();
        }
    }

    private static Bitmap ResizeForEmbedding(Bitmap bitmap, int targetSize)
    {
        var resized = new Bitmap(targetSize, targetSize, PixelFormat.Format24bppRgb);
        using var graphics = Graphics.FromImage(resized);
        graphics.InterpolationMode = InterpolationMode.Bilinear;
        graphics.DrawImage(bitmap, 0, 0, targetSize, targetSize);
        return resized;
    }

    private static DenseTensor<float> BitmapToEmbeddingTensor(Bitmap bitmap)
    {
        var size = bitmap.Width;
        var tensor = new DenseTensor<float>(new[] { 1, 3, size, size });

        var bits = bitmap.LockBits(
            new Rectangle(0, 0, size, size),
            ImageLockMode.ReadOnly,
            PixelFormat.Format24bppRgb);

        try
        {
            var stride = bits.Stride;
            var buffer = new byte[stride * size];
            Marshal.Copy(bits.Scan0, buffer, 0, buffer.Length);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var offset = y * stride + x * 3;
                    var b = buffer[offset] / 255.0f;
                    var g = buffer[offset + 1] / 255.0f;
                    var r = buffer[offset + 2] / 255.0f;

                    tensor[0, 0, y, x] = (r - 0.5f) / 0.5f;
                    tensor[0, 1, y, x] = (g - 0.5f) / 0.5f;
                    tensor[0, 2, y, x] = (b - 0.5f) / 0.5f;
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(bits);
        }

        return tensor;
    }

    private float[] ExtractEmbedding(IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results)
    {
        var dimension = _modelProvider.GetEmbeddingDimension();

        if (results.Count == 0)
            return GenerateFallbackEmbedding();

        var outputTensor = results[0].AsTensor<float>();
        var output = outputTensor.ToArray();

        var embedding = new float[Math.Min(dimension, output.Length)];
        for (int i = 0; i < embedding.Length; i++)
        {
            embedding[i] = output[i];
        }

        return embedding;
    }

    private static float[] NormalizeEmbedding(float[] embedding)
    {
        var norm = 0.0f;
        for (int i = 0; i < embedding.Length; i++)
        {
            norm += embedding[i] * embedding[i];
        }
        norm = MathF.Sqrt(norm);

        if (norm < 1e-6f)
            return embedding;

        var normalized = new float[embedding.Length];
        for (int i = 0; i < embedding.Length; i++)
        {
            normalized[i] = embedding[i] / norm;
        }

        return normalized;
    }

    private float[] GenerateFallbackEmbedding()
    {
        var dimension = _modelProvider.GetEmbeddingDimension();
        var random = new Random();
        var embedding = new float[dimension];

        for (int i = 0; i < dimension; i++)
        {
            embedding[i] = (float)(random.NextDouble() * 2 - 1);
        }

        return NormalizeEmbedding(embedding);
    }

    private InferenceSession GetSession()
    {
        _sessionLock.Wait();
        try
        {
            if (_sessionPool.TryTake(out var session))
                return session;

            var modelPath = _modelProvider.GetEmbeddingModelPath();
            var options = CreateSessionOptions();
            return new InferenceSession(modelPath, options);
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    private void ReturnSession(InferenceSession session)
    {
        _sessionPool.Add(session);
    }

    private SessionOptions CreateSessionOptions()
    {
        var options = new SessionOptions
        {
            InterOpNumThreads = _modelProvider.GetConfiguration().CpuThreadCount,
            IntraOpNumThreads = _modelProvider.GetConfiguration().CpuThreadCount,
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
        };

        if (_modelProvider.IsGpuAvailable() && _modelProvider.GetConfiguration().UseGpu)
        {
            try
            {
                options.AppendExecutionProvider_CUDA(0);
            }
            catch
            {
                try
                {
                    options.AppendExecutionProvider_DML(0);
                }
                catch
                {
                    _logger.LogInformation("GPU execution providers not available for embedding, using CPU");
                }
            }
        }

        return options;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var session in _sessionPool)
        {
            try { session.Dispose(); } catch { }
        }

        _sessionPool.Clear();
        _sessionLock.Dispose();
    }
}
