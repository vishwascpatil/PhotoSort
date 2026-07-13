namespace PhotoSort.Models;

public enum ProcessingState
{
    NotIndexed = 0,
    Indexed = 1,
    MetadataExtracted = 2,
    ThumbnailGenerated = 3,
    FaceProcessed = 4,
    TagProcessed = 5,
    Failed = 99
}
