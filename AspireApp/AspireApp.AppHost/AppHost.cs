using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.AspireApp_ApiService>("apiservice")
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.AspireApp_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

var documentRetrievalService = builder.AddDockerfile(
    "DocumentRetrievalService", "../AspireApp.DocumentRetrievalService")
    .WithEndpoint(name: "http", port: 8001, targetPort: 8001, scheme: "http")
    .WithVolume("documentRetrievalServiceData", "/app/data");


builder.Build().Run();
