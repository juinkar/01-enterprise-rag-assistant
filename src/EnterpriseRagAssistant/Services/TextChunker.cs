namespace EnterpriseRagAssistant.Services;

public sealed class TextChunker
{
    private readonly IConfiguration _configuration;

    public TextChunker(IConfiguration configuration) => _configuration = configuration;

    public IReadOnlyList<string> Chunk(string text)
    {
        var size = _configuration.GetValue("Rag:ChunkSize", 1200);
        var overlap = _configuration.GetValue("Rag:ChunkOverlap", 200);
        overlap = Math.Min(overlap, Math.Max(0, size - 1));

        var chunks = new List<string>();
        var start = 0;

        while (start < text.Length)
        {
            var length = Math.Min(size, text.Length - start);
            var chunk = text.Substring(start, length).Trim();

            if (!string.IsNullOrWhiteSpace(chunk))
                chunks.Add(chunk);

            if (start + length >= text.Length)
                break;

            start += Math.Max(1, size - overlap);
        }

        return chunks;
    }
}
