using Microsoft.EntityFrameworkCore;
using PhotoSort.Models;

namespace PhotoSort.Data.Repositories;

public sealed class FaceEmbeddingRepository : Repository<FaceEmbedding>, IFaceEmbeddingRepository
{
    public FaceEmbeddingRepository(IDbContextFactory<PhotoSortDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<FaceEmbedding?> GetByFaceIdAsync(int faceId)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<FaceEmbedding>()
            .FirstOrDefaultAsync(e => e.FaceId == faceId);
    }

    public async Task<IReadOnlyList<FaceEmbedding>> GetByFaceIdsAsync(IEnumerable<int> faceIds)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<FaceEmbedding>()
            .Where(e => faceIds.Contains(e.FaceId))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<FaceEmbedding>> GetEmbeddingsForPersonAsync(int personId)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<FaceEmbedding>()
            .Where(e => context.PersonFaces.Any(pf => pf.PersonId == personId && pf.FaceId == e.FaceId))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<FaceEmbedding>> GetUnassignedEmbeddingsAsync(int limit = 1000)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<FaceEmbedding>()
            .Where(e => !context.PersonFaces.Any(pf => pf.FaceId == e.FaceId))
            .Take(limit)
            .ToListAsync();
    }

    public async Task<int> GetEmbeddingCountAsync()
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<FaceEmbedding>().CountAsync();
    }

    public async Task<float[]?> GetCentroidForPersonAsync(int personId)
    {
        var embeddings = await GetEmbeddingsForPersonAsync(personId);
        if (embeddings.Count == 0)
            return null;

        return ComputeCentroid(embeddings.Select(e => e.Embedding).ToList());
    }

    public async Task<IReadOnlyList<(int PersonId, float[] Centroid)>> GetAllPersonCentroidsAsync()
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        var personFaces = await context.PersonFaces
            .GroupBy(pf => pf.PersonId)
            .Select(g => new { PersonId = g.Key, FaceIds = g.Select(pf => pf.FaceId).ToList() })
            .ToListAsync();

        var results = new List<(int PersonId, float[] Centroid)>();

        foreach (var pf in personFaces)
        {
            var embeddings = await context.Set<FaceEmbedding>()
                .Where(e => pf.FaceIds.Contains(e.FaceId))
                .Select(e => e.Embedding)
                .ToListAsync();

            if (embeddings.Count > 0)
            {
                var centroid = ComputeCentroid(embeddings);
                results.Add((pf.PersonId, centroid));
            }
        }

        return results;
    }

    private static float[] ComputeCentroid(List<float[]> embeddings)
    {
        if (embeddings.Count == 0)
            return [];

        var dimension = embeddings[0].Length;
        var centroid = new float[dimension];

        foreach (var embedding in embeddings)
        {
            for (int i = 0; i < dimension; i++)
            {
                centroid[i] += embedding[i];
            }
        }

        for (int i = 0; i < dimension; i++)
        {
            centroid[i] /= embeddings.Count;
        }

        var magnitude = (float)Math.Sqrt(centroid.Sum(x => x * x));
        if (magnitude > 0)
        {
            for (int i = 0; i < dimension; i++)
            {
                centroid[i] /= magnitude;
            }
        }

        return centroid;
    }
}
