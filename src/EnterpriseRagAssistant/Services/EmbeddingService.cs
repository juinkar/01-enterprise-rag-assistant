using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace EnterpriseRagAssistant.Services;

public sealed class EmbeddingService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public EmbeddingService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task<float[]> CreateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var endpoint = _configuration["AzureOpenAI:Endpoint"];
        var key = _configuration["AzureOpenAI:ApiKey"];
        var deployment = _configuration["AzureOpenAI:EmbeddingDeployment"];

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("Configure AzureOpenAI:Endpoint and AzureOpenAI:ApiKey before using embeddings.");

        var url = $"{endpoint.TrimEnd('/')}/openai/deployments/{deployment}/embeddings?api-version=2024-10-21";

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("api-key", key);
        request.Content = new StringContent(
            JsonSerializer.Serialize(new { input = text }),
            Encoding.UTF8,
            "application/json");

        var client = _httpClientFactory.CreateClient();
        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Embedding request failed: {(int)response.StatusCode} {body}");

        using var json = JsonDocument.Parse(body);
        var values = json.RootElement.GetProperty("data")[0].GetProperty("embedding");
        return values.EnumerateArray().Select(x => x.GetSingle()).ToArray();
    }
}
