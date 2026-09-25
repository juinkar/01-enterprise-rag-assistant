# Enterprise RAG Assistant

A portfolio-ready .NET 8 Web API demonstrating an enterprise RAG pipeline:

**PDF → text extraction → chunking → Azure OpenAI embeddings → vector retrieval → Semantic Kernel → GPT-4 deployment**

It has two storage modes:

1. **Local mode (default):** embeddings are stored in an in-memory vector store, so you can run the application without creating Azure AI Search.
2. **Azure mode:** set `Rag:UseAzureSearch=true` and configure Azure AI Search to use a real vector index.

## Tech stack

- .NET 8 Web API
- Semantic Kernel
- Azure OpenAI
- Azure AI Search vector search
- PdfPig PDF extraction
- Swagger/OpenAPI
- C# dependency injection

Microsoft's current Semantic Kernel documentation shows the Azure OpenAI connector pattern used here, and Azure AI Search's .NET SDK supports vector indexes and vector queries. See the official docs:
- https://learn.microsoft.com/en-us/semantic-kernel/concepts/ai-services/chat-completion/
- https://learn.microsoft.com/en-us/azure/search/search-get-started-vector

## 1. Prerequisites

- .NET 8 SDK
- Visual Studio 2022+ or VS Code with C# Dev Kit
- Azure OpenAI resource
- An Azure OpenAI chat deployment such as a GPT-4-class deployment
- An Azure OpenAI embedding deployment such as `text-embedding-3-small`

Azure AI Search is optional for local mode.

## 2. Configure secrets

For local development, use user-secrets instead of committing API keys:

```bash
cd src/EnterpriseRagAssistant

dotnet user-secrets init
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://YOUR-RESOURCE.openai.azure.com"
dotnet user-secrets set "AzureOpenAI:ApiKey" "YOUR-KEY"
dotnet user-secrets set "AzureOpenAI:ChatDeployment" "gpt-4o"
dotnet user-secrets set "AzureOpenAI:EmbeddingDeployment" "text-embedding-3-small"
```

Keep:

```json
"Rag": {
  "UseAzureSearch": false
}
```

for the easiest local run.

## 3. Run

```bash
dotnet restore
dotnet run
```

Open:

`http://localhost:5180/swagger`

## 4. Test the RAG flow

### Upload PDF

Swagger → `POST /api/documents/ingest` → choose a PDF.

### Ask a question

Swagger → `POST /api/chat/ask`

```json
{
  "question": "What is this document about?",
  "topK": 5
}
```

The API embeds the question, retrieves the closest chunks and sends only those chunks to the Semantic Kernel chat service.

## 5. Azure AI Search mode

Create an Azure AI Search service and configure:

```bash
dotnet user-secrets set "Rag:UseAzureSearch" "true"
dotnet user-secrets set "AzureSearch:Endpoint" "https://YOUR-SERVICE.search.windows.net"
dotnet user-secrets set "AzureSearch:ApiKey" "YOUR-KEY"
dotnet user-secrets set "AzureSearch:IndexName" "enterprise-rag-index"
```

Then restart the API and ingest the PDF again.

## Architecture

             ┌──────────────┐
PDF ────────►│ PDF Extractor│
             └──────┬───────┘
                    ▼
             ┌──────────────┐
             │ Text Chunker │
             └──────┬───────┘
                    ▼
             ┌─────────────────────┐
             │ Azure OpenAI        │
             │ text-embedding-3    │
             └──────────┬──────────┘
                        ▼
                ┌───────────────┐
                │ Vector Store  │
                │ Local / Azure  │
                └───────┬───────┘
                        │
Question ─► Embedding ─► Vector Search
                        │
                        ▼
                ┌───────────────┐
                │ Semantic      │
                │ Kernel        │
                └───────┬───────┘
                        ▼
                   GPT-4 class
                     answer

## GitHub

Do not commit `appsettings.Local.json`, API keys or private PDFs.
