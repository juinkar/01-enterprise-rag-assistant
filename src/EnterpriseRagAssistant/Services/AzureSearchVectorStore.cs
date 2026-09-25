using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using EnterpriseRagAssistant.Models;

namespace EnterpriseRagAssistant.Services;

public sealed class AzureSearchVectorStore
{
    private readonly IConfiguration _configuration;

    public AzureSearchVectorStore(IConfiguration configuration) => _configuration = configuration;

    private bool Enabled =>
        _configuration.GetValue("Rag:UseAzureSearch", false) &&
        !string.IsNullOrWhiteSpace(_configuration["AzureSearch:Endpoint"]) &&
        !string.IsNullOrWhiteSpace(_configuration["AzureSearch:ApiKey"]);

    private string IndexName => _configuration["AzureSearch:IndexName"] ?? "enterprise-rag-index";

    public async Task EnsureIndexAsync(int dimensions, CancellationToken cancellationToken = default)
    {
        if (!Enabled) return;

        var endpoint = new Uri(_configuration["AzureSearch:Endpoint"]!);
        var key = new AzureKeyCredential(_configuration["AzureSearch:ApiKey"]!);
        var indexClient = new SearchIndexClient(endpoint, key);

        var fields = new FieldBuilder().Build(typeof(SearchDocumentModel));
        var index = new SearchIndex(IndexName, fields)
        {
            VectorSearch = new VectorSearch
            {
                Algorithms = { new HnswAlgorithmConfiguration("hnsw") },
                Profiles = { new VectorSearchProfile("vector-profile", "hnsw") }
            }
        };

        // Keep vector dimensions configurable by recreating the vector field definition.
        index.Fields.Remove(index.Fields.First(f => f.Name == "contentVector"));
        index.Fields.Add(new SearchField("contentVector", SearchFieldDataType.Collection(SearchFieldDataType.Single))
        {
            IsSearchable = true,
            VectorSearchDimensions = dimensions,
            VectorSearchProfileName = "vector-profile"
        });

        await indexClient.CreateOrUpdateIndexAsync(index, cancellationToken: cancellationToken);
    }

    public async Task AddAsync(IEnumerable<DocumentChunk> chunks, CancellationToken cancellationToken = default)
    {
        if (!Enabled) return;

        var client = new SearchClient(
            new Uri(_configuration["AzureSearch:Endpoint"]!),
            IndexName,
            new AzureKeyCredential(_configuration["AzureSearch:ApiKey"]!));

        var docs = chunks.Select(x => new SearchDocumentModel
        {
            Id = x.Id,
            FileName = x.FileName,
            PageNumber = x.PageNumber,
            Content = x.Content,
            ContentVector = x.Embedding!
        });

        await client.IndexDocumentsAsync(IndexDocumentsBatch.Upload(docs), cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<DocumentChunk>> SearchAsync(float[] query, int topK, CancellationToken cancellationToken = default)
    {
        if (!Enabled) return [];

        var client = new SearchClient(
            new Uri(_configuration["AzureSearch:Endpoint"]!),
            IndexName,
            new AzureKeyCredential(_configuration["AzureSearch:ApiKey"]!));

        var vectorQuery = new VectorizedQuery(query)
        {
            KNearestNeighborsCount = topK,
            Fields = { "contentVector" }
        };

        var options = new SearchOptions
        {
            Size = topK,
            VectorSearch = new()
            {
                Queries = { vectorQuery }
            }
        };

        var response = await client.SearchAsync<SearchDocumentModel>(null, options, cancellationToken);
        var result = new List<DocumentChunk>();

        await foreach (var item in response.Value.GetResultsAsync())
        {
            result.Add(new DocumentChunk(
                item.Document.Id,
                item.Document.FileName,
                item.Document.PageNumber,
                item.Document.Content,
                item.Document.ContentVector));
        }

        return result;
    }

    public sealed class SearchDocumentModel
    {
        [SimpleField(IsKey = true)]
        public string Id { get; set; } = "";

        [SearchableField]
        public string FileName { get; set; } = "";

        [SimpleField]
        public int PageNumber { get; set; }

        [SearchableField]
        public string Content { get; set; } = "";

        public float[] ContentVector { get; set; } = [];
    }
}
