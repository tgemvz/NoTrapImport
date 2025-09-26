var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.AspireApp_ApiService>("apiservice")
    .WithHttpHealthCheck("/health");


builder.AddProject<Projects.Frontend>("frontend")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
