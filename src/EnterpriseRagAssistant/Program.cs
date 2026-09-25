using EnterpriseRagAssistant.Services;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 25 * 1024 * 1024;
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddPolicy("dev", policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

builder.Services.AddHttpClient();
builder.Services.AddSingleton<PdfTextExtractor>();
builder.Services.AddSingleton<TextChunker>();
builder.Services.AddSingleton<EmbeddingService>();
builder.Services.AddSingleton<LocalVectorStore>();
builder.Services.AddSingleton<AzureSearchVectorStore>();
builder.Services.AddSingleton<RagService>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("dev");
app.MapControllers();

app.MapGet("/", () => Results.Ok(new
{
    name = "Enterprise RAG Assistant",
    status = "running",
    endpoints = new[] { "POST /api/documents/ingest", "POST /api/chat/ask" }
}));

app.Run();
