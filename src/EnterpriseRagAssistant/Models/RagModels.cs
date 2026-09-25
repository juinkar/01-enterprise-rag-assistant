namespace EnterpriseRagAssistant.Models;

public record DocumentChunk(
    string Id,
    string FileName,
    int PageNumber,
    string Content,
    float[]? Embedding = null);

public record AskRequest(string Question, int? TopK = null);

public record SourceCitation(string FileName, int PageNumber, double Score, string Preview);

public record AskResponse(string Answer, IReadOnlyList<SourceCitation> Sources);

public record IngestResponse(string FileName, int Pages, int Chunks, string StorageMode);
