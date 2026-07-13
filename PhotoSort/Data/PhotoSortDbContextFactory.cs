using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PhotoSort.Data;

public sealed class PhotoSortDbContextFactory : IDesignTimeDbContextFactory<PhotoSortDbContext>
{
    public PhotoSortDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PhotoSortDbContext>();
        optionsBuilder.UseSqlite("Data Source=photosort.db");

        return new PhotoSortDbContext(optionsBuilder.Options);
    }
}
