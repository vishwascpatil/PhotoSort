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
            _logger.LogError("Detection model not found at {Path}. Download may have failed.", modelPath);
            throw new InvalidOperationException(
                $"Face detection model not found at '{modelPath}'. " +
                "Ensure the model file is present or check network connectivity for automatic download.");
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
            throw;
        }
    }

    public async Task<IReadOnlyList<DetectedFace>> DetectAsync(
        byte[] imageData,
        CancellationToken cancellationToken = default)
    {
        if (!_isInitialized)
            throw new InvalidOperationException("Detector not initialized");

        if (_sessionPool.IsEmpty)
            return [];

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
                var inputName = session.InputMetadata.Keys.First();
                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor(inputName, inputTensor)
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
            _logger.LogError(ex, "ONNX detection failed");
            throw;
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
                var inputName = session.InputMetadata.Keys.First();
                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor(inputName, tensor)
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
            _logger.LogError(ex, "ONNX detection from pixels failed");
            throw;
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
                    var b = buffer[offset];
                    var g = buffer[offset + 1];
                    var r = buffer[offset + 2];
                    tensor[0, 0, y, x] = (r - 127.5f) / 128.0f;
                    tensor[0, 1, y, x] = (g - 127.5f) / 128.0f;
                    tensor[0, 2, y, x] = (b - 127.5f) / 128.0f;
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
        try
        {
            if (results.Count == 0)
                return [];

            var resultsList = results.ToList();
            var allFaces = new List<DetectedFace>();
            var confidenceThreshold = _modelProvider.GetDetectionConfidenceThreshold();

            // Try SCRFD flat format: 9 outputs (3 score + 3 bbox + 3 kps at strides 8/16/32)
            if (resultsList.Count == 9)
            {
                // Map each output by its first dimension (total anchors for that stride)
                var strides = new[] { 8, 16, 32 };
                int[] expectedCounts = [12800, 3200, 800]; // 2 anchors * grid_H * grid_W per stride

                for (int si = 0; si < strides.Length; si++)
                {
                    int stride = strides[si];
                    int expectedN = expectedCounts[si];
                    int gridSize = 640 / stride;

                    // Find score, bbox, kps outputs matching this stride's anchor count
                    var scoreOutput = resultsList.FirstOrDefault(r =>
                    {
                        var dims = r.AsTensor<float>().Dimensions;
                        return dims.Length == 2 && (int)dims[0] == expectedN && (int)dims[1] == 1;
                    });
                    var bboxOutput = resultsList.FirstOrDefault(r =>
                    {
                        var dims = r.AsTensor<float>().Dimensions;
                        return dims.Length == 2 && (int)dims[0] == expectedN && (int)dims[1] == 4;
                    });
                    var kpsOutput = resultsList.FirstOrDefault(r =>
                    {
                        var dims = r.AsTensor<float>().Dimensions;
                        return dims.Length == 2 && (int)dims[0] == expectedN && (int)dims[1] == 10;
                    });

                    if (scoreOutput == null || bboxOutput == null)
                        continue;

                    var scores = scoreOutput.AsTensor<float>();
                    var bboxes = bboxOutput.AsTensor<float>();
                    var kpsTensor = kpsOutput?.AsTensor<float>();

                    int numAnchorsPerCell = 2;
                    int totalCells = gridSize * gridSize;

                    // Debug: count high scores
                    int totalAnchors = totalCells * numAnchorsPerCell;
                    int highScores = 0;
                    for (int i = 0; i < totalAnchors; i++)
                        if (scores[i, 0] >= confidenceThreshold) highScores++;
                    for (int cellIdx = 0; cellIdx < totalCells; cellIdx++)
                    {
                        int row = cellIdx / gridSize;
                        int col = cellIdx % gridSize;
                        float cx = (col + 0.5f) * stride;
                        float cy = (row + 0.5f) * stride;

                        for (int a = 0; a < numAnchorsPerCell; a++)
                        {
                            int anchorIdx = cellIdx * numAnchorsPerCell + a;

                            float score = scores[anchorIdx, 0];

                            if (score < confidenceThreshold)
                                continue;

                            float l = bboxes[anchorIdx, 0];
                            float t = bboxes[anchorIdx, 1];
                            float r = bboxes[anchorIdx, 2];
                            float b = bboxes[anchorIdx, 3];

                            float x1 = (cx - l * stride) * scaleX;
                            float y1 = (cy - t * stride) * scaleY;
                            float x2 = (cx + r * stride) * scaleX;
                            float y2 = (cy + b * stride) * scaleY;

                            float boxWidth = x2 - x1;
                            float boxHeight = y2 - y1;

                            if (boxWidth <= 0 || boxHeight <= 0)
                                continue;

                            double[]? landmarks = null;
                            if (kpsTensor != null)
                            {
                                landmarks = new double[10];
                                for (int j = 0; j < 10; j++)
                                {
                                    float kp = kpsTensor[anchorIdx, j];
                                    landmarks[j] = (j % 2 == 0)
                                        ? (cx + kp * stride) * scaleX
                                        : (cy + kp * stride) * scaleY;
                                }
                            }

                            allFaces.Add(new DetectedFace
                            {
                                BoundingBoxX = Math.Max(0, x1) / originalWidth,
                                BoundingBoxY = Math.Max(0, y1) / originalHeight,
                                BoundingBoxWidth = Math.Min(boxWidth, originalWidth - x1) / originalWidth,
                                BoundingBoxHeight = Math.Min(boxHeight, originalHeight - y1) / originalHeight,
                                Confidence = score,
                                FaceSize = Math.Max(boxWidth, boxHeight),
                                Landmarks = landmarks,
                                ModelVersion = _modelVersion
                            });
                        }
                    }
                }

                return ApplyNms(allFaces, _modelProvider.GetNmsIouThreshold());
            }

            // Named multi-output format (YOLOv8-face: cls/obj/bbox/kps at strides 8/16/32)
            if (resultsList.Count >= 8)
            {
                var outputs = results.ToDictionary(r => r.Name, r => r.AsTensor<float>().ToArray());

                var strides = new[] { 8, 16, 32 };
                foreach (var stride in strides)
                {
                    if (!outputs.TryGetValue($"cls_{stride}", out var cls) ||
                        !outputs.TryGetValue($"obj_{stride}", out var obj) ||
                        !outputs.TryGetValue($"bbox_{stride}", out var bbox))
                        continue;

                    int gridSize = 640 / stride;
                    int numAnchors = gridSize * gridSize;

                    if (cls.Length < numAnchors || obj.Length < numAnchors || bbox.Length < numAnchors * 4)
                        continue;

                    bool sigmoidBaked = cls.Any(v => v > 1.0f || v < 0.0f);

                    for (int i = 0; i < numAnchors; i++)
                    {
                        float clsScore = cls[i];
                        if (!sigmoidBaked)
                            clsScore = 1f / (1f + MathF.Exp(-clsScore));

                        float objScore = obj[i];
                        if (!sigmoidBaked)
                            objScore = 1f / (1f + MathF.Exp(-objScore));

                        float score = clsScore * objScore;

                        if (score < confidenceThreshold)
                            continue;

                        int gy = i / gridSize;
                        int gx = i % gridSize;

                        float anchorX = (gx + 0.5f) * stride;
                        float anchorY = (gy + 0.5f) * stride;

                        int bIdx = i * 4;
                        float x1 = (anchorX - bbox[bIdx] * stride) * scaleX;
                        float y1 = (anchorY - bbox[bIdx + 1] * stride) * scaleY;
                        float x2 = (anchorX + bbox[bIdx + 2] * stride) * scaleX;
                        float y2 = (anchorY + bbox[bIdx + 3] * stride) * scaleY;

                        float boxWidth = x2 - x1;
                        float boxHeight = y2 - y1;

                        if (boxWidth <= 0 || boxHeight <= 0)
                            continue;

                        allFaces.Add(new DetectedFace
                        {
                            BoundingBoxX = Math.Max(0, x1) / originalWidth,
                            BoundingBoxY = Math.Max(0, y1) / originalHeight,
                            BoundingBoxWidth = Math.Min(boxWidth, originalWidth - x1) / originalWidth,
                            BoundingBoxHeight = Math.Min(boxHeight, originalHeight - y1) / originalHeight,
                            Confidence = score,
                            FaceSize = Math.Max(boxWidth, boxHeight),
                            ModelVersion = _modelVersion
                        });
                    }
                }

                return ApplyNms(allFaces, _modelProvider.GetNmsIouThreshold());
            }

            // Fallback for older model formats (single flat output)
            var output = results[0].AsTensor<float>().ToArray();
            return PostProcessFlatFormat(output, scaleX, scaleY, originalWidth, originalHeight);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to post-process ONNX detection results");
            return [];
        }
    }

    private List<DetectedFace> PostProcessFlatFormat(
        float[] output,
        float scaleX, float scaleY,
        int originalWidth, int originalHeight)
    {
        var faces = new List<DetectedFace>();
        var confidenceThreshold = _modelProvider.GetDetectionConfidenceThreshold();

        if (output.Length < 5)
            return faces;

        int valuesPerDetection = 5;
        int numDetections = output.Length / valuesPerDetection;

        for (int i = 0; i < numDetections; i++)
        {
            int baseIdx = i * valuesPerDetection;
            if (baseIdx + 4 >= output.Length) break;

            float score = output[baseIdx + 4];
            if (score < confidenceThreshold) continue;

            float x1 = output[baseIdx] * scaleX;
            float y1 = output[baseIdx + 1] * scaleY;
            float x2 = output[baseIdx + 2] * scaleX;
            float y2 = output[baseIdx + 3] * scaleY;

            float boxWidth = x2 - x1;
            float boxHeight = y2 - y1;

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
