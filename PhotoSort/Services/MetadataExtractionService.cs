using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PhotoSort.Data;
using PhotoSort.Models;

namespace PhotoSort.Services;

public sealed class MetadataExtractionService : IMetadataExtractionService
{
    private readonly IDbContextFactory<PhotoSortDbContext> _contextFactory;
    private readonly IPipelineMediator _pipelineMediator;
    private readonly ILogger<MetadataExtractionService> _logger;

    private const int BatchSize = 500;
    private const int ChannelCapacity = 10_000;
    private const int DefaultWorkerCount = 4;

    private static readonly HashSet<string> ImageExtensions =
    [
        ".jpg", ".jpeg", ".png", ".heic", ".webp", ".bmp"
    ];

    private static readonly HashSet<string> VideoExtensions =
    [
        ".mp4", ".mov", ".avi", ".mkv"
    ];

    private CancellationTokenSource? _cts;

    public bool IsExtracting { get; private set; }

    public MetadataExtractionService(
        IDbContextFactory<PhotoSortDbContext> contextFactory,
        IPipelineMediator pipelineMediator,
        ILogger<MetadataExtractionService> logger)
    {
        _contextFactory = contextFactory;
        _pipelineMediator = pipelineMediator;
        _logger = logger;
    }

    public async Task<ExtractionResult> ExtractMetadataAsync(
        int folderId,
        IProgress<MetadataExtractionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (IsExtracting)
            throw new InvalidOperationException("Metadata extraction is already in progress.");

        IsExtracting = true;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var stopwatch = Stopwatch.StartNew();
        var errors = new ConcurrentBag<string>();
        var progressState = new MetadataExtractionProgress();

        try
        {
            var photosToProcess = await GetUnprocessedPhotosAsync(folderId);
            progressState.FilesQueued = photosToProcess.Count;

            if (photosToProcess.Count == 0)
            {
                stopwatch.Stop();
                return new ExtractionResult
                {
                    Duration = stopwatch.Elapsed,
                    WasCancelled = false
                };
            }

            var channel = Channel.CreateBounded<Photo>(new BoundedChannelOptions(ChannelCapacity)
            {
                SingleReader = false,
                SingleWriter = true,
                FullMode = BoundedChannelFullMode.Wait
            });

            var producerTask = ProducePhotosAsync(photosToProcess, channel.Writer, _cts.Token);

            var workers = new Task[DefaultWorkerCount];
            for (int i = 0; i < DefaultWorkerCount; i++)
            {
                workers[i] = WorkerAsync(
                    channel.Reader, progressState, progress, _cts.Token, errors);
            }

            var consumerTask = Task.WhenAll(workers);

            await Task.WhenAll(producerTask, consumerTask);

            stopwatch.Stop();

            return new ExtractionResult
            {
                TotalProcessed = progressState.FilesProcessed,
                TotalSkipped = progressState.FilesSkipped,
                TotalFailed = progressState.FilesFailed,
                Duration = stopwatch.Elapsed,
                Errors = errors.ToList(),
                WasCancelled = _cts.Token.IsCancellationRequested
            };
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return new ExtractionResult
            {
                TotalProcessed = progressState.FilesProcessed,
                TotalSkipped = progressState.FilesSkipped,
                TotalFailed = progressState.FilesFailed,
                Duration = stopwatch.Elapsed,
                Errors = errors.ToList(),
                WasCancelled = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Metadata extraction failed for folder {FolderId}", folderId);
            stopwatch.Stop();
            return new ExtractionResult
            {
                TotalProcessed = progressState.FilesProcessed,
                TotalSkipped = progressState.FilesSkipped,
                TotalFailed = progressState.FilesFailed,
                Duration = stopwatch.Elapsed,
                Errors = [.. errors, ex.Message],
                WasCancelled = false
            };
        }
        finally
        {
            IsExtracting = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    public async Task<ExtractionResult> ExtractAllAsync(
        IProgress<MetadataExtractionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (IsExtracting)
            throw new InvalidOperationException("Metadata extraction is already in progress.");

        IsExtracting = true;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var stopwatch = Stopwatch.StartNew();
        var errors = new ConcurrentBag<string>();
        var progressState = new MetadataExtractionProgress();

        try
        {
            var photosToProcess = await GetAllUnprocessedPhotosAsync();
            progressState.FilesQueued = photosToProcess.Count;

            if (photosToProcess.Count == 0)
            {
                stopwatch.Stop();
                return new ExtractionResult
                {
                    Duration = stopwatch.Elapsed,
                    WasCancelled = false
                };
            }

            var channel = Channel.CreateBounded<Photo>(new BoundedChannelOptions(ChannelCapacity)
            {
                SingleReader = false,
                SingleWriter = true,
                FullMode = BoundedChannelFullMode.Wait
            });

            var producerTask = ProducePhotosAsync(photosToProcess, channel.Writer, _cts.Token);

            var workers = new Task[DefaultWorkerCount];
            for (int i = 0; i < DefaultWorkerCount; i++)
            {
                workers[i] = WorkerAsync(
                    channel.Reader, progressState, progress, _cts.Token, errors);
            }

            await Task.WhenAll(producerTask, Task.WhenAll(workers));

            stopwatch.Stop();

            return new ExtractionResult
            {
                TotalProcessed = progressState.FilesProcessed,
                TotalSkipped = progressState.FilesSkipped,
                TotalFailed = progressState.FilesFailed,
                Duration = stopwatch.Elapsed,
                Errors = errors.ToList(),
                WasCancelled = _cts.Token.IsCancellationRequested
            };
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return new ExtractionResult
            {
                TotalProcessed = progressState.FilesProcessed,
                TotalSkipped = progressState.FilesSkipped,
                TotalFailed = progressState.FilesFailed,
                Duration = stopwatch.Elapsed,
                Errors = errors.ToList(),
                WasCancelled = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Metadata extraction failed");
            stopwatch.Stop();
            return new ExtractionResult
            {
                TotalProcessed = progressState.FilesProcessed,
                TotalSkipped = progressState.FilesSkipped,
                TotalFailed = progressState.FilesFailed,
                Duration = stopwatch.Elapsed,
                Errors = [.. errors, ex.Message],
                WasCancelled = false
            };
        }
        finally
        {
            IsExtracting = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    public Task CancelExtractionAsync()
    {
        _cts?.Cancel();
        return Task.CompletedTask;
    }

    private async Task<List<Photo>> GetUnprocessedPhotosAsync(int folderId)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.Photos
            .Where(p => p.FolderId == folderId && p.State == ProcessingState.Indexed)
            .ToListAsync();
    }

    private async Task<List<Photo>> GetAllUnprocessedPhotosAsync()
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.Photos
            .Where(p => p.State == ProcessingState.Indexed)
            .ToListAsync();
    }

    private static async Task ProducePhotosAsync(
        List<Photo> photos,
        ChannelWriter<Photo> writer,
        CancellationToken ct)
    {
        try
        {
            foreach (var photo in photos)
            {
                ct.ThrowIfCancellationRequested();
                await writer.WriteAsync(photo, ct);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        finally
        {
            writer.Complete();
        }
    }

    private async Task WorkerAsync(
        ChannelReader<Photo> reader,
        MetadataExtractionProgress progressState,
        IProgress<MetadataExtractionProgress>? progress,
        CancellationToken ct,
        ConcurrentBag<string> errors)
    {
        var batch = new List<Photo>(BatchSize);

        try
        {
            await foreach (var photo in reader.ReadAllAsync(ct))
            {
                try
                {
                    var metadata = ExtractMetadataFromFile(photo);

                    if (metadata is not null)
                    {
                        ApplyMetadataToPhoto(photo, metadata);
                        _pipelineMediator.NotifyMetadataExtracted(metadata);
                    }

                    photo.State = ProcessingState.MetadataExtracted;
                    photo.MetadataExtractedDate = DateTime.UtcNow;

                    batch.Add(photo);
                    Interlocked.Increment(ref progressState.FilesProcessed);

                    if (batch.Count >= BatchSize)
                    {
                        await PersistBatchAsync(batch, errors);
                        batch.Clear();
                    }

                    progress?.Report(progressState);
                }
                catch (Exception ex)
                {
                    errors.Add($"Failed: {photo.FilePath} - {ex.Message}");
                    Interlocked.Increment(ref progressState.FilesFailed);

                    photo.State = ProcessingState.Failed;
                    photo.MetadataExtractedDate = DateTime.UtcNow;
                    batch.Add(photo);
                }
            }

            if (batch.Count > 0)
            {
                await PersistBatchAsync(batch, errors);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Worker failed");
            errors.Add($"Worker error: {ex.Message}");
        }
    }

    private async Task PersistBatchAsync(List<Photo> batch, ConcurrentBag<string> errors)
    {
        try
        {
            await using var context = _contextFactory.CreateDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                foreach (var photo in batch)
                {
                    var existing = await context.Photos
                        .FirstOrDefaultAsync(p => p.Id == photo.Id);

                    if (existing is not null)
                    {
                        existing.DateTaken = photo.DateTaken;
                        existing.Width = photo.Width;
                        existing.Height = photo.Height;
                        existing.CameraMake = photo.CameraMake;
                        existing.CameraModel = photo.CameraModel;
                        existing.Orientation = photo.Orientation;
                        existing.Latitude = photo.Latitude;
                        existing.Longitude = photo.Longitude;
                        existing.Duration = photo.Duration;
                        existing.State = photo.State;
                        existing.MetadataExtractedDate = photo.MetadataExtractedDate;
                    }
                }

                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist metadata batch of {Count} photos", batch.Count);
            errors.Add($"Batch persist failed: {ex.Message}");
        }
    }

    private PhotoMetadata? ExtractMetadataFromFile(Photo photo)
    {
        if (!File.Exists(photo.FilePath))
            return null;

        var extension = Path.GetExtension(photo.FilePath).ToLowerInvariant();

        return extension switch
        {
            ".jpg" or ".jpeg" or ".png" or ".heic" or ".webp" or ".bmp"
                => ExtractImageMetadata(photo.FilePath),
            ".mp4" or ".mov" or ".avi" or ".mkv"
                => ExtractVideoMetadata(photo.FilePath),
            _ => null
        };
    }

    [SupportedOSPlatform("windows")]
    private PhotoMetadata? ExtractImageMetadata(string filePath)
    {
        try
        {
            using var shell = new ShellFile(filePath);
            var props = shell.Properties;

            return new PhotoMetadata
            {
                FilePath = filePath,
                DateTaken = props.DateTaken,
                Width = props.Width,
                Height = props.Height,
                CameraMake = props.CameraMake,
                CameraModel = props.CameraModel,
                Orientation = props.Orientation,
                Latitude = props.Latitude,
                Longitude = props.Longitude
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to extract image metadata from: {FilePath}", filePath);
            return null;
        }
    }

    [SupportedOSPlatform("windows")]
    private PhotoMetadata? ExtractVideoMetadata(string filePath)
    {
        try
        {
            using var shell = new ShellFile(filePath);
            var props = shell.Properties;

            return new PhotoMetadata
            {
                FilePath = filePath,
                Width = props.Width,
                Height = props.Height,
                Duration = props.Duration
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to extract video metadata from: {FilePath}", filePath);
            return null;
        }
    }

    private static void ApplyMetadataToPhoto(Photo photo, PhotoMetadata metadata)
    {
        photo.DateTaken ??= metadata.DateTaken;
        photo.Width ??= metadata.Width;
        photo.Height ??= metadata.Height;
        photo.CameraMake ??= metadata.CameraMake;
        photo.CameraModel ??= metadata.CameraModel;
        photo.Orientation ??= metadata.Orientation;
        photo.Latitude ??= metadata.Latitude;
        photo.Longitude ??= metadata.Longitude;
        photo.Duration ??= metadata.Duration;
    }

    private sealed class ShellFile : IDisposable
    {
        private readonly string _filePath;
        private ShellProperties? _properties;

        public ShellProperties Properties => _properties ??= new ShellProperties(_filePath);

        public ShellFile(string filePath)
        {
            _filePath = filePath;
        }

        public void Dispose()
        {
            _properties?.Dispose();
        }
    }

    private sealed class ShellProperties : IDisposable
    {
        private readonly string _filePath;

        public DateTime? DateTaken { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public string? CameraMake { get; set; }
        public string? CameraModel { get; set; }
        public int? Orientation { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double? Duration { get; set; }

        public ShellProperties(string filePath)
        {
            _filePath = filePath;

            try
            {
                var file = new FileInfo(filePath);
                var extension = file.Extension.ToLowerInvariant();

                if (IsImageExtension(extension))
                {
                    ReadImageProperties(file);
                }
                else if (IsVideoExtension(extension))
                {
                    ReadVideoProperties(file);
                }
            }
            catch
            {
                // Swallow - individual file failures should not stop the pipeline
            }
        }

        private void ReadImageProperties(FileInfo file)
        {
            try
            {
                using var image = System.Drawing.Image.FromFile(file.FullName);

                Width = image.Width;
                Height = image.Height;

                foreach (var prop in image.PropertyItems)
                {
                    switch (prop.Id)
                    {
                        case 0x9003: // DateTimeOriginal
                            DateTaken = ParseExifDate(prop.Value);
                            break;
                        case 0x010F: // Make
                            CameraMake = prop.Value is not null
                                ? System.Text.Encoding.ASCII.GetString(prop.Value).TrimEnd('\0')
                                : null;
                            break;
                        case 0x0110: // Model
                            CameraModel = prop.Value is not null
                                ? System.Text.Encoding.ASCII.GetString(prop.Value).TrimEnd('\0')
                                : null;
                            break;
                        case 0x0112: // Orientation
                            Orientation = prop.Value?[0];
                            break;
                        case 0x8825: // GPS
                            ReadGpsFromExif(image);
                            break;
                    }
                }
            }
            catch
            {
                // Corrupt or locked image - continue
            }
        }

        private void ReadGpsFromExif(System.Drawing.Image image)
        {
            try
            {
                var gpsItem = image.PropertyItems.FirstOrDefault(p => p.Id == 0x8825);
                if (gpsItem?.Value is null || gpsItem.Value.Length < 4)
                    return;

                // Parse the GPS IFD structure
                // The value contains an IFD with tag count (2 bytes) followed by entries (12 bytes each)
                // Then a 4-byte offset to next IFD (usually 0)
                var data = gpsItem.Value;
                if (data.Length < 2) return;

                var tagCount = BitConverter.ToUInt16(data, 0);
                double? lat = null, lon = null;
                char? latRef = null, lonRef = null;

                for (int i = 0; i < tagCount; i++)
                {
                    var offset = 2 + (i * 12);
                    if (offset + 12 > data.Length) break;

                    var tag = BitConverter.ToUInt16(data, offset);
                    var type = BitConverter.ToUInt16(data, offset + 2);
                    var count = BitConverter.ToUInt32(data, offset + 4);
                    var valueOffset = offset + 8;

                    // Read value (inline if ≤4 bytes, otherwise offset from start of IFD)
                    byte[]? valueBytes = null;
                    if (count * GetTypeSize(type) <= 4)
                    {
                        valueBytes = data.Skip(valueOffset).Take((int)(count * GetTypeSize(type))).ToArray();
                    }
                    else
                    {
                        // Value is at an offset from the beginning of the TIFF header
                        // For GPS IFD embedded in EXIF, the offset is from the start of the GPS data
                        var externalOffset = BitConverter.ToUInt32(data, valueOffset);
                        if (externalOffset + count * GetTypeSize(type) <= data.Length)
                        {
                            valueBytes = data.Skip((int)externalOffset).Take((int)(count * GetTypeSize(type))).ToArray();
                        }
                    }

                    if (valueBytes is null) continue;

                    switch (tag)
                    {
                        case 0x0001: // GPSLatitudeRef
                            if (valueBytes.Length >= 1)
                                latRef = (char)valueBytes[0];
                            break;
                        case 0x0002: // GPSLatitude
                            lat = ParseGpsCoordinate(valueBytes, type, count);
                            break;
                        case 0x0003: // GPSLongitudeRef
                            if (valueBytes.Length >= 1)
                                lonRef = (char)valueBytes[0];
                            break;
                        case 0x0004: // GPSLongitude
                            lon = ParseGpsCoordinate(valueBytes, type, count);
                            break;
                    }
                }

                if (lat.HasValue && lon.HasValue)
                {
                    Latitude = latRef == 'S' ? -lat.Value : lat.Value;
                    Longitude = lonRef == 'W' ? -lon.Value : lon.Value;
                }
            }
            catch
            {
                // No GPS data or corrupt GPS data
            }
        }

        private static int GetTypeSize(ushort type) => type switch
        {
            1 => 1,  // BYTE
            2 => 1,  // ASCII
            3 => 2,  // SHORT
            4 => 4,  // LONG
            5 => 8,  // RATIONAL
            7 => 1,  // UNDEFINED
            9 => 4,  // SLONG
            10 => 8, // SRATIONAL
            _ => 1
        };

        private static double ParseGpsCoordinate(byte[] data, ushort type, uint count)
        {
            if (type == 5 && count == 3) // RATIONAL, 3 components (degrees, minutes, seconds)
            {
                if (data.Length < 24) return 0;

                var d = BitConverter.ToUInt32(data, 0) / (double)BitConverter.ToUInt32(data, 4);
                var m = BitConverter.ToUInt32(data, 8) / (double)BitConverter.ToUInt32(data, 12);
                var s = BitConverter.ToUInt32(data, 16) / (double)BitConverter.ToUInt32(data, 20);

                return d + (m / 60.0) + (s / 3600.0);
            }

            if (type == 10 && count == 3) // SRATIONAL
            {
                if (data.Length < 24) return 0;

                var d = BitConverter.ToInt32(data, 0) / (double)BitConverter.ToInt32(data, 4);
                var m = BitConverter.ToInt32(data, 8) / (double)BitConverter.ToInt32(data, 12);
                var s = BitConverter.ToInt32(data, 16) / (double)BitConverter.ToInt32(data, 20);

                return Math.Abs(d) + (Math.Abs(m) / 60.0) + (Math.Abs(s) / 3600.0);
            }

            return 0;
        }

        private void ReadVideoProperties(FileInfo file)
        {
            // Video metadata extraction via Windows Media Foundation would go here.
            // For now, we read basic file properties.
        }

        private static DateTime? ParseExifDate(byte[]? value)
        {
            if (value is null || value.Length < 19)
                return null;

            var str = System.Text.Encoding.ASCII.GetString(value).TrimEnd('\0');
            if (DateTime.TryParseExact(str, "yyyy:MM:dd HH:mm:ss",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var result))
            {
                return result;
            }
            return null;
        }

        private static double ConvertGpsToDegrees(byte[] value, bool isNegative)
        {
            if (value.Length < 24) return 0;

            var d = BitConverter.ToInt32(value, 0) / (double)BitConverter.ToInt32(value, 4);
            var m = BitConverter.ToInt32(value, 8) / (double)BitConverter.ToInt32(value, 12);
            var s = BitConverter.ToInt32(value, 16) / (double)BitConverter.ToInt32(value, 20);

            var degrees = d + (m / 60.0) + (s / 3600.0);
            return isNegative ? -degrees : degrees;
        }

        private static bool IsImageExtension(string ext) =>
            ext is ".jpg" or ".jpeg" or ".png" or ".heic" or ".webp" or ".bmp";

        private static bool IsVideoExtension(string ext) =>
            ext is ".mp4" or ".mov" or ".avi" or ".mkv";

        public void Dispose()
        {
        }
    }
}
