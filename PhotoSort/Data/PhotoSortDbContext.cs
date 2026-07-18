using Microsoft.EntityFrameworkCore;
using PhotoSort.Data.Configurations;
using PhotoSort.Models;

namespace PhotoSort.Data;

public sealed class PhotoSortDbContext : DbContext
{
    public DbSet<Folder> Folders => Set<Folder>();
    public DbSet<Photo> Photos => Set<Photo>();
    public DbSet<Person> People => Set<Person>();
    public DbSet<Face> Faces => Set<Face>();
    public DbSet<PersonFace> PersonFaces => Set<PersonFace>();
    public DbSet<FaceEmbedding> FaceEmbeddings => Set<FaceEmbedding>();
    public DbSet<Place> Places => Set<Place>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<PhotoPlace> PhotoPlaces => Set<PhotoPlace>();
    public DbSet<PhotoTag> PhotoTags => Set<PhotoTag>();
    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<TripPhoto> TripPhotos => Set<TripPhoto>();
    public DbSet<TripPlace> TripPlaces => Set<TripPlace>();
    public DbSet<Memory> Memories => Set<Memory>();
    public DbSet<MemorySchedule> MemorySchedules => Set<MemorySchedule>();
    public DbSet<PhotoSignal> PhotoSignals => Set<PhotoSignal>();
    public DbSet<PhotoInteraction> PhotoInteractions => Set<PhotoInteraction>();
    public DbSet<UnknownFaceCluster> UnknownFaceClusters => Set<UnknownFaceCluster>();

    public DbSet<MemoryTypeFamily> MemoryTypeFamilies => Set<MemoryTypeFamily>();
    public DbSet<MemoryTypeEntity> MemoryTypes => Set<MemoryTypeEntity>();
    public DbSet<MemoryPhoto> MemoryPhotos => Set<MemoryPhoto>();
    public DbSet<MemoryItem> MemoryItems => Set<MemoryItem>();
    public DbSet<MemoryScore> MemoryScores => Set<MemoryScore>();
    public DbSet<MemoryCacheEntry> MemoryCacheEntries => Set<MemoryCacheEntry>();
    public DbSet<MemoryGenerationHistory> MemoryGenerationHistories => Set<MemoryGenerationHistory>();
    public DbSet<MemoryPreferences> MemoryPreferences => Set<MemoryPreferences>();
    public DbSet<MemoryFeedback> MemoryFeedback => Set<MemoryFeedback>();
    public DbSet<MemoryStatistics> MemoryStatistics => Set<MemoryStatistics>();

    public PhotoSortDbContext(DbContextOptions<PhotoSortDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new FolderConfiguration());
        modelBuilder.ApplyConfiguration(new PhotoConfiguration());
        modelBuilder.ApplyConfiguration(new PersonConfiguration());
        modelBuilder.ApplyConfiguration(new FaceConfiguration());
        modelBuilder.ApplyConfiguration(new PersonFaceConfiguration());
        modelBuilder.ApplyConfiguration(new FaceEmbeddingConfiguration());
        modelBuilder.ApplyConfiguration(new PlaceConfiguration());
        modelBuilder.ApplyConfiguration(new TagConfiguration());
        modelBuilder.ApplyConfiguration(new PhotoPlaceConfiguration());
        modelBuilder.ApplyConfiguration(new PhotoTagConfiguration());
        modelBuilder.ApplyConfiguration(new TripConfiguration());
        modelBuilder.ApplyConfiguration(new TripPhotoConfiguration());
        modelBuilder.ApplyConfiguration(new TripPlaceConfiguration());

        modelBuilder.ApplyConfiguration(new PhotoSignalConfiguration());
        modelBuilder.ApplyConfiguration(new MemoryTypeFamilyConfiguration());
        modelBuilder.ApplyConfiguration(new MemoryTypeEntityConfiguration());
        modelBuilder.ApplyConfiguration(new MemoryConfiguration());
        modelBuilder.ApplyConfiguration(new MemoryPhotoConfiguration());
        modelBuilder.ApplyConfiguration(new MemoryItemConfiguration());
        modelBuilder.ApplyConfiguration(new MemoryScoreConfiguration());
        modelBuilder.ApplyConfiguration(new MemoryCacheEntryConfiguration());
        modelBuilder.ApplyConfiguration(new MemoryGenerationHistoryConfiguration());
        modelBuilder.ApplyConfiguration(new MemoryPreferencesConfiguration());
        modelBuilder.ApplyConfiguration(new MemoryFeedbackConfiguration());
        modelBuilder.ApplyConfiguration(new MemoryStatisticsConfiguration());
    }
}
