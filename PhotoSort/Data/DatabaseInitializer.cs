using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace PhotoSort.Data;

public sealed class DatabaseInitializer
{
    private readonly IDbContextFactory<PhotoSortDbContext> _contextFactory;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(IDbContextFactory<PhotoSortDbContext> contextFactory, ILogger<DatabaseInitializer> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        try
        {
            await using var context = _contextFactory.CreateDbContext();
            await context.Database.MigrateAsync();

            _logger.LogInformation("Database initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize database");
            throw;
        }
    }
}
