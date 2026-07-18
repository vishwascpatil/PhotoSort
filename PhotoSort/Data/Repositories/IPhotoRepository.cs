using PhotoSort.Models;

namespace PhotoSort.Data.Repositories;

public interface IPhotoRepository : IRepository<Photo>
{
    Task<IReadOnlyList<Photo>> GetByFolderIdAsync(int folderId);
    Task<IReadOnlyList<Photo>> GetByDateRangeAsync(DateTime start, DateTime end);
    Task<IReadOnlyList<Photo>> GetFavoritesAsync();
    Task<Photo?> GetByFilePathAsync(string filePath);
    Task<IReadOnlyList<Photo>> GetByFolderIdWithDetailsAsync(int folderId);
    Task<Dictionary<string, Photo>> GetFilePathMapByFolderIdAsync(int folderId);
    Task<int> AddBatchAsync(IEnumerable<Photo> photos);
    Task<int> UpdateBatchAsync(IEnumerable<Photo> photos);
    Task<IReadOnlyList<Photo>> GetByStateAsync(ProcessingState state);
    Task<IReadOnlyList<Photo>> GetByFolderAndStateAsync(int folderId, ProcessingState state);
    Task<IReadOnlyList<Photo>> GetByStatesAsync(params ProcessingState[] states);
    Task<List<Photo>> GetIncompleteByFolderIdAsync(int folderId);
    Task<List<Photo>> GetAllIncompleteAsync();
    Task<int> UpdateStateBatchAsync(IEnumerable<(int Id, ProcessingState NewState)> updates);
    Task<int> UpdateMetadataBatchAsync(IEnumerable<Photo> photos);

    Task<IReadOnlyList<GalleryPhoto>> GetGalleryPageAsync(
        int skip,
        int take,
        GallerySortMode sortMode,
        int? folderId = null);

    Task<IReadOnlyList<GalleryPhoto>> GetGalleryPageAfterIdAsync(
        int afterId,
        int take,
        GallerySortMode sortMode,
        int? folderId = null);

    Task<int> GetGalleryCountAsync(int? folderId = null);

    Task<int> GetMaxIdAsync();
    Task<IReadOnlyList<int>> GetAllIdsAsync();
}
