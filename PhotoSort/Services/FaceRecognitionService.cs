using Microsoft.Extensions.Logging;
using PhotoSort.Models;

namespace PhotoSort.Services;

public sealed class FaceRecognitionService : IFaceRecognitionService
{
    private readonly ILogger<FaceRecognitionService> _logger;
    private bool _disposed;
    private bool _isInitialized;

    private const double DefaultSimilarityThreshold = 0.6;

    public bool IsInitialized => _isInitialized;

    public FaceRecognitionService(ILogger<FaceRecognitionService> logger)
    {
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized)
            return;

        try
        {
            _logger.LogInformation("Initializing face recognition service (clustering only)...");
            await Task.Delay(10, cancellationToken);
            _isInitialized = true;
            _logger.LogInformation("Face recognition service initialized");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize face recognition service");
            throw;
        }
    }

    public async Task<double> ComputeSimilarityAsync(
        float[] embedding1,
        float[] embedding2)
    {
        return await Task.Run(() =>
        {
            if (embedding1.Length != embedding2.Length)
                return 0;

            double dotProduct = 0;
            double norm1 = 0;
            double norm2 = 0;

            for (int i = 0; i < embedding1.Length; i++)
            {
                dotProduct += embedding1[i] * embedding2[i];
                norm1 += embedding1[i] * embedding1[i];
                norm2 += embedding2[i] * embedding2[i];
            }

            var magnitude = Math.Sqrt(norm1) * Math.Sqrt(norm2);
            return magnitude > 0 ? dotProduct / magnitude : 0;
        });
    }

    public async Task<IReadOnlyList<ClusterResult>> ClusterEmbeddingsAsync(
        IReadOnlyList<FaceEmbedding> embeddings,
        double similarityThreshold = 0.6,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            if (embeddings.Count == 0)
                return [];

            var clusters = new List<List<int>>();
            var assigned = new HashSet<int>();

            for (int i = 0; i < embeddings.Count; i++)
            {
                if (assigned.Contains(i))
                    continue;

                var cluster = new List<int> { i };
                assigned.Add(i);

                for (int j = i + 1; j < embeddings.Count; j++)
                {
                    if (assigned.Contains(j))
                        continue;

                    var similarity = CosineSimilarity(embeddings[i].Embedding, embeddings[j].Embedding);

                    if (similarity >= similarityThreshold)
                    {
                        cluster.Add(j);
                        assigned.Add(j);
                    }
                }

                clusters.Add(cluster);
            }

            var results = new List<ClusterResult>();

            for (int i = 0; i < clusters.Count; i++)
            {
                var clusterEmbeddings = clusters[i]
                    .Select(idx => embeddings[idx].Embedding)
                    .ToList();

                var centroid = ComputeCentroid(clusterEmbeddings);

                results.Add(new ClusterResult
                {
                    ClusterId = i,
                    FaceIds = clusters[i].Select(idx => embeddings[idx].FaceId).ToList(),
                    Centroid = centroid,
                    Size = clusters[i].Count
                });
            }

            return results.OrderByDescending(c => c.Size).ToList();
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<ClusterResult>> ClusterWithExistingPeopleAsync(
        IReadOnlyList<FaceEmbedding> newEmbeddings,
        IReadOnlyList<(int PersonId, float[] Centroid)> existingPeople,
        double similarityThreshold = 0.6,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var results = new List<ClusterResult>();
            var assigned = new HashSet<int>();

            var personClusters = new Dictionary<int, List<int>>();

            for (int i = 0; i < newEmbeddings.Count; i++)
            {
                if (assigned.Contains(i))
                    continue;

                double bestSimilarity = -1;
                int bestPersonId = -1;

                foreach (var (personId, centroid) in existingPeople)
                {
                    var similarity = CosineSimilarity(newEmbeddings[i].Embedding, centroid);
                    if (similarity > bestSimilarity)
                    {
                        bestSimilarity = similarity;
                        bestPersonId = personId;
                    }
                }

                if (bestSimilarity >= similarityThreshold && bestPersonId >= 0)
                {
                    if (!personClusters.ContainsKey(bestPersonId))
                    {
                        personClusters[bestPersonId] = [];
                    }
                    personClusters[bestPersonId].Add(i);
                    assigned.Add(i);
                }
            }

            foreach (var (personId, faceIndices) in personClusters)
            {
                var clusterEmbeddings = faceIndices
                    .Select(idx => newEmbeddings[idx].Embedding)
                    .ToList();

                var centroid = ComputeCentroid(clusterEmbeddings);

                results.Add(new ClusterResult
                {
                    ClusterId = personId,
                    FaceIds = faceIndices.Select(idx => newEmbeddings[idx].FaceId).ToList(),
                    Centroid = centroid,
                    Size = faceIndices.Count
                });
            }

            var unassignedIndices = new List<int>();
            for (int i = 0; i < newEmbeddings.Count; i++)
            {
                if (!assigned.Contains(i))
                {
                    unassignedIndices.Add(i);
                }
            }

            if (unassignedIndices.Count > 0)
            {
                var newClusters = ClusterNewFacesOnly(
                    unassignedIndices.Select(idx => newEmbeddings[idx]).ToList(),
                    unassignedIndices,
                    similarityThreshold);

                results.AddRange(newClusters);
            }

            return results.OrderByDescending(c => c.Size).ToList();
        }, cancellationToken);
    }

    private static List<ClusterResult> ClusterNewFacesOnly(
        IReadOnlyList<FaceEmbedding> embeddings,
        IReadOnlyList<int> originalIndices,
        double similarityThreshold)
    {
        var clusters = new List<List<int>>();
        var assigned = new HashSet<int>();

        for (int i = 0; i < embeddings.Count; i++)
        {
            if (assigned.Contains(i))
                continue;

            var cluster = new List<int> { i };
            assigned.Add(i);

            for (int j = i + 1; j < embeddings.Count; j++)
            {
                if (assigned.Contains(j))
                    continue;

                var similarity = CosineSimilarity(embeddings[i].Embedding, embeddings[j].Embedding);

                if (similarity >= similarityThreshold)
                {
                    cluster.Add(j);
                    assigned.Add(j);
                }
            }

            clusters.Add(cluster);
        }

        var results = new List<ClusterResult>();

        for (int i = 0; i < clusters.Count; i++)
        {
            var clusterEmbeddings = clusters[i]
                .Select(idx => embeddings[idx].Embedding)
                .ToList();

            var centroid = ComputeCentroid(clusterEmbeddings);

            results.Add(new ClusterResult
            {
                ClusterId = -(i + 1),
                FaceIds = clusters[i].Select(idx => originalIndices[idx]).ToList(),
                Centroid = centroid,
                Size = clusters[i].Count
            });
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

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            return 0;

        double dotProduct = 0;
        double normA = 0;
        double normB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        var magnitude = Math.Sqrt(normA) * Math.Sqrt(normB);
        return magnitude > 0 ? dotProduct / magnitude : 0;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
    }
}
