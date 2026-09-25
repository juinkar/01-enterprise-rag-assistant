using EnterpriseRagAssistant.Models;

namespace EnterpriseRagAssistant.Services;

public sealed class LocalVectorStore
{
    private readonly List<DocumentChunk> _chunks = [];
    private readonly object _lock = new();

    public void AddRange(IEnumerable<DocumentChunk> chunks)
    {
        lock (_lock)
        {
            _chunks.AddRange(chunks);
        }
    }

    public IReadOnlyList<DocumentChunk> Search(float[] query, int topK)
    {
        lock (_lock)
        {
            return _chunks
                .Where(x => x.Embedding is not null)
                .Select(x => (Chunk: x, Score: Cosine(query, x.Embedding!)))
                .OrderByDescending(x => x.Score)
                .Take(topK)
                .Select(x => x.Chunk)
                .ToList();
        }
    }

    private static double Cosine(float[] a, float[] b)
    {
        var length = Math.Min(a.Length, b.Length);
        double dot = 0, na = 0, nb = 0;

        for (var i = 0; i < length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }

        return na == 0 || nb == 0 ? 0 : dot / (Math.Sqrt(na) * Math.Sqrt(nb));
    }

    public int Count
    {
        get { lock (_lock) return _chunks.Count; }
    }
}
