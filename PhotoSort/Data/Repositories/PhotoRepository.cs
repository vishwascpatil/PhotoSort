using Microsoft.EntityFrameworkCore;
using PhotoSort.Models;

namespace PhotoSort.Data.Repositories;

public sealed class PhotoRepository : Repository<Photo>, IPhotoRepository
{
    public PhotoRepository(IDbContextFactory<PhotoSortDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<IReadOnlyList<Photo>> GetByFolderIdAsync(int folderId)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<Photo>()
            .Where(p => p.FolderId == folderId)
            .OrderByDescending(p => p.DateTaken)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Photo>> GetByDateRangeAsync(DateTime start, DateTime end)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<Photo>()
            .Where(p => p.DateTaken >= start && p.DateTaken <= end)
            .OrderByDescending(p => p.DateTaken)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Photo>> GetFavoritesAsync()
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<Photo>()
            .Where(p => p.IsFavorite)
            .OrderByDescending(p => p.DateTaken)
            .ToListAsync();
    }

    public async Task<Photo?> GetByFilePathAsync(string filePath)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<Photo>()
            .FirstOrDefaultAsync(p => p.FilePath == filePath);
    }

    public async Task<IReadOnlyList<Photo>> GetByFolderIdWithDetailsAsync(int folderId)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<Photo>()
            .Include(p => p.Folder)
            .Include(p => p.Faces).ThenInclude(f => f.PersonFaces).ThenInclude(pf => pf.Person)
            .Include(p => p.PhotoPlaces).ThenInclude(pp => pp.Place)
            .Include(p => p.PhotoTags).ThenInclude(pt => pt.Tag)
            .Where(p => p.FolderId == folderId)
            .OrderByDescending(p => p.DateTaken)
            .ToListAsync();
    }

    public async Task<Dictionary<string, Photo>> GetFilePathMapByFolderIdAsync(int folderId)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        var photos = await context.Set<Photo>()
            .Where(p => p.FolderId == folderId)
            .ToListAsync();

        return photos.ToDictionary(p => p.FilePath, p => p);
    }

    public async Task<int> AddBatchAsync(IEnumerable<Photo> photos)
    {
        var photoList = photos.ToList();
        if (photoList.Count == 0) return 0;

        await using var context = await ContextFactory.CreateDbContextAsync();
        await context.Set<Photo>().AddRangeAsync(photoList);
        return await context.SaveChangesAsync();
    }

    public async Task<int> UpdateBatchAsync(IEnumerable<Photo> photos)
    {
        var photoList = photos.ToList();
        if (photoList.Count == 0) return 0;

        await using var context = await ContextFactory.CreateDbContextAsync();
        context.Set<Photo>().UpdateRange(photoList);
        return await context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<Photo>> GetByStateAsync(ProcessingState state)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<Photo>()
            .Where(p => p.State == state)
            .OrderByDescending(p => p.DateTaken)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Photo>> GetByFolderAndStateAsync(int folderId, ProcessingState state)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<Photo>()
            .Where(p => p.FolderId == folderId && p.State == state)
            .OrderByDescending(p => p.DateTaken)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Photo>> GetByStatesAsync(params ProcessingState[] states)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<Photo>()
            .Where(p => states.Contains(p.State))
            .OrderByDescending(p => p.DateTaken)
            .ToListAsync();
    }

    public async Task<List<Photo>> GetIncompleteByFolderIdAsync(int folderId)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<Photo>()
            .Where(p => p.FolderId == folderId && p.State != ProcessingState.TagProcessed)
            .ToListAsync();
    }

    public async Task<List<Photo>> GetAllIncompleteAsync()
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<Photo>()
            .Where(p => p.State != ProcessingState.TagProcessed)
            .ToListAsync();
    }

    public async Task<int> UpdateStateBatchAsync(IEnumerable<(int Id, ProcessingState NewState)> updates)
    {
        var updateList = updates.ToList();
        if (updateList.Count == 0) return 0;

        await using var context = await ContextFactory.CreateDbContextAsync();
        var ids = updateList.Select(u => u.Id).ToList();
        var photos = await context.Set<Photo>().Where(p => ids.Contains(p.Id)).ToListAsync();

        foreach (var photo in photos)
        {
            var match = updateList.FirstOrDefault(u => u.Id == photo.Id);
            if (match.Id != 0)
                photo.State = match.NewState;
        }

        return await context.SaveChangesAsync();
    }

    public async Task<int> UpdateMetadataBatchAsync(IEnumerable<Photo> photos)
    {
        var photoList = photos.ToList();
        if (photoList.Count == 0) return 0;

        await using var context = await ContextFactory.CreateDbContextAsync();
        foreach (var photo in photoList)
        {
            var existing = await context.Set<Photo>().FirstOrDefaultAsync(p => p.Id == photo.Id);
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

        return await context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<GalleryPhoto>> GetGalleryPageAsync(
        int skip,
        int take,
        GallerySortMode sortMode)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        var query = context.Set<Photo>().AsNoTracking();

        query = sortMode == GallerySortMode.NewestFirst
            ? query.OrderByDescending(p => p.DateTaken ?? p.ModifiedDateUtc)
                    .ThenByDescending(p => p.Id)
            : query.OrderBy(p => p.DateTaken ?? p.ModifiedDateUtc)
                    .ThenBy(p => p.Id);

        return await query
            .Skip(skip)
            .Take(take)
            .Select(p => new GalleryPhoto
            {
                Id = p.Id,
                FilePath = p.FilePath,
                FileName = p.FileName,
                Extension = p.Extension,
                DateTaken = p.DateTaken,
                Width = p.Width,
                Height = p.Height,
                FileSize = p.FileSize,
                ThumbnailPath = p.ThumbnailPath,
                ThumbnailSmallPath = p.ThumbnailSmallPath,
                ThumbnailMediumPath = p.ThumbnailMediumPath,
                VideoThumbnailSmallPath = p.VideoThumbnailSmallPath,
                VideoThumbnailMediumPath = p.VideoThumbnailMediumPath,
                VideoThumbnailLargePath = p.VideoThumbnailLargePath,
                PreviewClipPath = p.PreviewClipPath,
                IsFavorite = p.IsFavorite,
                ModifiedDateUtc = p.ModifiedDateUtc,
                FolderId = p.FolderId,
                State = p.State,
                DateTakenYear = p.DateTaken != null ? p.DateTaken.Value.Year : (int?)null,
                DateTakenMonth = p.DateTaken != null ? p.DateTaken.Value.Month : (int?)null,
                DateTakenDay = p.DateTaken != null ? p.DateTaken.Value.Day : (int?)null
            })
            .ToListAsync();
    }

    public async Task<IReadOnlyList<GalleryPhoto>> GetGalleryPageAfterIdAsync(
        int afterId,
        int take,
        GallerySortMode sortMode)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        var query = context.Set<Photo>().AsNoTracking();

        query = sortMode == GallerySortMode.NewestFirst
            ? query.Where(p => p.Id < afterId)
                    .OrderByDescending(p => p.DateTaken ?? p.ModifiedDateUtc)
                    .ThenByDescending(p => p.Id)
            : query.Where(p => p.Id > afterId)
                    .OrderBy(p => p.DateTaken ?? p.ModifiedDateUtc)
                    .ThenBy(p => p.Id);

        return await query
            .Take(take)
            .Select(p => new GalleryPhoto
            {
                Id = p.Id,
                FilePath = p.FilePath,
                FileName = p.FileName,
                Extension = p.Extension,
                DateTaken = p.DateTaken,
                Width = p.Width,
                Height = p.Height,
                FileSize = p.FileSize,
                ThumbnailPath = p.ThumbnailPath,
                ThumbnailSmallPath = p.ThumbnailSmallPath,
                ThumbnailMediumPath = p.ThumbnailMediumPath,
                VideoThumbnailSmallPath = p.VideoThumbnailSmallPath,
                VideoThumbnailMediumPath = p.VideoThumbnailMediumPath,
                VideoThumbnailLargePath = p.VideoThumbnailLargePath,
                PreviewClipPath = p.PreviewClipPath,
                IsFavorite = p.IsFavorite,
                ModifiedDateUtc = p.ModifiedDateUtc,
                FolderId = p.FolderId,
                State = p.State,
                DateTakenYear = p.DateTaken != null ? p.DateTaken.Value.Year : (int?)null,
                DateTakenMonth = p.DateTaken != null ? p.DateTaken.Value.Month : (int?)null,
                DateTakenDay = p.DateTaken != null ? p.DateTaken.Value.Day : (int?)null
            })
            .ToListAsync();
    }

    public async Task<int> GetGalleryCountAsync()
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<Photo>().AsNoTracking().CountAsync();
    }

    public async Task<int> GetMaxIdAsync()
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<Photo>().AsNoTracking().MaxAsync(p => p.Id);
    }
}
