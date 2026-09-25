using EnterpriseRagAssistant.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Text;

namespace EnterpriseRagAssistant.Services;

public sealed class RagService
{
    private readonly IConfiguration _configuration;
    private readonly PdfTextExtractor _pdf;
    private readonly TextChunker _chunker;
    private readonly EmbeddingService _embeddings;
    private readonly LocalVectorStore _local;
    private readonly AzureSearchVectorStore _azureSearch;

    public RagService(
        IConfiguration configuration,
        PdfTextExtractor pdf,
        TextChunker chunker,
        EmbeddingService embeddings,
        LocalVectorStore local,
        AzureSearchVectorStore azureSearch)
    {
        _configuration = configuration;
        _pdf = pdf;
        _chunker = chunker;
        _embeddings = embeddings;
        _local = local;
        _azureSearch = azureSearch;
    }

    public async Task<IngestResponse> IngestAsync(string fileName, Stream stream, CancellationToken cancellationToken)
    {
        var pages = _pdf.Extract(stream);
        var chunks = new List<DocumentChunk>();

        foreach (var page in pages)
        {
            var pageChunks = _chunker.Chunk(page.Text);

            for (var i = 0; i < pageChunks.Count; i++)
            {
                var content = pageChunks[i];
                var embedding = await _embeddings.CreateEmbeddingAsync(content, cancellationToken);

                chunks.Add(new DocumentChunk(
                    $"{Guid.NewGuid():N}",
                    fileName,
                    page.PageNumber,
                    content,
                    embedding));
            }
        }

        var useAzure = _configuration.GetValue("Rag:UseAzureSearch", false);

        if (useAzure)
        {
            if (chunks.Count > 0)
                await _azureSearch.EnsureIndexAsync(chunks[0].Embedding!.Length, cancellationToken);

            await _azureSearch.AddAsync(chunks, cancellationToken);
        }
        else
        {
            _local.AddRange(chunks);
        }

        return new IngestResponse(
            fileName,
            pages.Count,
            chunks.Count,
            useAzure ? "Azure AI Search" : "Local in-memory vector store");
    }

    public async Task<AskResponse> AskAsync(string question, int? topK, CancellationToken cancellationToken)
    {
        var k = topK ?? _configuration.GetValue("Rag:TopK", 5);
        var queryVector = await _embeddings.CreateEmbeddingAsync(question, cancellationToken);
        var useAzure = _configuration.GetValue("Rag:UseAzureSearch", false);

        var hits = useAzure
            ? await _azureSearch.SearchAsync(queryVector, k, cancellationToken)
            : _local.Search(queryVector, k);

        if (hits.Count == 0)
        {
            return new AskResponse(
                "I could not find any indexed document content. Upload a PDF first.",
                []);
        }

        var context = new StringBuilder();

        foreach (var hit in hits)
        {
            context.AppendLine($"[Source: {hit.FileName}, page {hit.PageNumber}]");
            context.AppendLine(hit.Content);
            context.AppendLine();
        }

        var endpoint = _configuration["AzureOpenAI:Endpoint"];
        var key = _configuration["AzureOpenAI:ApiKey"];
        var deployment = _configuration["AzureOpenAI:ChatDeployment"];

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("Configure AzureOpenAI settings before asking questions.");

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.AddAzureOpenAIChatCompletion(
            deploymentName: deployment!,
            apiKey: key!,
            endpoint: endpoint!);

        var kernel = kernelBuilder.Build();
        var chat = kernel.GetRequiredService<IChatCompletionService>();

        var prompt = $"""
            You are an enterprise document assistant.
            Answer only from the supplied context.
            If the context does not contain the answer, say that the information is not available in the uploaded documents.
            Keep the answer clear and practical.
            Mention the relevant source file/page when useful.

            CONTEXT:
            {context}

            QUESTION:
            {question}
            """;

        var history = new ChatHistory();
        history.AddSystemMessage("You answer questions grounded in enterprise documents.");
        history.AddUserMessage(prompt);

        var response = await chat.GetChatMessageContentAsync(history, cancellationToken: cancellationToken);
        var answer = response.Content ?? "No answer was generated.";

        var citations = hits.Select(x => new SourceCitation(
            x.FileName,
            x.PageNumber,
            0,
            x.Content.Length > 180 ? x.Content[..180] + "..." : x.Content)).ToList();

        return new AskResponse(answer, citations);
    }
}
