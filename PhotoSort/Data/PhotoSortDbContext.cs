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
    }
}
