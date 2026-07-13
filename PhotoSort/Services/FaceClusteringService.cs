using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using PhotoSort.Models;

namespace PhotoSort.Services;

public sealed class FaceClusteringService : IFaceClusteringService
{
    private readonly ILogger<FaceClusteringService> _logger;

    public FaceClusteringService(ILogger<FaceClusteringService> logger)
    {
        _logger = logger;
    }

    public Task<IReadOnlyList<ClusterResult>> ClusterAsync(
        IReadOnlyList<FaceEmbedding> embeddings,
        double similarityThreshold = 0.6,
        CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<ClusterResult>>(() => ClusterInternal(embeddings, similarityThreshold, cancellationToken), cancellationToken);
    }

    public Task<IReadOnlyList<ClusterResult>> ClusterWithExistingAsync(
        IReadOnlyList<FaceEmbedding> newEmbeddings,
        IReadOnlyList<(int PersonId, float[] Centroid)> existingPeople,
        double similarityThreshold = 0.6,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var results = new List<ClusterResult>();
            var unassigned = new List<int>();

            for (int i = 0; i < newEmbeddings.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var embedding = newEmbeddings[i];
                if (embedding.Embedding == null || embedding.Embedding.Length == 0)
                    continue;

                int bestPersonId = -1;
                double bestSimilarity = similarityThreshold;

                foreach (var (personId, centroid) in existingPeople)
                {
                    var similarity = ComputeSimilarity(embedding.Embedding, centroid);
                    if (similarity > bestSimilarity)
                    {
                        bestSimilarity = similarity;
                        bestPersonId = personId;
                    }
                }

                if (bestPersonId >= 0)
                {
                    results.Add(new ClusterResult
                    {
                        FaceId = embedding.FaceId,
                        PersonId = bestPersonId,
                        Similarity = bestSimilarity,
                        IsNewPerson = false
                    });
                }
                else
                {
                    unassigned.Add(i);
                }
            }

            if (unassigned.Count > 0)
            {
                var unassignedEmbeddings = unassigned.Select(i => newEmbeddings[i]).ToList();
                var newClusters = ClusterInternal(unassignedEmbeddings, similarityThreshold, cancellationToken);

                var maxPersonId = existingPeople.Count > 0
                    ? existingPeople.Max(p => p.PersonId)
                    : 0;

                foreach (var cluster in newClusters)
                {
                    results.Add(new ClusterResult
                    {
                        FaceId = cluster.FaceId,
                        PersonId = maxPersonId + cluster.PersonId,
                        Similarity = cluster.Similarity,
                        IsNewPerson = true
                    });
                }
            }

            return (IReadOnlyList<ClusterResult>)results;
        }, cancellationToken);
    }

    public double ComputeSimilarity(float[] embedding1, float[] embedding2)
    {
        if (embedding1.Length != embedding2.Length)
        {
            var minLen = Math.Min(embedding1.Length, embedding2.Length);
            var e1 = new float[minLen];
            var e2 = new float[minLen];
            Array.Copy(embedding1, e1, minLen);
            Array.Copy(embedding2, e2, minLen);
            embedding1 = e1;
            embedding2 = e2;
        }

        double dotProduct = 0;
        double norm1 = 0;
        double norm2 = 0;

        for (int i = 0; i < embedding1.Length; i++)
        {
            dotProduct += embedding1[i] * embedding2[i];
            norm1 += embedding1[i] * embedding1[i];
            norm2 += embedding2[i] * embedding2[i];
        }

        var denominator = Math.Sqrt(norm1) * Math.Sqrt(norm2);
        if (denominator < 1e-10)
            return 0;

        return dotProduct / denominator;
    }

    public float[] ComputeCentroid(IReadOnlyList<float[]> embeddings)
    {
        if (embeddings.Count == 0)
            return [];

        var dimension = embeddings[0].Length;
        var centroid = new float[dimension];

        foreach (var embedding in embeddings)
        {
            for (int i = 0; i < Math.Min(dimension, embedding.Length); i++)
            {
                centroid[i] += embedding[i];
            }
        }

        var count = (float)embeddings.Count;
        for (int i = 0; i < dimension; i++)
        {
            centroid[i] /= count;
        }

        return NormalizeVector(centroid);
    }

    public Task RebuildClustersAsync(
        IReadOnlyList<FaceEmbedding> allEmbeddings,
        double similarityThreshold = 0.6,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            _logger.LogInformation("Rebuilding clusters for {Count} embeddings with threshold {Threshold}",
                allEmbeddings.Count, similarityThreshold);

            var clusters = ClusterInternal(allEmbeddings, similarityThreshold, cancellationToken);

            _logger.LogInformation("Rebuild complete: {ClusterCount} clusters identified",
                clusters.GroupBy(c => c.PersonId).Count());
        }, cancellationToken);
    }

    private List<ClusterResult> ClusterInternal(
        IReadOnlyList<FaceEmbedding> embeddings,
        double similarityThreshold,
        CancellationToken cancellationToken)
    {
        if (embeddings.Count == 0)
            return [];

        var validEmbeddings = embeddings
            .Where(e => e.Embedding != null && e.Embedding.Length > 0)
            .ToList();

        if (validEmbeddings.Count == 0)
            return [];

        _logger.LogDebug("Clustering {Count} valid embeddings", validEmbeddings.Count);

        var assignment = new int[validEmbeddings.Count];
        for (int i = 0; i < assignment.Length; i++)
            assignment[i] = -1;

        var clusterCount = 0;
        var representativeIndices = new List<int>();

        for (int i = 0; i < validEmbeddings.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (assignment[i] >= 0) continue;

            var clusterId = clusterCount++;
            assignment[i] = clusterId;
            representativeIndices.Add(i);

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                CancellationToken = cancellationToken
            };

            Parallel.For(i + 1, validEmbeddings.Count, parallelOptions, j =>
            {
                if (assignment[j] >= 0) return;

                var similarity = ComputeSimilarity(
                    validEmbeddings[i].Embedding!,
                    validEmbeddings[j].Embedding!);

                if (similarity >= similarityThreshold)
                {
                    lock (assignment)
                    {
                        if (assignment[j] == -1)
                        {
                            assignment[j] = clusterId;
                        }
                    }
                }
            });
        }

        var results = new List<ClusterResult>();
        var clusterGroups = new Dictionary<int, List<int>>();

        for (int i = 0; i < validEmbeddings.Count; i++)
        {
            var clusterId = assignment[i];
            if (!clusterGroups.ContainsKey(clusterId))
                clusterGroups[clusterId] = new List<int>();
            clusterGroups[clusterId].Add(i);
        }

        foreach (var (clusterId, memberIndices) in clusterGroups)
        {
            var representativeIdx = representativeIndices[clusterId];
            var representativeEmbedding = validEmbeddings[representativeIdx].Embedding!;

            foreach (var memberIdx in memberIndices)
            {
                var similarity = ComputeSimilarity(
                    representativeEmbedding,
                    validEmbeddings[memberIdx].Embedding!);

                results.Add(new ClusterResult
                {
                    FaceId = validEmbeddings[memberIdx].FaceId,
                    PersonId = clusterId,
                    Similarity = similarity,
                    IsNewPerson = true
                });
            }
        }

        _logger.LogDebug("Clustering complete: {ClusterCount} clusters, {TotalFaces} faces",
            clusterCount, validEmbeddings.Count);

        return results;
    }

    private static float[] NormalizeVector(float[] vector)
    {
        var norm = 0.0f;
        for (int i = 0; i < vector.Length; i++)
        {
            norm += vector[i] * vector[i];
        }
        norm = MathF.Sqrt(norm);

        if (norm < 1e-6f)
            return vector;

        var normalized = new float[vector.Length];
        for (int i = 0; i < vector.Length; i++)
        {
            normalized[i] = vector[i] / norm;
        }

        return normalized;
    }
}
