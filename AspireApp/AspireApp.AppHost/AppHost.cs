using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.AspireApp_ApiService>("apiservice")
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.AspireApp_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

var textEmbedding = builder.AddDockerfile(
    "textEmbedding", "../AspireApp.TextEmbedding")
    .WithEndpoint(name: "http", port: 8001, targetPort: 8001, scheme: "http")
    .WithVolume("data", "/qdrant/storage");

var qdrant = builder.AddContainer("qdrant", "qdrant/qdrant:latest")
    .WithEndpoint(name: "http", port: 6334, targetPort: 6334)
    .WithVolume("qdrant-data", "/qdrant/storage");

var ragService = builder.AddProject<Projects.AspireApp_RagService>("ragservice")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithEnvironment("TEXT_EMBEDDING_URL", "http://localhost:8001/")
    .WaitFor(qdrant)
    .WaitFor(textEmbedding);


builder.Build().Run();
