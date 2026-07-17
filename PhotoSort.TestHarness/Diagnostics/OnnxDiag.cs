using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using PhotoSort.Models;
using PhotoSort.Services;

namespace PhotoSort.TestHarness.Diagnostics;

public static class OnnxDiag
{
    private const string ModelsDir = @"C:\Users\vishw\AppData\Local\PhotoSort\Models";

    public static async Task RunAsync()
    {
        Console.WriteLine("\n=== ONNX Model Diagnostics ===\n");

        var detectionPath = Path.Combine(ModelsDir, "face_detection.onnx");
        var testPhoto = @"C:\Users\vishw\Downloads\Testfolder\IMG_9034.JPG";

        Console.WriteLine($"Detection model: {detectionPath}");
        Console.WriteLine($"  Exists: {File.Exists(detectionPath)}");

        if (File.Exists(detectionPath))
        {
            var fi = new FileInfo(detectionPath);
            Console.WriteLine($"  Size: {fi.Length:N0} bytes");

            try
            {
                var opts = new SessionOptions
                {
                    InterOpNumThreads = 4,
                    IntraOpNumThreads = 4,
                    GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
                };

                using var session = new InferenceSession(detectionPath, opts);
                var inputName = session.InputMetadata.Keys.First();

                Console.WriteLine($"  Model loaded OK. Opset: {session.ModelMetadata.Domain} Producer: {session.ModelMetadata.ProducerName} Version: {session.ModelMetadata.Version}");
                Console.WriteLine("  Inputs/Outputs:");
                foreach (var kv in session.InputMetadata)
                    Console.WriteLine($"    IN: '{kv.Key}' dims=[{string.Join(",", kv.Value.Dimensions.ToArray())}] type={kv.Value.OnnxValueType}");
                foreach (var kv in session.OutputMetadata)
                    Console.WriteLine($"    OUT: '{kv.Key}' dims=[{string.Join(",", kv.Value.Dimensions.ToArray())}] type={kv.Value.OnnxValueType}");

                    // Test with a real photo
                if (File.Exists(testPhoto))
                {
                    Console.WriteLine($"\n  Testing with photo: {testPhoto}");
                    var imageBytes = await File.ReadAllBytesAsync(testPhoto);
                    using var ms = new MemoryStream(imageBytes);
                    using var bitmap = new Bitmap(ms);

                    int inputW = 640, inputH = 640;
                    using var resized = new Bitmap(inputW, inputH, PixelFormat.Format24bppRgb);
                    using (var g = Graphics.FromImage(resized))
                    {
                        g.InterpolationMode = InterpolationMode.Bilinear;
                        g.DrawImage(bitmap, 0, 0, inputW, inputH);
                    }

                    var tensor = BitmapToTensor(resized);
                    var inputs = new List<NamedOnnxValue>
                    {
                        NamedOnnxValue.CreateFromTensor(inputName, tensor)
                    };

                    using var results = session.Run(inputs);
                    Console.WriteLine($"  Inference OK. {results.Count} outputs.");

                    var outputs = results.ToDictionary(r => r.Name, r => r.AsTensor<float>().ToArray());

                    float scaleX = (float)bitmap.Width / inputW;
                    float scaleY = (float)bitmap.Height / inputH;

                    // Verify actual tensor shapes
                    Console.WriteLine("\n  Actual output tensor shapes:");
                    foreach (var r in results)
                    {
                        var t = r.AsTensor<float>();
                        var dims = string.Join(",", t.Dimensions.ToArray());
                        Console.WriteLine($"    {r.Name}: shape=[{dims}] rank={t.Rank} length={t.Length}");
                    }

                    // Detect model format: SCRFD flat (numeric names) vs YOLOv8-face (named)
                    bool isScrfdFlat = results.All(r => int.TryParse(r.Name, out _));

                    if (isScrfdFlat)
                    {
                        Console.WriteLine("\n  Detected SCRFD flat format (numeric output names)");

                        // Test with a completely black image
                        Console.WriteLine("\n  --- Testing with black image ---");
                        var blackTensor = new DenseTensor<float>(new[] { 1, 3, 640, 640 });
                        var blackInputs = new List<NamedOnnxValue>
                        {
                            NamedOnnxValue.CreateFromTensor(inputName, blackTensor)
                        };
                        using var blackResults = session.Run(blackInputs);

                        var strides = new[] { 8, 16, 32 };
                        int[] expectedCounts = [12800, 3200, 800];

                        for (int si = 0; si < strides.Length; si++)
                        {
                            int expectedN = expectedCounts[si];
                            var scoreT = blackResults.FirstOrDefault(r =>
                            {
                                var d = r.AsTensor<float>().Dimensions;
                                return d.Length == 2 && (int)d[0] == expectedN && (int)d[1] == 1;
                            })?.AsTensor<float>();

                            if (scoreT == null) continue;

                            int n = (int)scoreT.Dimensions[0];
                            var vals = Enumerable.Range(0, Math.Min(n, 1000))
                                .Select(i => 1f / (1f + MathF.Exp(-scoreT[i, 0]))).ToList();
                            Console.WriteLine($"  Black image stride {strides[si]} score stats (first 1000): min={vals.Min():F6} max={vals.Max():F6} mean={vals.Average():F6}");

                            // Also check all scores for this stride
                            var allVals = Enumerable.Range(0, n)
                                .Select(i => 1f / (1f + MathF.Exp(-scoreT[i, 0]))).ToList();
                            Console.WriteLine($"  Black image stride {strides[si]} ALL scores: min={allVals.Min():F6} max={allVals.Max():F6} mean={allVals.Average():F6} unique={allVals.Distinct().Count()}");
                        }

                        // Normalization test: try (pixel-127.5)/128
                        Console.WriteLine("\n  --- SCRFD normalization test ---");
                        var scrfdTensor = BitmapToTensorScrfd(resized);

                        var scrfdInputs = new List<NamedOnnxValue>
                        {
                            NamedOnnxValue.CreateFromTensor(inputName, scrfdTensor)
                        };
                        using var scrfdResults = session.Run(scrfdInputs);

                        int totalHigh = 0;
                        for (int si = 0; si < strides.Length; si++)
                        {
                            int expectedN = expectedCounts[si];
                            var scoreT = scrfdResults.FirstOrDefault(r =>
                            {
                                var d = r.AsTensor<float>().Dimensions;
                                return d.Length == 2 && (int)d[0] == expectedN && (int)d[1] == 1;
                            })?.AsTensor<float>();

                            if (scoreT == null) continue;

                            int n = (int)scoreT.Dimensions[0];
                            var first100 = Enumerable.Range(0, Math.Min(n, 100))
                                .Select(i => 1f / (1f + MathF.Exp(-scoreT[i, 0]))).ToList();
                            Console.WriteLine($"  Stride {strides[si]} first 100 scores: min={first100.Min():F6} max={first100.Max():F6} mean={first100.Average():F6}");

                            int highCount = 0;
                            for (int i = 0; i < n; i++)
                            {
                                float s = 1f / (1f + MathF.Exp(-scoreT[i, 0]));
                                if (s >= 0.3f) highCount++;
                            }
                            Console.WriteLine($"  Stride {strides[si]}: {highCount}/{n} anchors >= 0.3");
                            totalHigh += highCount;
                        }
                        Console.WriteLine($"  Total high-score anchors: {totalHigh}");

                        // Try BGR channel ordering (OpenCV convention for SCRFD)
                        Console.WriteLine("\n  --- SCRFD normalization test (BGR order) ---");
                        var bgrTensor = BitmapToTensorScrfdBgr(resized);
                        var bgrInputs = new List<NamedOnnxValue>
                        {
                            NamedOnnxValue.CreateFromTensor(inputName, bgrTensor)
                        };
                        using var bgrResults = session.Run(bgrInputs);

                        int totalHighBgr = 0;
                        for (int si = 0; si < strides.Length; si++)
                        {
                            int expectedN = expectedCounts[si];
                            var scoreT = bgrResults.FirstOrDefault(r =>
                            {
                                var d = r.AsTensor<float>().Dimensions;
                                return d.Length == 2 && (int)d[0] == expectedN && (int)d[1] == 1;
                            })?.AsTensor<float>();

                            if (scoreT == null) continue;

                            int n = (int)scoreT.Dimensions[0];
                            var first100 = Enumerable.Range(0, Math.Min(n, 100))
                                .Select(i => 1f / (1f + MathF.Exp(-scoreT[i, 0]))).ToList();
                            Console.WriteLine($"  Stride {strides[si]} first 100 scores: min={first100.Min():F6} max={first100.Max():F6} mean={first100.Average():F6}");

                            int highCount = 0;
                            for (int i = 0; i < n; i++)
                            {
                                float s = 1f / (1f + MathF.Exp(-scoreT[i, 0]));
                                if (s >= 0.3f) highCount++;
                            }
                            Console.WriteLine($"  Stride {strides[si]}: {highCount}/{n} anchors >= 0.3");
                            totalHighBgr += highCount;
                        }
                        Console.WriteLine($"  Total high-score anchors (BGR): {totalHighBgr}");

                        // Count high-score anchors WITHOUT applying sigmoid (raw model outputs may be probabilities)
                        Console.WriteLine("\n  --- Counting raw scores (no sigmoid, treated as probability) ---");
                        using var rawResults = session.Run(inputs); // Re-run with raw [0,255]
                        for (int si = 0; si < strides.Length; si++)
                        {
                            int expectedN = expectedCounts[si];
                            var scoreT = rawResults.FirstOrDefault(r =>
                            {
                                var d = r.AsTensor<float>().Dimensions;
                                return d.Length == 2 && (int)d[0] == expectedN && (int)d[1] == 1;
                            })?.AsTensor<float>();

                            if (scoreT == null) continue;

                            int n = (int)scoreT.Dimensions[0];
                            float min = float.MaxValue, max = float.MinValue, sum = 0;
                            int highCount03 = 0, highCount05 = 0;
                            for (int i = 0; i < n; i++)
                            {
                                float v = scoreT[i, 0];
                                min = Math.Min(min, v);
                                max = Math.Max(max, v);
                                sum += v;
                                if (v >= 0.3f) highCount03++;
                                if (v >= 0.5f) highCount05++;
                            }
                            Console.WriteLine($"  Stride {strides[si]} raw: min={min:F6} max={max:F6} mean={sum/n:F6} >=0.3={highCount03} >=0.5={highCount05}");
                        }

                        // Same count with SCRFD normalization (pixel-127.5)/128
                        Console.WriteLine("\n  --- Counting scores with SCRFD norm, no sigmoid ---");
                        var scrfdNoSigTensor = BitmapToTensorScrfd(resized);
                        var scrfdNoSigInputs = new List<NamedOnnxValue>
                        {
                            NamedOnnxValue.CreateFromTensor(inputName, scrfdNoSigTensor)
                        };
                        using var scrfdNoSigResults = session.Run(scrfdNoSigInputs);
                        for (int si = 0; si < strides.Length; si++)
                        {
                            int expectedN = expectedCounts[si];
                            var scoreT = scrfdNoSigResults.FirstOrDefault(r =>
                            {
                                var d = r.AsTensor<float>().Dimensions;
                                return d.Length == 2 && (int)d[0] == expectedN && (int)d[1] == 1;
                            })?.AsTensor<float>();

                            if (scoreT == null) continue;

                            int n = (int)scoreT.Dimensions[0];
                            float min = float.MaxValue, max = float.MinValue, sum = 0;
                            int highCount03 = 0, highCount05 = 0;
                            for (int i = 0; i < n; i++)
                            {
                                float v = scoreT[i, 0];
                                min = Math.Min(min, v);
                                max = Math.Max(max, v);
                                sum += v;
                                if (v >= 0.3f) highCount03++;
                                if (v >= 0.5f) highCount05++;
                            }
                            Console.WriteLine($"  Stride {strides[si]} SCRFD-norm: min={min:F6} max={max:F6} mean={sum/n:F6} >=0.3={highCount03} >=0.5={highCount05}");
                        }
                    }
                    else
                    {
                        // YOLOv8-face format with named outputs (original diagnostic logic)
                        Console.WriteLine("\n  Detected YOLOv8-face format (named outputs)");

                        // Test with a completely black image to see model behavior
                        Console.WriteLine("\n  --- Testing with black image ---");
                        var blackTensor = new DenseTensor<float>(new[] { 1, 3, 640, 640 });
                        var blackInputs = new List<NamedOnnxValue>
                        {
                            NamedOnnxValue.CreateFromTensor(inputName, blackTensor)
                        };
                        using var blackResults = session.Run(blackInputs);
                        var blackOutputs = blackResults.ToDictionary(r => r.Name, r => r.AsTensor<float>().ToArray());
                        var blackCls = blackOutputs["cls_8"].Select(v => 1f / (1f + MathF.Exp(-v))).ToList();
                        Console.WriteLine($"  Black image CLS stats: min={blackCls.Min():F4} max={blackCls.Max():F4} mean={blackCls.Average():F4}");

                        // Test with random noise
                        Console.WriteLine("\n  --- Testing with random noise ---");
                        var rng = new Random(42);
                        var noiseTensor = new DenseTensor<float>(new[] { 1, 3, 640, 640 });
                        for (int i = 0; i < noiseTensor.Length; i++)
                            noiseTensor.SetValue(i, (float)rng.NextDouble());
                        var noiseInputs = new List<NamedOnnxValue>
                        {
                            NamedOnnxValue.CreateFromTensor(inputName, noiseTensor)
                        };
                        using var noiseResults = session.Run(noiseInputs);
                        var noiseOutputs = noiseResults.ToDictionary(r => r.Name, r => r.AsTensor<float>().ToArray());
                        var noiseCls = noiseOutputs["cls_8"].Select(v => 1f / (1f + MathF.Exp(-v))).ToList();
                        Console.WriteLine($"  Noise image CLS stats: min={noiseCls.Min():F4} max={noiseCls.Max():F4} mean={noiseCls.Average():F4}");

                        // Read full tensors to investigate all outputs
                        Console.WriteLine("\n  Investigating outputs at stride 8:");
                        var cls8Flat = outputs["cls_8"];
                        var obj8Flat = outputs["obj_8"];
                        var bbox8Flat = outputs["bbox_8"];

                        // Print unique obj values (should be constant if all 0)
                        var uniqueObj = obj8Flat.Take(1000).Distinct().ToList();
                        Console.WriteLine($"  OBJ unique values (first 1000): {string.Join(", ", uniqueObj.Take(5))}{(uniqueObj.Count > 5 ? "..." : "")} (count={uniqueObj.Count})");
                        if (uniqueObj.Count == 1)
                            Console.WriteLine($"  -> OBJ is CONSTANT (all anchors = {uniqueObj[0]})");

                        // Print unique cls values (first 20)
                        var uniqueCls = cls8Flat.Take(20).Distinct().ToList();
                        Console.WriteLine($"  CLS unique values (first 20): {string.Join(", ", uniqueCls.Take(5).Select(v => $"{v:F4}"))}... (count={uniqueCls.Count})");

                        // Check bbox ranges
                        var bboxes = bbox8Flat;
                        float minBx = float.MaxValue, maxBx = float.MinValue;
                        float minBy = float.MaxValue, maxBy = float.MinValue;
                        float minBw = float.MaxValue, maxBw = float.MinValue;
                        float minBh = float.MaxValue, maxBh = float.MinValue;
                        int numAnchors8 = 6400;
                        for (int i = 0; i < numAnchors8; i++)
                        {
                            int bi = i * 4;
                            minBx = Math.Min(minBx, bboxes[bi]);
                            maxBx = Math.Max(maxBx, bboxes[bi]);
                            minBy = Math.Min(minBy, bboxes[bi + 1]);
                            maxBy = Math.Max(maxBy, bboxes[bi + 1]);
                            minBw = Math.Min(minBw, bboxes[bi + 2]);
                            maxBw = Math.Max(maxBw, bboxes[bi + 2]);
                            minBh = Math.Min(minBh, bboxes[bi + 3]);
                            maxBh = Math.Max(maxBh, bboxes[bi + 3]);
                        }
                        Console.WriteLine($"  BBOX ranges: left=[{minBx:F4},{maxBx:F4}] top=[{minBy:F4},{maxBy:F4}] right=[{minBw:F4},{maxBw:F4}] bottom=[{minBh:F4},{maxBh:F4}]");

                        // Compute cls stats to understand distribution
                        var clsVals = cls8Flat.Select(v => 1f / (1f + MathF.Exp(-v))).ToList();
                        Console.WriteLine($"\n  CLS stats (stride 8, n={clsVals.Count}): min={clsVals.Min():F4} max={clsVals.Max():F4} mean={clsVals.Average():F4}");

                        // CLS range test: try different thresholds
                        var strides = new[] { 8, 16, 32 };

                        // Now try with BGR order and ImageNet normalization
                        Console.WriteLine("\n  --- Trying ImageNet normalization (BGR) ---");
                        var tensor2 = BitmapToTensorImageNet(resized);
                        var inputs2 = new List<NamedOnnxValue>
                        {
                            NamedOnnxValue.CreateFromTensor(inputName, tensor2)
                        };
                        using var results2 = session.Run(inputs2);
                        var outputs2 = results2.ToDictionary(r => r.Name, r => r.AsTensor<float>().ToArray());
                        var cls8b = outputs2["cls_8"];
                        var clsVals2 = cls8b.Select(v => 1f / (1f + MathF.Exp(-v))).ToList();
                        Console.WriteLine($"  CLS stats (ImageNet BGR, stride 8): min={clsVals2.Min():F4} max={clsVals2.Max():F4} mean={clsVals2.Average():F4}");

                        foreach (var thresh in new[] { 0.3f, 0.5f, 0.7f, 0.9f })
                        {
                            int total = 0;
                            foreach (var stride in strides)
                            {
                                if (!outputs2.TryGetValue($"cls_{stride}", out var cls)) continue;
                                int gridSize = 640 / stride;
                                int numAnchors = gridSize * gridSize;
                                for (int i = 0; i < numAnchors; i++)
                                {
                                    float s = 1f / (1f + MathF.Exp(-cls[i]));
                                    if (s >= thresh) total++;
                                }
                            }
                            Console.WriteLine($"  CLS >= {thresh:F1}: {total} anchors");
                        }

                        // Use the best normalization for full detection test
                        var bestOutputs = outputs;
                        var bestLabel = "[0,1] RGB";

                        if (clsVals2.Max() - clsVals2.Min() > clsVals.Max() - clsVals.Min())
                        {
                            bestOutputs = outputs2;
                            bestLabel = "ImageNet BGR";
                        }

                        Console.WriteLine($"\n  Using best normalization ({bestLabel}) for detection:");

                        var allFaces = new List<(float x1, float y1, float x2, float y2, float score)>();

                        foreach (var stride in strides)
                        {
                            if (!bestOutputs.TryGetValue($"cls_{stride}", out var cls) ||
                                !bestOutputs.TryGetValue($"bbox_{stride}", out var bbox))
                                continue;

                            int gridSize = 640 / stride;
                            int numAnchors = gridSize * gridSize;
                            int highScoreCount = 0;

                            for (int i = 0; i < numAnchors; i++)
                            {
                                float clsScore = 1f / (1f + MathF.Exp(-cls[i]));
                                if (clsScore >= 0.5f) highScoreCount++;

                                if (clsScore < 0.3f) continue;

                                int gy = i / gridSize;
                                int gx = i % gridSize;
                                float anchorX = (gx + 0.5f) * stride;
                                float anchorY = (gy + 0.5f) * stride;

                                int bIdx = i * 4;
                                float x1 = (anchorX - bbox[bIdx] * stride) * scaleX;
                                float y1 = (anchorY - bbox[bIdx + 1] * stride) * scaleY;
                                float x2 = (anchorX + bbox[bIdx + 2] * stride) * scaleX;
                                float y2 = (anchorY + bbox[bIdx + 3] * stride) * scaleY;

                                float w = x2 - x1;
                                float h = y2 - y1;
                                if (w <= 10 || h <= 10) continue;

                                allFaces.Add((x1, y1, x2, y2, clsScore));
                            }

                            Console.WriteLine($"  Stride {stride}: grid={gridSize}x{gridSize} anchors={numAnchors} highScores(>=0.5)={highScoreCount}");
                        }

                        Console.WriteLine($"  Total candidate faces (pre-NMS, min 10px): {allFaces.Count}");

                        var kept = ApplyNms(allFaces, 0.4f);
                        Console.WriteLine($"  Faces after NMS: {kept.Count}");

                        foreach (var face in kept.Take(10))
                        {
                            Console.WriteLine($"    Face: [{face.x1:F1},{face.y1:F1}]-[{face.x2:F1},{face.y2:F1}] score={face.score:F4} size={Math.Max(face.x2 - face.x1, face.y2 - face.y1):F0}px");
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"\n  Test photo not found: {testPhoto}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ERROR: {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine($"  Stack: {ex.StackTrace}");
            }
        }

        Console.WriteLine("\n=== Diagnostics Complete ===");
    }

    private static DenseTensor<float> BitmapToTensor(Bitmap bitmap)
    {
        int width = bitmap.Width, height = bitmap.Height;
        var tensor = new DenseTensor<float>(new[] { 1, 3, height, width });
        var bits = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

        try
        {
            int stride = bits.Stride;
            var buffer = new byte[stride * height];
            Marshal.Copy(bits.Scan0, buffer, 0, buffer.Length);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int offset = y * stride + x * 3;
                    float r = buffer[offset + 2];
                    float g = buffer[offset + 1];
                    float b = buffer[offset];

                    // RAW pixel values [0, 255] (no normalization)
                    tensor[0, 0, y, x] = r;
                    tensor[0, 1, y, x] = g;
                    tensor[0, 2, y, x] = b;
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(bits);
        }

        return tensor;
    }

    private static DenseTensor<float> BitmapToTensorImageNet(Bitmap bitmap)
    {
        int width = bitmap.Width, height = bitmap.Height;
        var tensor = new DenseTensor<float>(new[] { 1, 3, height, width });
        var bits = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

        try
        {
            int stride = bits.Stride;
            var buffer = new byte[stride * height];
            Marshal.Copy(bits.Scan0, buffer, 0, buffer.Length);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int offset = y * stride + x * 3;
                    float r = buffer[offset + 2] / 255.0f;
                    float g = buffer[offset + 1] / 255.0f;
                    float b = buffer[offset] / 255.0f;

                    // ImageNet normalization: BGR order for OpenCV compatibility
                    tensor[0, 0, y, x] = (b - 0.406f) / 0.225f;
                    tensor[0, 1, y, x] = (g - 0.456f) / 0.224f;
                    tensor[0, 2, y, x] = (r - 0.485f) / 0.229f;
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(bits);
        }

        return tensor;
    }

    private static DenseTensor<float> BitmapToTensorScrfd(Bitmap bitmap)
    {
        int width = bitmap.Width, height = bitmap.Height;
        var tensor = new DenseTensor<float>(new[] { 1, 3, height, width });
        var bits = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

        try
        {
            int stride = bits.Stride;
            var buffer = new byte[stride * height];
            Marshal.Copy(bits.Scan0, buffer, 0, buffer.Length);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int offset = y * stride + x * 3;
                    float r = buffer[offset + 2];
                    float g = buffer[offset + 1];
                    float b = buffer[offset];

                    // SCRFD normalization: (pixel - 127.5) / 128.0
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

    private static DenseTensor<float> BitmapToTensorScrfdBgr(Bitmap bitmap)
    {
        int width = bitmap.Width, height = bitmap.Height;
        var tensor = new DenseTensor<float>(new[] { 1, 3, height, width });
        var bits = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

        try
        {
            int stride = bits.Stride;
            var buffer = new byte[stride * height];
            Marshal.Copy(bits.Scan0, buffer, 0, buffer.Length);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int offset = y * stride + x * 3;
                    float r = buffer[offset + 2];
                    float g = buffer[offset + 1];
                    float b = buffer[offset];

                    // BGR order: channel 0 = B, channel 1 = G, channel 2 = R
                    tensor[0, 0, y, x] = (b - 127.5f) / 128.0f;
                    tensor[0, 1, y, x] = (g - 127.5f) / 128.0f;
                    tensor[0, 2, y, x] = (r - 127.5f) / 128.0f;
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(bits);
        }

        return tensor;
    }

    private static List<(float x1, float y1, float x2, float y2, float score)> ApplyNms(
        List<(float x1, float y1, float x2, float y2, float score)> faces, float iouThreshold)
    {
        var sorted = faces.OrderByDescending(f => f.score).ToList();
        var result = new List<(float x1, float y1, float x2, float y2, float score)>();

        foreach (var face in sorted)
        {
            bool suppressed = false;
            foreach (var existing in result)
            {
                float interX1 = Math.Max(face.x1, existing.x1);
                float interY1 = Math.Max(face.y1, existing.y1);
                float interX2 = Math.Min(face.x2, existing.x2);
                float interY2 = Math.Min(face.y2, existing.y2);
                float inter = Math.Max(0, interX2 - interX1) * Math.Max(0, interY2 - interY1);
                float areaA = (face.x2 - face.x1) * (face.y2 - face.y1);
                float areaB = (existing.x2 - existing.x1) * (existing.y2 - existing.y1);
                float iou = inter / (areaA + areaB - inter);

                if (iou > iouThreshold) { suppressed = true; break; }
            }
            if (!suppressed) result.Add(face);
        }

        return result;
    }
}
