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

public sealed class OnnxFaceDetector : IOnnxFaceDetector
{
    private readonly IFaceModelProvider _modelProvider;
    private readonly ILogger<OnnxFaceDetector> _logger;

    private readonly ConcurrentBag<InferenceSession> _sessionPool = new();
    private readonly SemaphoreSlim _sessionLock;
    private bool _disposed;
    private bool _isInitialized;
    private double _lastInferenceTimeMs;
    private string _modelVersion = "";

    public bool IsInitialized => _isInitialized;

    public OnnxFaceDetector(
        IFaceModelProvider modelProvider,
        ILogger<OnnxFaceDetector> logger)
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

        var modelPath = _modelProvider.GetDetectionModelPath();

        if (!File.Exists(modelPath))
        {
            _logger.LogWarning("Detection model not found at {Path}. Will use fallback detection.", modelPath);
            _modelVersion = "fallback-1.0";
            _isInitialized = true;
            return;
        }

        try
        {
            var sessionOptions = CreateSessionOptions();
            var session = new InferenceSession(modelPath, sessionOptions);
            _sessionPool.Add(session);

            _modelVersion = _modelProvider.GetDetectionModelVersion();
            _isInitialized = true;

            _logger.LogInformation(
                "ONNX face detector initialized: {Model}, Inputs: {Inputs}, Outputs: {Outputs}",
                Path.GetFileName(modelPath),
                string.Join(", ", session.InputMetadata.Keys),
                string.Join(", ", session.OutputMetadata.Keys));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize ONNX face detector from {Path}", modelPath);
            _modelVersion = "fallback-1.0";
            _isInitialized = true;
        }
    }

    public async Task<IReadOnlyList<DetectedFace>> DetectAsync(
        byte[] imageData,
        CancellationToken cancellationToken = default)
    {
        if (!_isInitialized)
            throw new InvalidOperationException("Detector not initialized");

        if (_sessionPool.IsEmpty)
            return await DetectWithFallbackAsync(imageData, cancellationToken);

        return await Task.Run(() => DetectWithOnnx(imageData, cancellationToken), cancellationToken);
    }

    public async Task<IReadOnlyList<DetectedFace>> DetectAsync(
        float[] pixelData,
        int width,
        int height,
        CancellationToken cancellationToken = default)
    {
        if (!_isInitialized)
            throw new InvalidOperationException("Detector not initialized");

        if (_sessionPool.IsEmpty)
            return [];

        return await Task.Run(() => DetectWithOnnxFromPixels(pixelData, width, height, cancellationToken), cancellationToken);
    }

    public bool IsModelLoaded() => !_sessionPool.IsEmpty;

    public string GetModelVersion() => _modelVersion;

    public double GetLastInferenceTimeMs() => _lastInferenceTimeMs;

    private IReadOnlyList<DetectedFace> DetectWithOnnx(byte[] imageData, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            using var stream = new MemoryStream(imageData);
            using var bitmap = new Bitmap(stream);

            var inputWidth = _modelProvider.GetDetectionInputWidth();
            var inputHeight = _modelProvider.GetDetectionInputHeight();

            var (resizedBitmap, scaleX, scaleY) = ResizeForDetection(bitmap, inputWidth, inputHeight);

            using var _ = resizedBitmap;
            var inputTensor = BitmapToTensor(resizedBitmap);

            var session = GetSession();
            try
            {
                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor("input", inputTensor)
                };

                using var results = session.Run(inputs);
                var faces = PostProcessResults(results, scaleX, scaleY, bitmap.Width, bitmap.Height);

                sw.Stop();
                _lastInferenceTimeMs = sw.Elapsed.TotalMilliseconds;

                return faces;
            }
            finally
            {
                ReturnSession(session);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "ONNX detection failed, using fallback");
            return DetectWithFallback(imageData);
        }
    }

    private IReadOnlyList<DetectedFace> DetectWithOnnxFromPixels(
        float[] pixelData, int width, int height, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            var inputWidth = _modelProvider.GetDetectionInputWidth();
            var inputHeight = _modelProvider.GetDetectionInputHeight();

            var tensor = new DenseTensor<float>(
                pixelData,
                new[] { 1, 3, inputHeight, inputWidth });

            var session = GetSession();
            try
            {
                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor("input", tensor)
                };

                using var results = session.Run(inputs);
                var scaleX = (float)width / inputWidth;
                var scaleY = (float)height / inputHeight;
                var faces = PostProcessResults(results, scaleX, scaleY, width, height);

                sw.Stop();
                _lastInferenceTimeMs = sw.Elapsed.TotalMilliseconds;

                return faces;
            }
            finally
            {
                ReturnSession(session);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "ONNX detection from pixels failed");
            return [];
        }
    }

    private static (Bitmap resized, float scaleX, float scaleY) ResizeForDetection(Bitmap bitmap, int targetWidth, int targetHeight)
    {
        var scaleX = (float)bitmap.Width / targetWidth;
        var scaleY = (float)bitmap.Height / targetHeight;

        var resized = new Bitmap(targetWidth, targetHeight, PixelFormat.Format24bppRgb);
        using var graphics = Graphics.FromImage(resized);
        graphics.InterpolationMode = InterpolationMode.Bilinear;
        graphics.DrawImage(bitmap, 0, 0, targetWidth, targetHeight);

        return (resized, scaleX, scaleY);
    }

    private static DenseTensor<float> BitmapToTensor(Bitmap bitmap)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;
        var tensor = new DenseTensor<float>(new[] { 1, 3, height, width });

        var bits = bitmap.LockBits(
            new Rectangle(0, 0, width, height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format24bppRgb);

        try
        {
            var stride = bits.Stride;
            var buffer = new byte[stride * height];
            Marshal.Copy(bits.Scan0, buffer, 0, buffer.Length);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var offset = y * stride + x * 3;
                    var b = buffer[offset] / 255.0f;
                    var g = buffer[offset + 1] / 255.0f;
                    var r = buffer[offset + 2] / 255.0f;

                    tensor[0, 0, y, x] = (r - 0.485f) / 0.229f;
                    tensor[0, 1, y, x] = (g - 0.456f) / 0.224f;
                    tensor[0, 2, y, x] = (b - 0.406f) / 0.225f;
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(bits);
        }

        return tensor;
    }

    private List<DetectedFace> PostProcessResults(
        IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results,
        float scaleX, float scaleY,
        int originalWidth, int originalHeight)
    {
        var faces = new List<DetectedFace>();
        var confidenceThreshold = _modelProvider.GetDetectionConfidenceThreshold();

        try
        {
            if (results.Count == 0)
                return faces;

            var output = results[0].AsTensor<float>().ToArray();

            if (output.Length >= 5)
            {
                int valuesPerDetection = 5;
                int numDetections = output.Length / valuesPerDetection;

                for (int i = 0; i < numDetections; i++)
                {
                    var baseIdx = i * valuesPerDetection;
                    if (baseIdx + 4 >= output.Length) break;

                    var score = output[baseIdx + 4];
                    if (score < confidenceThreshold) continue;

                    var x1 = output[baseIdx] * scaleX;
                    var y1 = output[baseIdx + 1] * scaleY;
                    var x2 = output[baseIdx + 2] * scaleX;
                    var y2 = output[baseIdx + 3] * scaleY;

                    var boxWidth = x2 - x1;
                    var boxHeight = y2 - y1;

                    if (boxWidth <= 0 || boxHeight <= 0) continue;

                    double[]? landmarks = null;
                    if (valuesPerDetection > 5)
                    {
                        landmarks = new double[10];
                        for (int j = 0; j < 10 && (baseIdx + 5 + j) < output.Length; j++)
                        {
                            landmarks[j] = output[baseIdx + 5 + j] * (j % 2 == 0 ? scaleX : scaleY);
                        }
                    }

                    faces.Add(new DetectedFace
                    {
                        BoundingBoxX = Math.Max(0, x1) / originalWidth,
                        BoundingBoxY = Math.Max(0, y1) / originalHeight,
                        BoundingBoxWidth = Math.Min(boxWidth, originalWidth - x1) / originalWidth,
                        BoundingBoxHeight = Math.Min(boxHeight, originalHeight - y1) / originalHeight,
                        Confidence = score,
                        Landmarks = landmarks,
                        FaceSize = Math.Max(boxWidth, boxHeight),
                        ModelVersion = _modelVersion
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to post-process ONNX detection results");
        }

        return ApplyNms(faces, _modelProvider.GetNmsIouThreshold());
    }

    private static List<DetectedFace> ApplyNms(List<DetectedFace> faces, float iouThreshold)
    {
        var sorted = faces.OrderByDescending(f => f.Confidence).ToList();
        var result = new List<DetectedFace>();

        foreach (var face in sorted)
        {
            var isSuppressed = false;

            foreach (var existing in result)
            {
                var iou = CalculateIoU(face, existing);
                if (iou > iouThreshold)
                {
                    isSuppressed = true;
                    break;
                }
            }

            if (!isSuppressed)
                result.Add(face);
        }

        return result;
    }

    private static double CalculateIoU(DetectedFace a, DetectedFace b)
    {
        var x1 = Math.Max(a.BoundingBoxX, b.BoundingBoxX);
        var y1 = Math.Max(a.BoundingBoxY, b.BoundingBoxY);
        var x2 = Math.Min(a.BoundingBoxX + a.BoundingBoxWidth, b.BoundingBoxX + b.BoundingBoxWidth);
        var y2 = Math.Min(a.BoundingBoxY + a.BoundingBoxHeight, b.BoundingBoxY + b.BoundingBoxHeight);

        var intersection = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
        var areaA = a.BoundingBoxWidth * a.BoundingBoxHeight;
        var areaB = b.BoundingBoxWidth * b.BoundingBoxHeight;
        var union = areaA + areaB - intersection;

        return union > 0 ? intersection / union : 0;
    }

    private Task<IReadOnlyList<DetectedFace>> DetectWithFallbackAsync(byte[] imageData, CancellationToken cancellationToken)
    {
        return Task.Run<IReadOnlyList<DetectedFace>>(() => DetectWithFallback(imageData), cancellationToken);
    }

    private List<DetectedFace> DetectWithFallback(byte[] imageData)
    {
        try
        {
            using var stream = new MemoryStream(imageData);
            using var bitmap = new Bitmap(stream);

            var faces = new List<DetectedFace>();
            var sw = Stopwatch.StartNew();

            using var gray = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format8bppIndexed);
            using (var graphics = Graphics.FromImage(gray))
            {
                graphics.DrawImage(bitmap, 0, 0, bitmap.Width, bitmap.Height);
            }

            var windowSize = 24;
            var stepSize = 4;
            var scaleFactor = 1.2;

            for (var scale = 1.0; scale < 4.0; scale *= scaleFactor)
            {
                var scaledWidth = (int)(bitmap.Width / scale);
                var scaledHeight = (int)(bitmap.Height / scale);

                if (scaledWidth < windowSize || scaledHeight < windowSize)
                    break;

                for (var y = 0; y < scaledHeight - windowSize; y += stepSize)
                {
                    for (var x = 0; x < scaledWidth - windowSize; x += stepSize)
                    {
                        var confidence = CalculateWindowScore(gray, x, y, windowSize);

                        if (confidence > 0.3)
                        {
                            faces.Add(new DetectedFace
                            {
                                BoundingBoxX = (double)x / bitmap.Width,
                                BoundingBoxY = (double)y / bitmap.Height,
                                BoundingBoxWidth = (double)windowSize / bitmap.Width,
                                BoundingBoxHeight = (double)windowSize / bitmap.Height,
                                Confidence = confidence,
                                FaceSize = windowSize,
                                ModelVersion = "fallback-1.0"
                            });
                        }
                    }
                }
            }

            faces = ApplyNms(faces, 0.3f);

            sw.Stop();
            _lastInferenceTimeMs = sw.Elapsed.TotalMilliseconds;

            return faces;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Fallback detection failed");
            return [];
        }
    }

    private static double CalculateWindowScore(Bitmap gray, int x, int y, int windowSize)
    {
        double score = 0;
        int blockSize = 8;

        for (var by = 0; by < windowSize - blockSize; by += blockSize)
        {
            for (var bx = 0; bx < windowSize - blockSize; bx += blockSize)
            {
                var gradientX = 0.0;
                var gradientY = 0.0;

                for (var py = by; py < by + blockSize && y + py + 1 < gray.Height; py++)
                {
                    for (var px = bx; px < bx + blockSize && x + px + 1 < gray.Width; px++)
                    {
                        var pixel = gray.GetPixel(x + px, y + py);
                        var right = gray.GetPixel(x + px + 1, y + py);
                        var bottom = gray.GetPixel(x + px, y + py + 1);

                        gradientX += Math.Abs(right.R - pixel.R);
                        gradientY += Math.Abs(bottom.R - pixel.R);
                    }
                }

                var magnitude = Math.Sqrt(gradientX * gradientX + gradientY * gradientY);
                score += magnitude;
            }
        }

        return score / (windowSize * windowSize) * 10;
    }

    private InferenceSession GetSession()
    {
        _sessionLock.Wait();
        try
        {
            if (_sessionPool.TryTake(out var session))
                return session;

            var modelPath = _modelProvider.GetDetectionModelPath();
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
                    _logger.LogInformation("GPU execution providers not available, using CPU");
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
